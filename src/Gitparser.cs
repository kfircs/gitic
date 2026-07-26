using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gitic
{
    public interface IGitParser
    {
        string CommitMarker { get; }
        string NumstatMarker { get; }
        List<GitCommitRecord> ParseGitLog(string output);
        GitCommitRecord? ParseCommitRecord(string record);
        List<GitFileChange> ParseNumstatAndPatches(string text);
        List<string> BuildGitLogArguments(GitHistoryExtractorOptions options);
    }

    public class GitParser : IGitParser
    {
        private const int MaxSymbolLength = 60;

        public string CommitMarker => "__GITIZER_COMMIT__";
        public string NumstatMarker => "__GITIZER_NUMSTAT__";

        public List<string> BuildGitLogArguments(GitHistoryExtractorOptions options)
        {
            var opt = options ?? new GitHistoryExtractorOptions();
            var args = new List<string>
            {
                "log",
                "--numstat",
                "-p",
                $"--format=format:{CommitMarker}%n%H%n%aI%n%an%n%ae%n%P%n%B%n{NumstatMarker}"
            };

            if (opt.IncludeMerges)
            {
                args.Add("--cc");
            }
            else
            {
                args.Add("--no-merges");
            }

            if (!opt.AllTime)
            {
                args.Add($"--since={opt.Since ?? GitUtils.DefaultSinceDate()}");
            }

            return args;
        }

        public List<GitCommitRecord> ParseGitLog(string output)
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

        public GitCommitRecord? ParseCommitRecord(string record)
        {
            int markerIndex = record.IndexOf(NumstatMarker);
            if (markerIndex == -1)
            {
                return null;
            }

            string metadataStr = record.Substring(0, markerIndex).TrimEnd();
            var metadata = metadataStr.Split('\n');
            if (!TryParseMetadataLines(metadata, out string hash, out string date, out string authorName, out string authorEmail, out string parentsLine, out List<string> messageLines))
            {
                return null;
            }

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

        public List<GitIdentity> ParseCoAuthors(string message)
        {
            var identities = new List<GitIdentity>();
            var seen = new HashSet<string>();
            var matches = GitRegexConstants.CoAuthoredByRegex.Matches(message);
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

        public string NormalizeNumstatPath(string path)
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

        private bool TryParseMetadataLines(
            string[] metadata,
            out string hash,
            out string date,
            out string authorName,
            out string authorEmail,
            out string parentsLine,
            out List<string> messageLines)
        {
            if (metadata.Length < 5)
            {
                hash = string.Empty;
                date = string.Empty;
                authorName = string.Empty;
                authorEmail = string.Empty;
                parentsLine = string.Empty;
                messageLines = new List<string>();
                return false;
            }

            hash = metadata[0];
            date = metadata[1];
            authorName = metadata[2];
            authorEmail = metadata[3];
            parentsLine = metadata[4];
            messageLines = metadata.Skip(5).ToList();
            return true;
        }

        private int ParseDiffLineCount(string value)
        {
            return value == "-" ? 0 : (int.TryParse(value, out var parsedVal) ? parsedVal : 0);
        }
    }
}
