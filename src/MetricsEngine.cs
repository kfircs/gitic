using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public class TemporalCouplingResult
    {
        public List<TemporalCoupling> Couplings { get; set; } = new();
        public int OversizedCommitCount { get; set; }
        public int MaxObservedFiles { get; set; }
        public int Limit { get; set; }
    }

    public interface ITemporalCouplingEngine
    {
        TemporalCouplingResult CalculateTemporalCoupling(List<List<string>> allIncludedCommits);
    }

    public interface ILeadTimeEngine
    {
        LeadTimesInfo CalculateLeadTimes(List<GitCommitRecord> commits);
    }

    public class TemporalCouplingEngine : ITemporalCouplingEngine
    {
        private const int TemporalCouplingMinSharedCommits = 3;
        private const double TemporalCouplingMinCouplingDegree = 0.25;
        private const int TemporalCouplingMaxResults = 15;

        private readonly int _maxCommitFileCount;

        public TemporalCouplingEngine(int maxCommitFileCount = 20)
        {
            _maxCommitFileCount = maxCommitFileCount;
        }

        public TemporalCouplingResult CalculateTemporalCoupling(List<List<string>> allIncludedCommits)
        {
            var fileCommitCount = new Dictionary<string, int>();
            var sharedCommitCounts = new Dictionary<string, int>();
            int oversizedCommitCount = 0;
            int maxObservedFiles = 0;

            foreach (var filePaths in allIncludedCommits)
            {
                if (filePaths.Count == 0)
                {
                    continue;
                }

                if (filePaths.Count > maxObservedFiles)
                {
                    maxObservedFiles = filePaths.Count;
                }

                if (filePaths.Count > _maxCommitFileCount)
                {
                    oversizedCommitCount++;
                    continue;
                }

                foreach (var file in filePaths)
                {
                    fileCommitCount.TryGetValue(file, out int count);
                    fileCommitCount[file] = count + 1;
                }

                for (int i = 0; i < filePaths.Count; i++)
                {
                    for (int j = i + 1; j < filePaths.Count; j++)
                    {
                        string file1 = filePaths[i];
                        string file2 = filePaths[j];
                        string fileA = string.CompareOrdinal(file1, file2) < 0 ? file1 : file2;
                        string fileB = string.CompareOrdinal(file1, file2) < 0 ? file2 : file1;
                        string pairKey = $"{fileA}|{fileB}";

                        sharedCommitCounts.TryGetValue(pairKey, out int shared);
                        sharedCommitCounts[pairKey] = shared + 1;
                    }
                }
            }

            var temporalCouplings = new List<TemporalCoupling>();
            foreach (var kvp in sharedCommitCounts)
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

                fileCommitCount.TryGetValue(fileA, out int totalA);
                fileCommitCount.TryGetValue(fileB, out int totalB);

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

            var couplings = temporalCouplings
                .OrderByDescending(tc => tc.CouplingDegree)
                .ThenByDescending(tc => tc.SharedCommits)
                .Take(TemporalCouplingMaxResults)
                .ToList();

            return new TemporalCouplingResult
            {
                Couplings = couplings,
                OversizedCommitCount = oversizedCommitCount,
                MaxObservedFiles = maxObservedFiles,
                Limit = _maxCommitFileCount
            };
        }
    }

    public class LeadTimeEngine : ILeadTimeEngine
    {
        private const double MsPerHour = 3600000.0;
        private const int LeadTimeMainAncestorsMaxDepth = 150;
        private const int LeadTimeBranchCommitsMaxDepth = 100;
        private const double LeadTimeMinHours = 0.1;

        private readonly IGitGraph _gitGraph;

        public LeadTimeEngine(IGitGraph? gitGraph = null)
        {
            _gitGraph = gitGraph ?? new GitGraphCalculator();
        }

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

                    var mainAncestors = _gitGraph.GetAncestors(p1, commitMap, LeadTimeMainAncestorsMaxDepth);
                    var branchCommits = _gitGraph.GetBranchCommits(p2, mainAncestors, commitMap, LeadTimeBranchCommitsMaxDepth);

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
}
