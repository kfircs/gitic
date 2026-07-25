using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gitic
{
    public static class Exclusions
    {
        private static readonly HashSet<string> LockFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "package-lock.json",
            "npm-shrinkwrap.json",
            "pnpm-lock.yaml",
            "yarn.lock",
            "Cargo.lock",
            "Gemfile.lock",
            "composer.lock"
        };

        private static readonly HashSet<string> BinaryImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            "jpeg",
            "jpg",
            "png",
            "gif",
            "webp",
            "ico",
            "bmp"
        };

        private static readonly Dictionary<string, string> DirectoryExcludes = new(StringComparer.OrdinalIgnoreCase)
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

        public static string AreaForPath(
            string path,
            int depth,
            List<NamedArea> namedAreas,
            HashSet<string>? warnings = null)
        {
            var matchingAreas = namedAreas.Where(area =>
                area.Paths.Any(areaPath => PathUtils.MatchesPathPattern(path, areaPath))
            ).ToList();

            if (matchingAreas.Count > 0)
            {
                if (matchingAreas.Count > 1 && warnings != null)
                {
                    warnings.Add(
                        $"Path {path} matched multiple configured areas ({string.Join(", ", matchingAreas.Select(a => a.Name))}); using {matchingAreas[0].Name}."
                    );
                }
                return matchingAreas[0].Name;
            }

            var segments = PathUtils.NormalizeGitPath(path).Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 1)
            {
                return ".";
            }
            return string.Join("/", segments.Take(Math.Min(depth, segments.Length - 1)));
        }
    }

    public class ExclusionCategory
    {
        public string Category { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
    }

    public class PathClassifier
    {
        public static List<ExcludeRule> LoadGitignoreRules(string repoRoot)
        {
            var rules = new List<ExcludeRule>();
            string gitignorePath = Path.Combine(repoRoot, ".gitignore");
            if (!File.Exists(gitignorePath))
            {
                return rules;
            }

            try
            {
                var lines = File.ReadAllLines(gitignorePath);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    {
                        continue;
                    }

                    string pattern = trimmed;
                    if (pattern.EndsWith("/"))
                    {
                        pattern += "**";
                    }

                    if (pattern.StartsWith("/"))
                    {
                        pattern = pattern.Substring(1);
                    }
                    else if (!pattern.StartsWith("**/"))
                    {
                        pattern = "**/" + pattern;
                    }

                    rules.Add(new ExcludeRule { Pattern = pattern, Category = "gitignore" });

                    if (pattern.StartsWith("**/"))
                    {
                        rules.Add(new ExcludeRule { Pattern = pattern.Substring(3), Category = "gitignore" });
                    }
                }
            }
            catch
            {
                // Ignore gracefully
            }

            return rules;
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
            _requestedPath = requestedPath == null ? null : PathUtils.NormalizeGitPath(requestedPath).TrimEnd('/');
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
                return new ExclusionCategory { Category = "deleted", Pattern = "missing from HEAD" };
            }

            return null;
        }

        private static readonly HashSet<string> LockFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "package-lock.json",
            "npm-shrinkwrap.json",
            "pnpm-lock.yaml",
            "yarn.lock",
            "Cargo.lock",
            "Gemfile.lock",
            "composer.lock"
        };

        private static readonly HashSet<string> BinaryImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            "jpeg", "jpg", "png", "gif", "webp", "ico", "bmp"
        };

        private static readonly Dictionary<string, string> DirectoryExcludes = new(StringComparer.OrdinalIgnoreCase)
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

        private ExclusionCategory? ClassifyDefaultExclusion(string path)
        {
            var segments = path.Split('/');
            for (int i = 0; i < segments.Length - 1; i++)
            {
                var segment = segments[i];
                if (DirectoryExcludes.TryGetValue(segment, out var pattern))
                {
                    return new ExclusionCategory { Category = "generated_or_vendor", Pattern = pattern };
                }
                if (segment.Equals("generated", StringComparison.OrdinalIgnoreCase))
                {
                    return new ExclusionCategory { Category = "generated", Pattern = "**/generated/**" };
                }
            }

            string fileName = segments.LastOrDefault() ?? path;
            if (IsLockfile(fileName))
            {
                return new ExclusionCategory { Category = "lockfile", Pattern = "lockfiles" };
            }
            if (fileName.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase))
            {
                return new ExclusionCategory { Category = "generated", Pattern = "*.min.js" };
            }
            if (fileName.Contains(".generated.", StringComparison.OrdinalIgnoreCase))
            {
                return new ExclusionCategory { Category = "generated", Pattern = "*.generated.*" };
            }
            if (fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            {
                return new ExclusionCategory { Category = "generated", Pattern = "*.Designer.cs" };
            }
            if (fileName.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
            {
                return new ExclusionCategory { Category = "generated", Pattern = "ModelSnapshot.cs" };
            }
            if (IsBinaryImage(fileName))
            {
                return new ExclusionCategory { Category = "binary", Pattern = "image files" };
            }
            return null;
        }

        private bool IsBinaryImage(string fileName)
        {
            int idx = fileName.LastIndexOf('.');
            if (idx <= 0)
            {
                return false;
            }
            string ext = fileName.Substring(idx + 1).ToLower();
            return BinaryImageExtensions.Contains(ext);
        }

        private ExclusionCategory? ClassifyConfiguredExclusion(string path)
        {
            foreach (var exclude in _excludes)
            {
                if (PathUtils.MatchesPathPattern(path, exclude.Pattern))
                {
                    return new ExclusionCategory
                    {
                        Category = exclude.Category,
                        Pattern = exclude.Pattern
                    };
                }
            }
            return null;
        }

        private bool IsLockfile(string fileName)
        {
            return fileName.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) || LockFiles.Contains(fileName);
        }

        private void AddExclusion(string category, string pattern)
        {
            string key = $"{category}:{pattern}";
            if (!_exclusions.TryGetValue(key, out var current))
            {
                current = new ExclusionSummary { Category = category, Pattern = pattern, Count = 0 };
                _exclusions[key] = current;
            }
            current.Count += 1;
        }
    }
}
