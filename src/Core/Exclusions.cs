using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gitic;

public static class Exclusions
{
    public static readonly HashSet<string> LockFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "package-lock.json",
        "npm-shrinkwrap.json",
        "pnpm-lock.yaml",
        "yarn.lock",
        "Cargo.lock",
        "Gemfile.lock",
        "composer.lock"
    };

    public static readonly HashSet<string> BinaryImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpeg",
        ".jpg",
        ".png",
        ".gif",
        ".webp",
        ".ico",
        ".bmp"
    };

    public static readonly HashSet<string> NonCodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".markdown",
        ".mdown",
        ".svg",
        ".txt",
        ".pdf",
        ".docx",
        ".doc",
        ".xlsx",
        ".xls",
        ".pptx",
        ".ppt",
        ".csv",
        ".rtf",
        ".epub",
        ".mobi",
        ".zip",
        ".tar",
        ".gz",
        ".rar",
        ".7z"
    };

    public static readonly Dictionary<string, string> DirectoryExcludes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".git", ".git/**" },
        { "node_modules", "node_modules/**" },
        { "vendor", "vendor/**" },
        { "dist", "dist/**" },
        { "build", "build/**" },
        { "coverage", "coverage/**" },
        { ".next", ".next/**" },
        { "out", "out/**" },
        { "target", "target/**" }
    };
}

public class ExclusionCategory
{
    public string Category { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
}

public interface IPathClassifier
{
    bool Check(string path);
    List<ExclusionSummary> GetExclusions();
}

public class PathClassifier : IPathClassifier
{
    public static List<ExcludeRule> ParseGitignoreLines(IEnumerable<string> lines, string category = "gitignore")
    {
        List<ExcludeRule> rules = [];
        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            string pattern = trimmed;
            if (pattern.EndsWith('/'))
            {
                pattern += "**";
            }

            if (pattern.StartsWith('/'))
            {
                pattern = pattern[1..];
            }
            else if (!pattern.StartsWith("**/"))
            {
                pattern = "**/" + pattern;
            }

            rules.Add(new() { Pattern = pattern, Category = category });

            if (pattern.StartsWith("**/"))
            {
                rules.Add(new() { Pattern = pattern[3..], Category = category });
            }
        }
        return rules;
    }

    public static List<ExcludeRule> LoadGitignoreRules(string repoRoot, IFileSystem? fileSystem = null)
    {
        fileSystem ??= new PhysicalFileSystem();
        try
        {
            string gitignorePath = Path.Combine(repoRoot, ".gitignore");
            if (!fileSystem.FileExists(gitignorePath))
            {
                return [];
            }

            var lines = new List<string>();
            using (var stream = fileSystem.OpenRead(gitignorePath))
            using (var reader = new StreamReader(stream))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }
            return ParseGitignoreLines(lines);
        }
        catch
        {
            return [];
        }
    }

    private readonly HashSet<string> _headFiles;
    private readonly List<ExcludeRule> _excludes;
    private readonly bool _includeDeleted;
    private readonly string? _requestedPath;
    private readonly Dictionary<string, ExclusionSummary> _exclusions = new();

    public PathClassifier(
        HashSet<string> headFiles,
        List<ExcludeRule> excludes,
        bool includeDeleted,
        string? requestedPath)
    {
        _headFiles = headFiles;
        _excludes = excludes;
        _includeDeleted = includeDeleted;
        _requestedPath = requestedPath is null ? null : PathUtils.NormalizeGitPath(requestedPath).TrimEnd('/');
    }

    public bool Check(string path)
    {
        string normalizedPath = PathUtils.NormalizeGitPath(path);
        if (!IsInsideRequestedPath(normalizedPath))
        {
            return false;
        }

        var exclusion = ClassifyExclusion(normalizedPath);
        if (exclusion != null)
        {
            AddExclusion(exclusion.Category, exclusion.Pattern);
            return false;
        }

        return true;
    }

    public List<ExclusionSummary> GetExclusions()
    {
        return _exclusions.Values.ToList();
    }

    private bool IsInsideRequestedPath(string path)
    {
        if (string.IsNullOrEmpty(_requestedPath))
        {
            return true;
        }
        return path == _requestedPath || path.StartsWith(_requestedPath + "/");
    }

    private ExclusionCategory? ClassifyExclusion(string path)
    {
        var defaultExclusion = ClassifyDefaultExclusion(path);
        if (defaultExclusion != null)
        {
            return defaultExclusion;
        }

        var configuredExclusion = ClassifyConfiguredExclusion(path);
        if (configuredExclusion != null)
        {
            return configuredExclusion;
        }

        if (!_includeDeleted && !_headFiles.Contains(path))
        {
            return new() { Category = "deleted", Pattern = "missing from HEAD" };
        }

        return null;
    }

    private ExclusionCategory? ClassifyDefaultExclusion(string path)
    {
        var segments = path.Split('/');
        for (int i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            if (Exclusions.DirectoryExcludes.TryGetValue(segment, out var pattern))
            {
                return new() { Category = "generated_or_vendor", Pattern = pattern };
            }
            if (segment.Equals("generated", StringComparison.OrdinalIgnoreCase))
            {
                return new() { Category = "generated", Pattern = "**/generated/**" };
            }
        }

        string fileName = segments.LastOrDefault() ?? path;
        if (IsLockfile(fileName))
        {
            return new() { Category = "lockfile", Pattern = "lockfiles" };
        }
        if (fileName.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase))
        {
            return new() { Category = "generated", Pattern = "*.min.js" };
        }
        if (fileName.Contains(".generated.", StringComparison.OrdinalIgnoreCase))
        {
            return new() { Category = "generated", Pattern = "*.generated.*" };
        }
        if (fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
        {
            return new() { Category = "generated", Pattern = "*.Designer.cs" };
        }
        if (fileName.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
        {
            return new() { Category = "generated", Pattern = "ModelSnapshot.cs" };
        }
        if (IsBinaryImage(fileName))
        {
            return new() { Category = "binary", Pattern = "image files" };
        }
        if (IsNonCode(fileName))
        {
            return new() { Category = "non_code", Pattern = "non-code files" };
        }
        return null;
    }

    private bool IsBinaryImage(string fileName) => Exclusions.BinaryImageExtensions.Contains(Path.GetExtension(fileName));

    private bool IsNonCode(string fileName) => Exclusions.NonCodeExtensions.Contains(Path.GetExtension(fileName));

    private ExclusionCategory? ClassifyConfiguredExclusion(string path)
    {
        foreach (var exclude in _excludes)
        {
            if (PathUtils.MatchesPathPattern(path, exclude.Pattern))
            {
                return new()
                {
                    Category = exclude.Category,
                    Pattern = exclude.Pattern
                };
            }
        }
        return null;
    }

    private bool IsLockfile(string fileName) => fileName.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) || Exclusions.LockFiles.Contains(fileName);

    private void AddExclusion(string category, string pattern)
    {
        string key = $"{category}:{pattern}";
        if (!_exclusions.TryGetValue(key, out var current))
        {
            current = new() { Category = category, Pattern = pattern, Count = 0 };
            _exclusions[key] = current;
        }
        current.Count += 1;
    }
}
