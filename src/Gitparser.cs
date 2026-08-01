using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("Gitic.Tests")]

namespace Gitic
{
    internal class GitParser : IGitParser
    {
        private readonly IGitPatchParser _patchParser;

        public GitParser(IGitPatchParser patchParser)
        {
            _patchParser = patchParser ?? throw new ArgumentNullException(nameof(patchParser));
        }

        public string CommitMarker => "__GITIC_COMMIT__";
        public string NumstatMarker => "__GITIC_NUMSTAT__";

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

        private record GitCommitMetadata(
            string Hash,
            string Date,
            string AuthorName,
            string AuthorEmail,
            string ParentsLine,
            List<string> MessageLines);

        /// <summary>
        /// Parses an individual raw commit record containing metadata and patch details.
        /// Retained as public/internal helper to ensure backward-compatibility if referenced.
        /// </summary>
        public GitCommitRecord? ParseCommitRecord(string record)
        {
            int markerIndex = record.IndexOf(NumstatMarker);
            if (markerIndex == -1)
            {
                return null;
            }

            string metadataStr = record.Substring(0, markerIndex).TrimEnd();
            var metadata = metadataStr.Split('\n');
            var commitMetadata = TryParseMetadataLines(metadata);
            if (commitMetadata == null)
            {
                return null;
            }

            string message = string.Join("\n", commitMetadata.MessageLines).Trim();
            
            string numstatText = record.Substring(markerIndex + NumstatMarker.Length);
            var files = _patchParser.ParseNumstatAndPatches(numstatText);

            var parents = ParseParents(commitMetadata.ParentsLine);

            long timestamp = 0;
            if (DateTimeOffset.TryParse(commitMetadata.Date.Trim(), out var parsedDate))
            {
                timestamp = parsedDate.ToUnixTimeMilliseconds();
            }

            return new GitCommitRecord
            {
                Hash = commitMetadata.Hash.Trim(),
                Date = commitMetadata.Date.Trim(),
                Timestamp = timestamp,
                Author = new GitIdentity
                {
                    Name = commitMetadata.AuthorName.Trim(),
                    Email = commitMetadata.AuthorEmail.Trim()
                },
                CoAuthors = ParseCoAuthors(message),
                ParentCount = parents.Count,
                Parents = parents,
                Message = message,
                Files = files
            };
        }

        /// <summary>
        /// Parses co-author signatures (Co-authored-by:) from the commit message.
        /// </summary>
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

        private GitCommitMetadata? TryParseMetadataLines(string[] metadata)
        {
            if (metadata.Length < 5)
            {
                return null;
            }

            return new GitCommitMetadata(
                Hash: metadata[0],
                Date: metadata[1],
                AuthorName: metadata[2],
                AuthorEmail: metadata[3],
                ParentsLine: metadata[4],
                MessageLines: metadata.Skip(5).ToList()
            );
        }

        private static List<string> ParseParents(string parentsLine) =>
            string.IsNullOrWhiteSpace(parentsLine)
                ? []
                : parentsLine.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
