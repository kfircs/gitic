using System;
using System.Collections.Generic;

namespace Gitic;

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
