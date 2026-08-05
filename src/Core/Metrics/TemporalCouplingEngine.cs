using System;
using System.Collections.Generic;

namespace Gitic;

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

    public TemporalCouplingResult CalculateTemporalCoupling(List<CommitFileSet> allIncludedCommits)
    {
        return _metricsEngine.CalculateTemporalCoupling(allIncludedCommits, _config);
    }
}
