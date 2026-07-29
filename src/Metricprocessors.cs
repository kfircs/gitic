using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public interface IMetricProcessorService
    {
        List<ContributorMetric> RenderContributors(List<ContributorAccumulator> items);
        List<AutomationMetric> RenderAutomation(List<ContributorAccumulator> items);
        List<FileMetric> SortFilesForCommand(List<FileMetric> files, AnalysisCommand command);
        List<AreaMetric> SortAreasForCommand(List<AreaMetric> areas, AnalysisCommand command);
        List<ContributorMetric> SortContributorsForCommand(List<ContributorMetric> contributors, AnalysisCommand command);
        void SortMetrics(AnalysisResult result, AnalysisCommand command);
        HashSet<string> GetActiveContributorKeys(List<GitCommitRecord> commits);
    }

    public class MetricProcessorService : IMetricProcessorService
    {
        private readonly IMetricsEngine _metricsEngine;

        public MetricProcessorService(IMetricsEngine? metricsEngine = null)
        {
            _metricsEngine = metricsEngine ?? new MetricsEngine();
        }

        public List<ContributorMetric> RenderContributors(List<ContributorAccumulator> items)
        {
            return _metricsEngine.RenderContributors(items);
        }

        public List<AutomationMetric> RenderAutomation(List<ContributorAccumulator> items)
        {
            return _metricsEngine.RenderAutomation(items);
        }

        public List<FileMetric> SortFilesForCommand(List<FileMetric> files, AnalysisCommand command)
        {
            return _metricsEngine.SortFilesForCommand(files, command);
        }

        public List<AreaMetric> SortAreasForCommand(List<AreaMetric> areas, AnalysisCommand command)
        {
            return _metricsEngine.SortAreasForCommand(areas, command);
        }

        public List<ContributorMetric> SortContributorsForCommand(List<ContributorMetric> contributors, AnalysisCommand command)
        {
            return _metricsEngine.SortContributorsForCommand(contributors, command);
        }

        public void SortMetrics(AnalysisResult result, AnalysisCommand command)
        {
            _metricsEngine.SortMetrics(result, command);
        }

        public HashSet<string> GetActiveContributorKeys(List<GitCommitRecord> commits)
        {
            return _metricsEngine.GetActiveContributorKeys(commits);
        }
    }
}
