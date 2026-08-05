using System;
using System.Collections.Generic;

namespace Gitic;

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
