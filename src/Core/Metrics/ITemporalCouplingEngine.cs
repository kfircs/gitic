using System;
using System.Collections.Generic;

namespace Gitic;

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

public interface ITemporalCouplingEngine
{
    TemporalCouplingResult CalculateTemporalCoupling(List<CommitFileSet> allIncludedCommits);
}
