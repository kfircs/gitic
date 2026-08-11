using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic;

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

    public TemporalCouplingResult CalculateTemporalCoupling(List<CommitFileSet> allIncludedCommits, TemporalCouplingConfig? config = null)
    {
        var cfg = config ?? new TemporalCouplingConfig();
        var fileCommitCount = new Dictionary<string, int>();
        var sharedCommitCounts = new Dictionary<string, int>();

        PopulateCommitAndSharedCounts(
            allIncludedCommits,
            cfg,
            fileCommitCount,
            sharedCommitCounts,
            out int oversizedCommitCount,
            out int maxObservedFiles);

        var couplings = BuildTemporalCouplings(sharedCommitCounts, fileCommitCount, cfg);

        return new TemporalCouplingResult
        {
            Couplings = couplings,
            OversizedCommitCount = oversizedCommitCount,
            MaxObservedFiles = maxObservedFiles,
            Limit = cfg.MaxCommitFileCount
        };
    }

    private void PopulateCommitAndSharedCounts(
        List<CommitFileSet> allIncludedCommits,
        TemporalCouplingConfig cfg,
        Dictionary<string, int> fileCommitCount,
        Dictionary<string, int> sharedCommitCounts,
        out int oversizedCommitCount,
        out int maxObservedFiles)
    {
        oversizedCommitCount = 0;
        maxObservedFiles = 0;

        foreach (var commit in allIncludedCommits)
        {
            var filePaths = commit.Files;
            if (filePaths == null || filePaths.Count == 0) continue;

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
                    string pairKey = GetFilePairKey(filePaths[i], filePaths[j]);

                    sharedCommitCounts.TryGetValue(pairKey, out int shared);
                    sharedCommitCounts[pairKey] = shared + 1;
                }
            }
        }
    }

    private string GetFilePairKey(string file1, string file2)
    {
        string fileA = string.CompareOrdinal(file1, file2) < 0 ? file1 : file2;
        string fileB = string.CompareOrdinal(file1, file2) < 0 ? file2 : file1;
        return $"{fileA}|{fileB}";
    }

    private List<TemporalCoupling> BuildTemporalCouplings(
        Dictionary<string, int> sharedCommitCounts,
        Dictionary<string, int> fileCommitCount,
        TemporalCouplingConfig cfg)
    {
        var couplings = new List<TemporalCoupling>();
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
                couplings.Add(new TemporalCoupling
                {
                    FileA = fileA,
                    FileB = fileB,
                    SharedCommits = sharedCommits,
                    CouplingDegree = couplingDegree
                });
            }
        }

        return couplings
            .OrderByDescending(tc => tc.CouplingDegree)
            .ThenByDescending(tc => tc.SharedCommits)
            .Take(cfg.MaxResults)
            .ToList();
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
        if (command == AnalysisCommand.Areas)
        {
            return areas.OrderByDescending(a => a.HeatScore).ToList();
        }
        return areas.OrderByDescending(a => a.AttentionScore).ToList();
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

        var activeCommits = sortedCommits.Where((commit, index) => index < topQuarterCount || commit.Timestamp >= ninetyDaysAgo);

        foreach (var commit in activeCommits)
        {
            activeKeys.Add(IdentityUtils.IdentityKey(commit.Author));
            foreach (var co in commit.CoAuthors)
            {
                activeKeys.Add(IdentityUtils.IdentityKey(co));
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
