namespace Gitic;

/// <summary>
/// Represents a deep interface for parsing git diff and numstat metadata.
/// Exposes only the high-leverage entry point, keeping extraction details encapsulated.
/// </summary>
internal interface IGitPatchParser
{
    List<GitFileChange> ParseNumstatAndPatches(string text);
}

internal class GitPatchParser : IGitPatchParser
{
    private const int MaxSymbolLength = 60;

    /// <summary>
    /// Parses the raw numstat and patch details into structured file changes.
    /// </summary>
    public List<GitFileChange> ParseNumstatAndPatches(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        var diffStartIdx = lines.FindIndex(l => l.StartsWith("diff --git "));
        List<string> numstatLines;
        List<string> patchLines;

        if (diffStartIdx >= 0)
        {
            numstatLines = lines.Take(diffStartIdx).ToList();
            patchLines = lines.Skip(diffStartIdx).ToList();
        }
        else
        {
            numstatLines = lines;
            patchLines = new List<string>();
        }

        var (fileChanges, fileChangesMap) = ParseNumstatMetadata(numstatLines);
        ExtractSymbolsFromHunks(patchLines, fileChangesMap);
        return fileChanges;
    }

    private (List<GitFileChange> fileChanges, Dictionary<string, GitFileChange> fileChangesMap) ParseNumstatMetadata(List<string> lines)
    {
        var fileChanges = new List<GitFileChange>();
        var fileChangesMap = new Dictionary<string, GitFileChange>();

        foreach (var line in lines)
        {
            if (line.Contains('\t'))
            {
                var parts = line.Split('\t');
                if (parts.Length >= 3)
                {
                    string addedStr = parts[0];
                    string deletedStr = parts[1];
                    var pathParts = parts.Skip(2).ToList();
                    string rawPath = string.Join("\t", pathParts);
                    string path = NormalizeNumstatPath(rawPath);

                    if (path.Length > 0)
                    {
                        int added = ParseDiffLineCount(addedStr);
                        int deleted = ParseDiffLineCount(deletedStr);

                        var change = new GitFileChange { Path = path, Added = added, Deleted = deleted, Symbols = new List<string>() };
                        fileChanges.Add(change);
                        fileChangesMap[path] = change;
                    }
                }
            }
        }

        return (fileChanges, fileChangesMap);
    }

    private void ExtractSymbolsFromHunks(
        List<string> lines,
        Dictionary<string, GitFileChange> fileChangesMap)
    {
        string? currentPath = null;
        foreach (var line in lines)
        {
            if (line.StartsWith("diff --git "))
            {
                var match = GitRegexConstants.DiffGitRegex.Match(line);
                if (match.Success)
                {
                    currentPath = PathUtils.NormalizeGitPath(match.Groups[1].Value);
                }
                else
                {
                    currentPath = null;
                }
            }
            else if (line.StartsWith("@@ ") && currentPath != null)
            {
                var match = GitRegexConstants.HunkHeaderRegex.Match(line);
                if (match.Success && match.Groups.Count >= 2)
                {
                    string symbol = CleanSymbol(match.Groups[1].Value);
                    if (symbol.Length > 0)
                    {
                        if (fileChangesMap.TryGetValue(currentPath, out var change))
                        {
                            change.Symbols ??= new List<string>();
                            if (!change.Symbols.Contains(symbol))
                            {
                                change.Symbols.Add(symbol);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Cleans a symbol extracted from hunk headers by stripping suffixes and decorators.
    /// Public to support direct testing in PortedModulesTests.
    /// </summary>
    public string CleanSymbol(string symbol)
    {
        string cleaned = symbol.Trim();
        if (cleaned.StartsWith('@'))
        {
            return "";
        }
        if (GitRegexConstants.ImportExcludeRegex.IsMatch(cleaned))
        {
            return "";
        }

        cleaned = GitRegexConstants.SemicolonSuffixRegex.Replace(cleaned, "");

        cleaned = GitRegexConstants.BracketsSuffixRegex.Replace(cleaned, "");

        if (cleaned.Length > MaxSymbolLength)
        {
            cleaned = cleaned.Substring(0, MaxSymbolLength) + "...";
        }

        return cleaned;
    }

    /// <summary>
    /// Normalizes git rename/move syntax inside numstat paths.
    /// Public to support direct testing in PortedModulesTests.
    /// </summary>
    public string NormalizeNumstatPath(string path)
    {
        string normalized = PathUtils.NormalizeGitPath(path);
        normalized = GitRegexConstants.BraceRenameRegex.Replace(normalized, "$1");
        if (normalized.Contains(" => ") && !normalized.Contains("{") && !normalized.Contains("}"))
        {
            var parts = normalized.Split(new[] { " => " }, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                return parts[1];
            }
        }
        return normalized;
    }

    private int ParseDiffLineCount(string value)
    {
        return value == "-" ? 0 : (int.TryParse(value, out var parsedVal) ? parsedVal : 0);
    }
}
