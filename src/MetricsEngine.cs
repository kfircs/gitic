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

    public interface IMergeLeadTimeCalculator
    {
        MergeLeadTimeRecord? CalculateMergeLeadTime(GitCommitRecord mergeCommit, Dictionary<string, GitCommitRecord> commitMap);
    }

    public class TemporalCouplingConfig
    {
        public int MinSharedCommits { get; set; } = 3;
        public double MinCouplingDegree { get; set; } = 0.25;
        public int MaxResults { get; set; } = 15;
        public int MaxCommitFileCount { get; set; } = 20;
    }

    public class LeadTimeConfig
    {
        public int MainAncestorsMaxDepth { get; set; } = 150;
        public int BranchCommitsMaxDepth { get; set; } = 100;
        public double MinHours { get; set; } = 0.1;
    }

    public class MetricsCalculationRequest
    {
        public List<GitCommitRecord> Commits { get; set; } = new();
        public List<List<string>> AllIncludedCommits { get; set; } = new();
        public List<ContributorAccumulator> ContributorAccumulators { get; set; } = new();
        public List<ContributorAccumulator> AutomationAccumulators { get; set; } = new();
        public TemporalCouplingConfig? TemporalCouplingConfig { get; set; }
        public LeadTimeConfig? LeadTimeConfig { get; set; }
    }

    public class MetricsResult
    {
        public List<ContributorMetric> Contributors { get; set; } = new();
        public List<AutomationMetric> Automation { get; set; } = new();
        public TemporalCouplingResult TemporalCoupling { get; set; } = new();
        public LeadTimesInfo LeadTimes { get; set; } = new();
        public HashSet<string> ActiveContributorKeys { get; set; } = new();
    }

    public interface IMetricsEngine
    {
        MetricsResult CalculateAll(MetricsCalculationRequest request);
        TemporalCouplingResult CalculateTemporalCoupling(List<List<string>> allIncludedCommits, TemporalCouplingConfig? config = null);
        LeadTimesInfo CalculateLeadTimes(List<GitCommitRecord> commits, LeadTimeConfig? config = null);
        List<ContributorMetric> RenderContributors(List<ContributorAccumulator> items);
        List<AutomationMetric> RenderAutomation(List<ContributorAccumulator> items);
        List<FileMetric> SortFilesForCommand(List<FileMetric> files, AnalysisCommand command);
        List<AreaMetric> SortAreasForCommand(List<AreaMetric> areas, AnalysisCommand command);
        List<ContributorMetric> SortContributorsForCommand(List<ContributorMetric> contributors, AnalysisCommand command);
        void SortMetrics(AnalysisResult result, AnalysisCommand command);
        HashSet<string> GetActiveContributorKeys(List<GitCommitRecord> commits);
    }

    public class TemporalCouplingEngine : ITemporalCouplingEngine
    {
        private readonly IMetricsEngine _metricsEngine;
        private readonly TemporalCouplingConfig _config;

        public TemporalCouplingEngine(int maxCommitFileCount = 20)
        {
            _config = new TemporalCouplingConfig { MaxCommitFileCount = maxCommitFileCount };
            _metricsEngine = new MetricsEngine();
        }

        public TemporalCouplingEngine(TemporalCouplingConfig config, IMetricsEngine? metricsEngine = null)
        {
            _config = config ?? new TemporalCouplingConfig();
            _metricsEngine = metricsEngine ?? new MetricsEngine();
        }

        public TemporalCouplingResult CalculateTemporalCoupling(List<List<string>> allIncludedCommits)
        {
            return _metricsEngine.CalculateTemporalCoupling(allIncludedCommits, _config);
        }
    }

    public class MergeLeadTimeCalculator : IMergeLeadTimeCalculator
    {
        private const double MsPerHour = 3600000.0;

        private readonly IGitGraph _gitGraph;
        private readonly LeadTimeConfig _config;

        public MergeLeadTimeCalculator(IGitGraph? gitGraph = null)
        {
            _gitGraph = gitGraph ?? new GitGraphCalculator();
            _config = new LeadTimeConfig();
        }

        public MergeLeadTimeCalculator(IGitGraph? gitGraph, LeadTimeConfig? config)
        {
            _gitGraph = gitGraph ?? new GitGraphCalculator();
            _config = config ?? new LeadTimeConfig();
        }

        public MergeLeadTimeRecord? CalculateMergeLeadTime(GitCommitRecord m, Dictionary<string, GitCommitRecord> commitMap)
        {
            if (m.Parents == null || m.Parents.Count <= 1)
            {
                return null;
            }

            string p1 = m.Parents[0];
            string p2 = m.Parents[1];

            var mainAncestors = _gitGraph.GetAncestors(p1, commitMap, _config.MainAncestorsMaxDepth);
            var branchCommits = _gitGraph.GetBranchCommits(p2, mainAncestors, commitMap, _config.BranchCommitsMaxDepth);

            if (branchCommits.Count > 0)
            {
                var earliest = branchCommits.Aggregate(branchCommits[0], (oldest, curr) =>
                    curr.Timestamp < oldest.Timestamp ? curr : oldest);

                double leadTimeMs = m.Timestamp - earliest.Timestamp;
                double leadTimeHours = ScoringUtils.RoundRatio(Math.Max(_config.MinHours, leadTimeMs / MsPerHour));

                var filesSet = new HashSet<string>();
                foreach (var bc in branchCommits)
                {
                    foreach (var f in bc.Files)
                    {
                        filesSet.Add(f.Path);
                    }
                }

                return new MergeLeadTimeRecord
                {
                    Hash = m.Hash,
                    Message = m.Message.Split('\n')[0].Trim(),
                    Author = m.Author.Name,
                    Date = m.Date,
                    LeadTimeHours = leadTimeHours,
                    FileCount = filesSet.Count
                };
            }

            return null;
        }
    }

    public class LeadTimeEngine : ILeadTimeEngine
    {
        private readonly IMetricsEngine _metricsEngine;
        private readonly LeadTimeConfig _config;

        public LeadTimeEngine(IMergeLeadTimeCalculator? calculator = null)
        {
            _config = new LeadTimeConfig();
            _metricsEngine = new MetricsEngine(calculator);
        }

        public LeadTimeEngine(LeadTimeConfig config, IMetricsEngine? metricsEngine = null)
        {
            _config = config ?? new LeadTimeConfig();
            _metricsEngine = metricsEngine ?? new MetricsEngine();
        }

        public LeadTimesInfo CalculateLeadTimes(List<GitCommitRecord> commits)
        {
            return _metricsEngine.CalculateLeadTimes(commits, _config);
        }
    }

    public class MetricsEngine : IMetricsEngine
    {
        private readonly IMergeLeadTimeCalculator _calculator;

        public MetricsEngine(IMergeLeadTimeCalculator? calculator = null)
        {
            _calculator = calculator ?? new MergeLeadTimeCalculator();
        }

        private const long MsPerDay = 86400000L;
        private const long ActiveContributorDays = 90L;
        private const long ActiveContributorLookbackMs = ActiveContributorDays * MsPerDay;
        private const double TopQuarterFraction = 0.25;

        private static readonly Dictionary<string, Func<List<AreaMetric>, List<AreaMetric>>> AreaSorters =
            new Dictionary<string, Func<List<AreaMetric>, List<AreaMetric>>>
            {
                { "attention", list => list.OrderByDescending(a => a.AttentionScore).ToList() },
                { "heat", list => list.OrderByDescending(a => a.HeatScore).ToList() },
                { "churn", list => list.OrderByDescending(a => a.Churn).ToList() },
                { "contributors", list => list.OrderByDescending(a => a.ContributorCount).ToList() }
            };

        private static readonly Dictionary<string, Func<List<FileMetric>, List<FileMetric>>> FileSorters =
            new Dictionary<string, Func<List<FileMetric>, List<FileMetric>>>
            {
                { "attention", list => list.OrderByDescending(f => f.AttentionScore).ToList() },
                { "heat", list => list.OrderByDescending(f => f.HeatScore).ToList() },
                { "churn", list => list.OrderByDescending(f => f.Churn).ToList() },
                { "rework", list => list.OrderByDescending(f => f.ReworkRate ?? 0).ToList() },
                { "coordination", list => list.OrderByDescending(f => f.CoordinationOverlap ?? 0).ToList() },
                { "lines", list => list.OrderByDescending(f => f.Lines ?? 0).ToList() }
            };

        private static readonly Func<List<TemporalCoupling>, List<TemporalCoupling>> DefaultTemporalCouplingSorter =
            list => list
                .OrderByDescending(tc => tc.CouplingDegree)
                .ThenByDescending(tc => tc.SharedCommits)
                .ThenBy(tc => tc.FileA)
                .ToList();

        private static readonly Dictionary<string, Func<List<TemporalCoupling>, List<TemporalCoupling>>> TemporalCouplingSorters =
            new Dictionary<string, Func<List<TemporalCoupling>, List<TemporalCoupling>>>
            {
                { "coupling", DefaultTemporalCouplingSorter },
                { "coupling_degree", DefaultTemporalCouplingSorter },
                { "degree", DefaultTemporalCouplingSorter },
                { "shared", list => list.OrderByDescending(tc => tc.SharedCommits).ThenByDescending(tc => tc.CouplingDegree).ThenBy(tc => tc.FileA).ToList() },
                { "shared_commits", list => list.OrderByDescending(tc => tc.SharedCommits).ThenByDescending(tc => tc.CouplingDegree).ThenBy(tc => tc.FileA).ToList() },
                { "commits", list => list.OrderByDescending(tc => tc.SharedCommits).ThenByDescending(tc => tc.CouplingDegree).ThenBy(tc => tc.FileA).ToList() },
                { "file", list => list.OrderBy(tc => tc.FileA).ThenBy(tc => tc.FileB).ToList() },
                { "filea", list => list.OrderBy(tc => tc.FileA).ThenBy(tc => tc.FileB).ToList() },
                { "file_a", list => list.OrderBy(tc => tc.FileA).ThenBy(tc => tc.FileB).ToList() }
            };

        private static readonly Func<List<MergeLeadTimeRecord>, List<MergeLeadTimeRecord>> DefaultLeadTimeSorter =
            list => list
                .OrderByDescending(m => m.LeadTimeHours)
                .ThenByDescending(m => m.Date)
                .ToList();

        private static readonly Dictionary<string, Func<List<MergeLeadTimeRecord>, List<MergeLeadTimeRecord>>> LeadTimeSorters =
            new Dictionary<string, Func<List<MergeLeadTimeRecord>, List<MergeLeadTimeRecord>>>
            {
                { "lead_time", DefaultLeadTimeSorter },
                { "leadtime", DefaultLeadTimeSorter },
                { "time", DefaultLeadTimeSorter },
                { "hours", DefaultLeadTimeSorter },
                { "date", list => list.OrderByDescending(m => m.Date).ThenByDescending(m => m.LeadTimeHours).ToList() },
                { "time_stamp", list => list.OrderByDescending(m => m.Date).ThenByDescending(m => m.LeadTimeHours).ToList() },
                { "files", list => list.OrderByDescending(m => m.FileCount).ThenByDescending(m => m.LeadTimeHours).ToList() },
                { "file_count", list => list.OrderByDescending(m => m.FileCount).ThenByDescending(m => m.LeadTimeHours).ToList() },
                { "count", list => list.OrderByDescending(m => m.FileCount).ThenByDescending(m => m.LeadTimeHours).ToList() },
                { "author", list => list.OrderBy(m => m.Author).ThenByDescending(m => m.LeadTimeHours).ToList() }
            };

        private readonly MetricSorter<AreaMetric> _areaSorter = new(AreaSorters, (list, cmd) => list.OrderByDescending(a => a.HeatScore).ToList());
        private readonly MetricSorter<FileMetric> _fileSorter = new(FileSorters, (list, cmd) => list.OrderByDescending(f => f.Lines ?? 0).ThenByDescending(f => f.Size ?? 0).ThenByDescending(f => f.AttentionScore).ToList());
        private readonly MetricSorter<TemporalCoupling> _temporalCouplingSorter = new(TemporalCouplingSorters, (list, cmd) => DefaultTemporalCouplingSorter(list));
        private readonly MetricSorter<MergeLeadTimeRecord> _leadTimeSorter = new(LeadTimeSorters, (list, cmd) => DefaultLeadTimeSorter(list));

        public MetricsResult CalculateAll(MetricsCalculationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var result = new MetricsResult();
            result.ActiveContributorKeys = GetActiveContributorKeys(request.Commits);
            result.Contributors = RenderContributors(request.ContributorAccumulators);
            result.Automation = RenderAutomation(request.AutomationAccumulators);
            result.TemporalCoupling = CalculateTemporalCoupling(request.AllIncludedCommits, request.TemporalCouplingConfig);
            result.LeadTimes = CalculateLeadTimes(request.Commits, request.LeadTimeConfig);

            return result;
        }

        public TemporalCouplingResult CalculateTemporalCoupling(List<List<string>> allIncludedCommits, TemporalCouplingConfig? config = null)
        {
            var cfg = config ?? new TemporalCouplingConfig();
            var fileCommitCount = new Dictionary<string, int>();
            var sharedCommitCounts = new Dictionary<string, int>();
            int oversizedCommitCount = 0;
            int maxObservedFiles = 0;

            foreach (var filePaths in allIncludedCommits)
            {
                if (filePaths.Count == 0) continue;

                if (filePaths.Count > maxObservedFiles)
                {
                    maxObservedFiles = filePaths.Count;
                }

                if (filePaths.Count > cfg.MaxCommitFileCount)
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

                if (sharedCommits < cfg.MinSharedCommits) continue;

                var parts = pairKey.Split('|');
                string fileA = parts[0];
                string fileB = parts[1];

                fileCommitCount.TryGetValue(fileA, out int totalA);
                fileCommitCount.TryGetValue(fileB, out int totalB);

                if (totalA == 0 || totalB == 0) continue;

                double couplingDegree = ScoringUtils.RoundRatio((double)sharedCommits / (totalA + totalB - sharedCommits));
                if (couplingDegree >= cfg.MinCouplingDegree)
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
                .Take(cfg.MaxResults)
                .ToList();

            return new TemporalCouplingResult
            {
                Couplings = couplings,
                OversizedCommitCount = oversizedCommitCount,
                MaxObservedFiles = maxObservedFiles,
                Limit = cfg.MaxCommitFileCount
            };
        }

        public LeadTimesInfo CalculateLeadTimes(List<GitCommitRecord> commits, LeadTimeConfig? config = null)
        {
            var cfg = config ?? new LeadTimeConfig();
            var commitMap = commits.ToDictionary(c => c.Hash, c => c);
            var merges = new List<MergeLeadTimeRecord>();

            foreach (var m in commits)
            {
                var record = _calculator.CalculateMergeLeadTime(m, commitMap);
                if (record != null)
                {
                    merges.Add(record);
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

        public List<ContributorMetric> RenderContributors(List<ContributorAccumulator> items)
        {
            return RenderContributorAccumulatorList(items);
        }

        public List<AutomationMetric> RenderAutomation(List<ContributorAccumulator> items)
        {
            return RenderContributorAccumulatorList(items)
                .Select(m => new AutomationMetric
                {
                    Name = m.Name,
                    Email = m.Email,
                    TotalActivity = m.TotalActivity,
                    Areas = m.Areas
                }).ToList();
        }

        private List<ContributorMetric> RenderContributorAccumulatorList(List<ContributorAccumulator> items)
        {
            return items
                .Select(item => new ContributorMetric
                {
                    Name = item.Identity.Name,
                    Email = item.Identity.Email,
                    TotalActivity = ScoringUtils.RoundRatio(item.TotalActivity),
                    Areas = ContributorAreas(item)
                })
                .OrderByDescending(c => c.TotalActivity)
                .ToList();
        }

        private List<ContributorAreaMetric> ContributorAreas(ContributorAccumulator item)
        {
            return item.Areas
                .Select(kv =>
                {
                    string area = kv.Key;
                    double activity = kv.Value;
                    double total = item.TotalActivity;
                    double share = total > 0 ? activity / total : 0.0;
                    return new ContributorAreaMetric
                    {
                        Area = area,
                        Activity = ScoringUtils.RoundRatio(activity),
                        ActivityShare = ScoringUtils.RoundRatio(share),
                        FamiliarityScore = ScoringUtils.RoundRatio(Math.Sqrt(share) * 100.0)
                    };
                })
                .OrderByDescending(c => c.Activity)
                .ToList();
        }

        public List<FileMetric> SortFilesForCommand(List<FileMetric> files, AnalysisCommand command)
        {
            if (command == AnalysisCommand.Areas)
            {
                return files.OrderByDescending(f => f.HeatScore).ToList();
            }
            return files
                .OrderByDescending(f => f.Lines ?? 0)
                .ThenByDescending(f => f.Size ?? 0)
                .ThenByDescending(f => f.AttentionScore)
                .ToList();
        }

        public List<AreaMetric> SortAreasForCommand(List<AreaMetric> areas, AnalysisCommand command)
        {
            if (command == AnalysisCommand.Hotspots)
            {
                return areas.OrderByDescending(a => a.AttentionScore).ToList();
            }
            return areas.OrderByDescending(a => a.HeatScore).ToList();
        }

        public List<ContributorMetric> SortContributorsForCommand(
            List<ContributorMetric> contributors,
            AnalysisCommand command)
        {
            return contributors.OrderByDescending(c => c.TotalActivity).ToList();
        }

        public void SortMetrics(AnalysisResult result, AnalysisCommand command)
        {
            if (result == null) return;

            string? sortKey = result.Settings?.Sort;

            if (command == AnalysisCommand.Areas)
            {
                result.Areas = _areaSorter.Sort(result.Areas, sortKey, command);
            }
            else
            {
                result.Areas = SortAreasForCommand(result.Areas, command);
            }

            if (command == AnalysisCommand.Hotspots)
            {
                result.Files = _fileSorter.Sort(result.Files, sortKey, command);
            }
            else
            {
                result.Files = SortFilesForCommand(result.Files, command);
            }

            result.Contributors = SortContributorsForCommand(result.Contributors, command);

            if (command == AnalysisCommand.TemporalCoupling && result.TemporalCoupling != null)
            {
                result.TemporalCoupling = _temporalCouplingSorter.Sort(result.TemporalCoupling, sortKey, command);
            }

            if (command == AnalysisCommand.LeadTime && result.LeadTimes != null && result.LeadTimes.Merges != null)
            {
                result.LeadTimes.Merges = _leadTimeSorter.Sort(result.LeadTimes.Merges, sortKey, command);
            }
        }

        public HashSet<string> GetActiveContributorKeys(List<GitCommitRecord> commits)
        {
            var activeKeys = new HashSet<string>();
            if (commits.Count == 0)
            {
                return activeKeys;
            }

            long maxTimestamp = commits.Max(c => c.Timestamp);
            long ninetyDaysAgo = maxTimestamp - ActiveContributorLookbackMs;

            var sortedCommits = commits.OrderByDescending(c => c.Timestamp).ToList();
            int topQuarterCount = Math.Max(1, (int)Math.Floor(commits.Count * TopQuarterFraction));

            for (int i = 0; i < sortedCommits.Count; i++)
            {
                var commit = sortedCommits[i];
                if (i < topQuarterCount || commit.Timestamp >= ninetyDaysAgo)
                {
                    activeKeys.Add(IdentityUtils.IdentityKey(commit.Author));
                    foreach (var co in commit.CoAuthors)
                    {
                        activeKeys.Add(IdentityUtils.IdentityKey(co));
                    }
                }
            }
            return activeKeys;
        }
    }

    internal class MetricSorter<T>
    {
        private readonly Dictionary<string, Func<List<T>, List<T>>> _sorters;
        private readonly Func<List<T>, AnalysisCommand, List<T>> _defaultSorter;

        public MetricSorter(
            Dictionary<string, Func<List<T>, List<T>>> sorters,
            Func<List<T>, AnalysisCommand, List<T>> defaultSorter)
        {
            _sorters = sorters;
            _defaultSorter = defaultSorter;
        }

        public List<T> Sort(List<T>? list, string? sortKey, AnalysisCommand command)
        {
            if (list == null) return new List<T>();
            if (!string.IsNullOrEmpty(sortKey) && _sorters.TryGetValue(sortKey.ToLower(), out var sorter))
            {
                return sorter(list);
            }
            return _defaultSorter(list, command);
        }
    }
}
