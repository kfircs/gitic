using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gitic
{
    public static class GitParser
    {
        public const string CommitMarker = "__GITIZER_COMMIT__";
        public const string NumstatMarker = "__GITIZER_NUMSTAT__";

        public static List<GitCommitRecord> ParseGitLog(string output)
        {
            return output
                .Split(new[] { CommitMarker }, StringSplitOptions.None)
                .Select(record => record.Trim())
                .Where(record => record.Length > 0)
                .Select(ParseCommitRecord)
                .Where(record => record != null)
                .Select(record => record!)
                .ToList();
        }

        public static GitCommitRecord? ParseCommitRecord(string record)
        {
            int markerIndex = record.IndexOf(NumstatMarker);
            if (markerIndex == -1)
            {
                return null;
            }

            string metadataStr = record.Substring(0, markerIndex).TrimEnd();
            var metadata = metadataStr.Split('\n');
            if (metadata.Length < 5)
            {
                return null;
            }

            string hash = metadata[0];
            string date = metadata[1];
            string authorName = metadata[2];
            string authorEmail = metadata[3];
            string parentsLine = metadata[4];

            var messageLines = metadata.Skip(5).ToList();
            string message = string.Join("\n", messageLines).Trim();
            
            string numstatText = record.Substring(markerIndex + NumstatMarker.Length);
            var files = ParseNumstatAndPatches(numstatText);

            var parents = string.IsNullOrWhiteSpace(parentsLine)
                ? new List<string>()
                : parentsLine.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            long timestamp = 0;
            if (DateTimeOffset.TryParse(date.Trim(), out var parsedDate))
            {
                timestamp = parsedDate.ToUnixTimeMilliseconds();
            }

            return new GitCommitRecord
            {
                Hash = hash.Trim(),
                Date = date.Trim(),
                Timestamp = timestamp,
                Author = new GitIdentity
                {
                    Name = authorName.Trim(),
                    Email = authorEmail.Trim()
                },
                CoAuthors = ParseCoAuthors(message),
                ParentCount = parents.Count,
                Parents = parents,
                Message = message,
                Files = files
            };
        }

        public static List<GitFileChange> ParseNumstatAndPatches(string text)
        {
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();

            var (fileChanges, fileChangesMap) = ParseNumstatMetadata(lines);
            ExtractSymbolsFromHunks(lines, fileChangesMap);
            return fileChanges;
        }

        public static (List<GitFileChange> fileChanges, Dictionary<string, GitFileChange> fileChangesMap) ParseNumstatMetadata(List<string> lines)
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
                            int added = addedStr == "-" ? 0 : (int.TryParse(addedStr, out var addVal) ? addVal : 0);
                            int deleted = deletedStr == "-" ? 0 : (int.TryParse(deletedStr, out var delVal) ? delVal : 0);
                            
                            var change = new GitFileChange { Path = path, Added = added, Deleted = deleted, Symbols = new List<string>() };
                            fileChanges.Add(change);
                            fileChangesMap[path] = change;
                        }
                    }
                }
            }

            return (fileChanges, fileChangesMap);
        }

        public static void ExtractSymbolsFromHunks(
            List<string> lines,
            Dictionary<string, GitFileChange> fileChangesMap)
        {
            string? currentPath = null;
            foreach (var line in lines)
            {
                if (line.StartsWith("diff --git "))
                {
                    var match = Regex.Match(line, @" b/(.*)$");
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
                    var match = Regex.Match(line, @"^@@\s+-\d+(?:,\d+)?\s+\+\d+(?:,\d+)?\s+@@\s*(.*)$");
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

        public static string CleanSymbol(string symbol)
        {
            string cleaned = symbol.Trim();
            if (cleaned.StartsWith("@"))
            {
                return "";
            }
            if (Regex.IsMatch(cleaned, @"^(import|require|using|export\s+\*\s+from)\b", RegexOptions.IgnoreCase))
            {
                return "";
            }

            cleaned = Regex.Replace(cleaned, @";\s*$", "");

            while (true)
            {
                string prev = cleaned;
                cleaned = Regex.Replace(cleaned, @"\s*[\{\(\[]\s*$", "");
                if (cleaned == prev)
                {
                    break;
                }
            }

            if (cleaned.Length > 60)
            {
                cleaned = cleaned.Substring(0, 60) + "...";
            }

            return cleaned;
        }

        public static List<GitIdentity> ParseCoAuthors(string message)
        {
            var identities = new List<GitIdentity>();
            var seen = new HashSet<string>();
            var matches = Regex.Matches(message, @"^Co-authored-by:\s*(.*?)\s*<([^>]+)>", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    var identity = new GitIdentity
                    {
                        Name = match.Groups[1].Value.Trim(),
                        Email = match.Groups[2].Value.Trim()
                    };
                    string key = IdentityUtils.IdentityKey(identity);
                    if (!seen.Contains(key))
                    {
                        identities.Add(identity);
                        seen.Add(key);
                    }
                }
            }
            return identities;
        }

        public static string NormalizeNumstatPath(string path)
        {
            string normalized = PathUtils.NormalizeGitPath(path);
            var match = Regex.Match(normalized, @"\{.*? => (.*?)\}");
            if (match.Success)
            {
                return Regex.Replace(normalized, @"\{.*? => (.*?)\}", match.Groups[1].Value);
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
    }
}
