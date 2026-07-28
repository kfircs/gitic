using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("Gitic.Tests")]

namespace Gitic
{
    internal interface IGitParser
    {
        string CommitMarker { get; }
        string NumstatMarker { get; }
        List<GitCommitRecord> ParseGitLog(string output);
        GitCommitRecord? ParseCommitRecord(string record);
        List<string> BuildGitLogArguments(GitHistoryExtractorOptions options);
    }

    internal class GitParser : IGitParser
    {
        private readonly IGitPatchParser _patchParser;

        public GitParser(IGitPatchParser patchParser)
        {
            _patchParser = patchParser;
        }

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
            return Regex.Split(output, $@"^{Regex.Escape(CommitMarker)}\r?$", RegexOptions.Multiline)
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
            var files = _patchParser.ParseNumstatAndPatches(numstatText);

            var parents = ParseParents(parentsLine);

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

        private static List<string> ParseParents(string parentsLine) =>
            string.IsNullOrWhiteSpace(parentsLine)
                ? []
                : parentsLine.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
