using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public class ChangeAccumulator
    {
        private readonly IPathClassifier _filter;
        private readonly CommitClassifier _classifier = new();
        private readonly Dictionary<string, ItemAccumulator> _files = new();
        private readonly Dictionary<string, ItemAccumulator> _areas = new();
        private readonly Dictionary<string, ContributorAccumulator> _contributors = new();
        private readonly Dictionary<string, ContributorAccumulator> _automation = new();
        private readonly HashSet<string> _warnings = new();
        private int _includedFileChangeCount = 0;
        private readonly IIdentityRegistry _identityRegistry;

        private readonly GitizerConfig _config;
        private readonly AnalysisSettings _settings;

        public ChangeAccumulator(
            GitizerConfig config,
            AnalysisSettings settings,
            IPathClassifier filter,
            IIdentityRegistry identityRegistry)
        {
            _config = config;
            _settings = settings;
            _filter = filter;
            _identityRegistry = identityRegistry;
        }

        public void PrepareIdentityMerging(List<GitCommitRecord> commits)
        {
            foreach (var commit in commits)
            {
                _identityRegistry.RegisterRealIdentity(commit.Author);
                foreach (var co in commit.CoAuthors)
                {
                    _identityRegistry.RegisterRealIdentity(co);
                }
            }
        }

        public List<EmailCollision> GetEmailCollisions()
        {
            return _identityRegistry.GetEmailCollisions();
        }

        public void AddCommit(GitCommitRecord commit, List<string> filesInCommit)
        {
            foreach (var change in commit.Files)
            {
                string path = PathUtils.NormalizeGitPath(change.Path);
                if (!_filter.Check(path))
                {
                    continue;
                }

                _includedFileChangeCount += 1;
                string areaName = Exclusions.AreaForPath(path, _settings.Depth, _config.Areas, _warnings);
                var fileAccumulator = GetOrCreateItem(_files, path);
                var areaAccumulator = GetOrCreateItem(_areas, areaName);
                
                AddChangeToItem(fileAccumulator, path, change, commit);
                AddChangeToItem(areaAccumulator, path, change, commit);

                filesInCommit.Add(path);

                var participants = ParticipantsForCommit(commit);
                foreach (var participant in participants)
                {
                    if (_identityRegistry.IsBot(participant.Identity))
                    {
                        AddContributorActivity(_automation, participant.Identity, areaName, participant.Credit);
                        continue;
                    }

                    AddContributorActivity(
                        _contributors,
                        participant.Identity,
                        areaName,
                        participant.Credit
                    );
                    AddContributorCredit(fileAccumulator, participant.Identity, participant.Credit);
                    AddContributorCredit(areaAccumulator, participant.Identity, participant.Credit);
                }
            }
        }

        public Dictionary<string, ItemAccumulator> GetFiles()
        {
            return _files;
        }

        public Dictionary<string, ItemAccumulator> GetAreas()
        {
            return _areas;
        }

        public Dictionary<string, ContributorAccumulator> GetContributors()
        {
            return _contributors;
        }

        public Dictionary<string, ContributorAccumulator> GetAutomation()
        {
            return _automation;
        }

        public List<ExclusionSummary> GetExclusions()
        {
            return _filter.GetExclusions()
                .OrderBy(e => e.Category, StringComparer.Ordinal)
                .ToList();
        }

        public HashSet<string> GetWarnings()
        {
            return _warnings;
        }

        public int GetIncludedFileChangeCount()
        {
            return _includedFileChangeCount;
        }

        private ItemAccumulator GetOrCreateItem(Dictionary<string, ItemAccumulator> items, string key)
        {
            if (items.TryGetValue(key, out var existing))
            {
                return existing;
            }
            var created = new ItemAccumulator
            {
                Key = key,
                Touches = 0,
                Added = 0,
                Deleted = 0,
                Churn = 0,
                LastTouched = 0,
                Files = new HashSet<string>(),
                ContributorCredits = new Dictionary<string, ContributorCredit>(),
                Symbols = new Dictionary<string, int>(),
                BugFixTouches = 0,
                FeatureTouches = 0
            };
            items[key] = created;
            return created;
        }

        private void AddChangeToItem(
            ItemAccumulator item,
            string path,
            GitFileChange change,
            GitCommitRecord commit)
        {
            item.Touches += 1;
            item.Added += change.Added;
            item.Deleted += change.Deleted;
            item.Churn += change.Added + change.Deleted;
            item.Files.Add(path);

            if (commit.Timestamp > item.LastTouched)
            {
                item.LastTouched = commit.Timestamp;
            }

            string commitCategory = _classifier.Classify(commit.Message);
            if (commitCategory == "bugfix")
            {
                item.BugFixTouches += 1;
            }
            else if (commitCategory == "feature")
            {
                item.FeatureTouches += 1;
            }

            if (change.Symbols != null)
            {
                foreach (var symbol in change.Symbols)
                {
                    item.Symbols.TryGetValue(symbol, out int count);
                    item.Symbols[symbol] = count + 1;
                }
            }
        }

        private void AddContributorCredit(
            ItemAccumulator item,
            GitIdentity identity,
            double activity)
        {
            string key = IdentityUtils.IdentityKey(identity);
            if (!item.ContributorCredits.TryGetValue(key, out var current))
            {
                current = new ContributorCredit { Identity = identity, Activity = 0.0 };
                item.ContributorCredits[key] = current;
            }
            current.Activity = ScoringUtils.RoundActivity(current.Activity + activity);
        }

        private class ParticipantInfo
        {
            public GitIdentity Identity { get; set; } = new();
            public double Credit { get; set; }
        }

        private List<ParticipantInfo> ParticipantsForCommit(GitCommitRecord commit)
        {
            var author = _identityRegistry.Resolve(commit.Author);
            var coAuthors = commit.CoAuthors.Select(co => _identityRegistry.Resolve(co)).ToList();

            var identities = new List<GitIdentity> { author };
            identities.AddRange(coAuthors);

            var unique = new Dictionary<string, GitIdentity>();
            foreach (var identity in identities)
            {
                unique[IdentityUtils.IdentityKey(identity)] = identity;
            }

            double credit = ScoringUtils.RoundRatio(1.0 / unique.Count);
            return unique.Values.Select(identity => new ParticipantInfo { Identity = identity, Credit = credit }).ToList();
        }

        private void AddContributorActivity(
            Dictionary<string, ContributorAccumulator> items,
            GitIdentity identity,
            string areaName,
            double activity)
        {
            string key = IdentityUtils.IdentityKey(identity);
            if (!items.TryGetValue(key, out var accumulator))
            {
                accumulator = new ContributorAccumulator
                {
                    Identity = identity,
                    TotalActivity = 0.0,
                    Areas = new Dictionary<string, double>()
                };
                items[key] = accumulator;
            }
            accumulator.TotalActivity = ScoringUtils.RoundActivity(accumulator.TotalActivity + activity);
            
            accumulator.Areas.TryGetValue(areaName, out double currentAreaActivity);
            accumulator.Areas[areaName] = ScoringUtils.RoundActivity(currentAreaActivity + activity);
        }
    }
}
