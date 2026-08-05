using System;
using System.Collections.Generic;

namespace Gitic;

public class LeadTimeConfig
{
    public int MainAncestorsMaxDepth { get; set; } = 150;
    public int BranchCommitsMaxDepth { get; set; } = 100;
    public double MinHours { get; set; } = 0.1;
}

public interface ILeadTimeEngine
{
    LeadTimesInfo CalculateLeadTimes(List<GitCommitRecord> commits);
}

public interface IMergeLeadTimeCalculator
{
    MergeLeadTimeRecord? CalculateMergeLeadTime(GitCommitRecord mergeCommit, Dictionary<string, GitCommitRecord> commitMap);
}
