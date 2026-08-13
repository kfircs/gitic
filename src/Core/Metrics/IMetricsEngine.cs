using System;
using System.Collections.Generic;

namespace Gitic;

public class LeadTimeConfig
{
    public int MainAncestorsMaxDepth { get; set; } = 150;
    public int BranchCommitsMaxDepth { get; set; } = 100;
    public double MinHours { get; set; } = 0.1;
}

public interface IMergeLeadTimeCalculator
{
    MergeLeadTimeRecord? CalculateMergeLeadTime(GitCommitRecord mergeCommit, IGitCommitGraph commitGraph);
}

public class TemporalCouplingResult
{
    public List<TemporalCoupling> Couplings { get; set; } = new();
    public int OversizedCommitCount { get; set; }
    public int MaxObservedFiles { get; set; }
    public int Limit { get; set; }
}

public class TemporalCouplingConfig
{
    public int MinSharedCommits { get; set; } = 3;
    public double MinCouplingDegree { get; set; } = 0.25;
    public int MaxResults { get; set; } = 15;
    public int MaxCommitFileCount { get; set; } = 20;
}

public class MetricsCalculationRequest
{
    public List<GitCommitRecord> Commits { get; set; } = new();
    public List<CommitFileSet> AllIncludedCommits { get; set; } = new();
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
    TemporalCouplingResult CalculateTemporalCoupling(List<CommitFileSet> allIncludedCommits, TemporalCouplingConfig? config = null);
    LeadTimesInfo CalculateLeadTimes(List<GitCommitRecord> commits, LeadTimeConfig? config = null);
    List<ContributorMetric> RenderContributors(List<ContributorAccumulator> items);
    List<AutomationMetric> RenderAutomation(List<ContributorAccumulator> items);
    List<FileMetric> SortFilesForCommand(List<FileMetric> files, AnalysisCommand command);
    List<AreaMetric> SortAreasForCommand(List<AreaMetric> areas, AnalysisCommand command);
    List<ContributorMetric> SortContributorsForCommand(List<ContributorMetric> contributors, AnalysisCommand command);
    void SortMetrics(AnalysisResult result, AnalysisCommand command);
    HashSet<string> GetActiveContributorKeys(List<GitCommitRecord> commits);
}
