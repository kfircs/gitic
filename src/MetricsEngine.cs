using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public interface IMetricsEngineCoordinator
    {
        TemporalCouplingEngine GetTemporalCouplingEngine();
        LeadTimeEngine GetLeadTimeEngine();
        void TrackCommit(List<string> filesInCommit);
        (List<TemporalCoupling> topCouplings, LeadTimesInfo leadTimes) Calculate(List<GitCommitRecord> commits);
    }

    public class TemporalCouplingEngine
    {
        private const int TemporalCouplingMinSharedCommits = 3;
        private const double TemporalCouplingMinCouplingDegree = 0.25;
        private const int TemporalCouplingMaxResults = 15;

        private readonly Dictionary<string, int> _fileCommitCount = new();
        private readonly Dictionary<string, int> _sharedCommitCounts = new();
        private readonly int _maxCommitFileCount;
        private int _oversizedCommitCount = 0;
        private int _maxObservedFiles = 0;

        public TemporalCouplingEngine(int maxCommitFileCount = 20)
        {
            _maxCommitFileCount = maxCommitFileCount;
        }

        public void TrackCommitFiles(List<string> filesInCommit)
        {
            if (filesInCommit.Count == 0)
            {
                return;
            }

            if (filesInCommit.Count > _maxObservedFiles)
            {
                _maxObservedFiles = filesInCommit.Count;
            }

            if (filesInCommit.Count > _maxCommitFileCount)
            {
                _oversizedCommitCount++;
                return;
            }

            foreach (var file in filesInCommit)
            {
                _fileCommitCount.TryGetValue(file, out int count);
                _fileCommitCount[file] = count + 1;
            }

            for (int i = 0; i < filesInCommit.Count; i++)
            {
                for (int j = i + 1; j < filesInCommit.Count; j++)
                {
                    string file1 = filesInCommit[i];
                    string file2 = filesInCommit[j];
                    string fileA = string.CompareOrdinal(file1, file2) < 0 ? file1 : file2;
                    string fileB = string.CompareOrdinal(file1, file2) < 0 ? file2 : file1;
                    string pairKey = $"{fileA}|{fileB}";

                    _sharedCommitCounts.TryGetValue(pairKey, out int shared);
                    _sharedCommitCounts[pairKey] = shared + 1;
                }
            }
        }

        public (int count, int maxObserved, int limit) GetOversizedCommitInfo()
        {
            return (_oversizedCommitCount, _maxObservedFiles, _maxCommitFileCount);
        }

        public List<TemporalCoupling> CalculateTemporalCoupling()
        {
            var temporalCouplings = new List<TemporalCoupling>();
            foreach (var kvp in _sharedCommitCounts)
            {
                string pairKey = kvp.Key;
                int sharedCommits = kvp.Value;

                if (sharedCommits < TemporalCouplingMinSharedCommits)
                {
                    continue;
                }

                var parts = pairKey.Split('|');
                string fileA = parts[0];
                string fileB = parts[1];

                _fileCommitCount.TryGetValue(fileA, out int totalA);
                _fileCommitCount.TryGetValue(fileB, out int totalB);

                if (totalA == 0 || totalB == 0)
                {
                    continue;
                }

                double couplingDegree = ScoringUtils.RoundRatio((double)sharedCommits / (totalA + totalB - sharedCommits));
                if (couplingDegree >= TemporalCouplingMinCouplingDegree)
                {
                    temporalCouplings.Add(new TemporalCoupling
                    {
                        FileA = fileA,
                        FileB = fileB,
                        SharedCommits = sharedCommits,
                        CouplingDegree = couplingDegree
                    });
                }
            }

            return temporalCouplings
                .OrderByDescending(tc => tc.CouplingDegree)
                .ThenByDescending(tc => tc.SharedCommits)
                .Take(TemporalCouplingMaxResults)
                .ToList();
        }
    }

    public class LeadTimeEngine
    {
        private const double MsPerHour = 3600000.0;
        private const int LeadTimeMainAncestorsMaxDepth = 150;
        private const int LeadTimeBranchCommitsMaxDepth = 100;
        private const double LeadTimeMinHours = 0.1;

        public LeadTimesInfo CalculateLeadTimes(List<GitCommitRecord> commits)
        {
            var commitMap = commits.ToDictionary(c => c.Hash, c => c);
            var merges = new List<MergeLeadTimeRecord>();

            foreach (var m in commits)
            {
                if (m.Parents != null && m.Parents.Count > 1)
                {
                    string p1 = m.Parents[0];
                    string p2 = m.Parents[1];

                    var mainAncestors = GitGraph.GetAncestors(p1, commitMap, LeadTimeMainAncestorsMaxDepth);
                    var branchCommits = GitGraph.GetBranchCommits(p2, mainAncestors, commitMap, LeadTimeBranchCommitsMaxDepth);

                    if (branchCommits.Count > 0)
                    {
                        var earliest = branchCommits.Aggregate(branchCommits[0], (oldest, curr) =>
                            curr.Timestamp < oldest.Timestamp ? curr : oldest);

                        double leadTimeMs = m.Timestamp - earliest.Timestamp;
                        double leadTimeHours = ScoringUtils.RoundRatio(Math.Max(LeadTimeMinHours, leadTimeMs / MsPerHour));

                        var filesSet = new HashSet<string>();
                        foreach (var bc in branchCommits)
                        {
                            foreach (var f in bc.Files)
                            {
                                filesSet.Add(f.Path);
                            }
                        }

                        merges.Add(new MergeLeadTimeRecord
                        {
                            Hash = m.Hash,
                            Message = m.Message.Split('\n')[0].Trim(),
                            Author = m.Author.Name,
                            Date = m.Date,
                            LeadTimeHours = leadTimeHours,
                            FileCount = filesSet.Count
                        });
                    }
                }
            }

            double averageLeadTimeHours = merges.Count > 0
                ? ScoringUtils.RoundRatio(merges.Sum(m => m.LeadTimeHours) / merges.Count)
                : 0.0;

            return new LeadTimesInfo
            {
                AverageLeadTimeHours = averageLeadTimeHours,
                Merges = merges
            };
        }
    }

    public class MetricsEngineCoordinator : IMetricsEngineCoordinator
    {
        private readonly TemporalCouplingEngine _temporalCouplingEngine;
        private readonly LeadTimeEngine _leadTimeEngine;

        public MetricsEngineCoordinator(int maxCommitFileCount = 20)
        {
            _temporalCouplingEngine = new TemporalCouplingEngine(maxCommitFileCount);
            _leadTimeEngine = new LeadTimeEngine();
        }

        public TemporalCouplingEngine GetTemporalCouplingEngine()
        {
            return _temporalCouplingEngine;
        }

        public LeadTimeEngine GetLeadTimeEngine()
        {
            return _leadTimeEngine;
        }

        public void TrackCommit(List<string> filesInCommit)
        {
            _temporalCouplingEngine.TrackCommitFiles(filesInCommit);
        }

        public (List<TemporalCoupling> topCouplings, LeadTimesInfo leadTimes) Calculate(List<GitCommitRecord> commits)
        {
            return (
                _temporalCouplingEngine.CalculateTemporalCoupling(),
                _leadTimeEngine.CalculateLeadTimes(commits)
            );
        }
    }
}
