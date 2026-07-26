using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gitic
{
    internal interface IGitPatchParser
    {
        List<GitFileChange> ParseNumstatAndPatches(string text);
        (List<GitFileChange> fileChanges, Dictionary<string, GitFileChange> fileChangesMap) ParseNumstatMetadata(List<string> lines);
        void ExtractSymbolsFromHunks(List<string> lines, Dictionary<string, GitFileChange> fileChangesMap);
        string CleanSymbol(string symbol);
        string NormalizeNumstatPath(string path);
    }

    internal class GitPatchParser : IGitPatchParser
    {
        private const int MaxSymbolLength = 60;

        public List<GitFileChange> ParseNumstatAndPatches(string text)
        {
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();

            var (fileChanges, fileChangesMap) = ParseNumstatMetadata(lines);
            ExtractSymbolsFromHunks(lines, fileChangesMap);
            return fileChanges;
        }

        public (List<GitFileChange> fileChanges, Dictionary<string, GitFileChange> fileChangesMap) ParseNumstatMetadata(List<string> lines)
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

        public void ExtractSymbolsFromHunks(
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

            while (true)
            {
                string prev = cleaned;
                cleaned = GitRegexConstants.BracketsSuffixRegex.Replace(cleaned, "");
                if (cleaned == prev)
                {
                    break;
                }
            }

            if (cleaned.Length > MaxSymbolLength)
            {
                cleaned = cleaned.Substring(0, MaxSymbolLength) + "...";
            }

            return cleaned;
        }

        public string NormalizeNumstatPath(string path)
        {
            string normalized = PathUtils.NormalizeGitPath(path);
            var match = GitRegexConstants.BraceRenameRegex.Match(normalized);
            if (match.Success)
            {
                return GitRegexConstants.BraceRenameRegex.Replace(normalized, match.Groups[1].Value);
            }
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
}
