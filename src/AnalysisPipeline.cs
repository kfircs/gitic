using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public interface IAnalysisPipeline
    {
        AnalysisPipelineResult Run(
            List<GitCommitRecord> commits,
            HashSet<string> headFiles,
            GitizerConfig config,
            AnalysisSettings settings,
            AnalysisCommand command);
    }

    public class AnalysisPipelineResult
    {
        public List<FileMetric> Files { get; set; } = new();
        public List<AreaMetric> Areas { get; set; } = new();
        public List<ContributorMetric> Contributors { get; set; } = new();
        public List<AutomationMetric> Automation { get; set; } = new();
        public List<TemporalCoupling> TemporalCouplings { get; set; } = new();
        public LeadTimesInfo LeadTimes { get; set; } = new();
        public List<ExclusionSummary> Exclusions { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public int IncludedFileChangeCount { get; set; }
    }

    public class AnalysisPipeline : IAnalysisPipeline
    {
        private readonly ITemporalCouplingEngine? _temporalCouplingEngine;
        private readonly ILeadTimeEngine _leadTimeEngine;
        private readonly IMetricProcessorService _metricProcessorService;
        private readonly IFamiliarityScoringEngine? _scoringEngine;
        private readonly IWarningCollector _warningCollector;
        private readonly IIdentityRegistry? _identityRegistry;
        private readonly IChangeAccumulator? _accumulator;

        public AnalysisPipeline(
            ITemporalCouplingEngine? temporalCouplingEngine = null,
            ILeadTimeEngine? leadTimeEngine = null,
            IMetricProcessorService? metricProcessorService = null,
            IFamiliarityScoringEngine? scoringEngine = null,
            IWarningCollector? warningCollector = null,
            IIdentityRegistry? identityRegistry = null,
            IChangeAccumulator? accumulator = null)
        {
            _temporalCouplingEngine = temporalCouplingEngine;
            _leadTimeEngine = leadTimeEngine ?? new LeadTimeEngine();
            _metricProcessorService = metricProcessorService ?? new MetricProcessorService();
            _scoringEngine = scoringEngine;
            _warningCollector = warningCollector ?? new WarningCollector();
            _identityRegistry = identityRegistry;
            _accumulator = accumulator;
        }

        public AnalysisPipelineResult Run(
            List<GitCommitRecord> commits,
            HashSet<string> headFiles,
            GitizerConfig config,
            AnalysisSettings settings,
            AnalysisCommand command)
        {
            var pathClassifier = new PathClassifier(headFiles, config.Excludes, settings.IncludeDeleted, settings.Path);
            bool mergeByEmail = (config.Identity?.MergeOnEmail == true) || (settings.MergeByEmail == true);
            var actualIdentityRegistry = _identityRegistry ?? new IdentityRegistry(config.Aliases, config.Bots, mergeByEmail);
            IChangeAccumulator actualAccumulator = _accumulator ?? new ChangeAccumulator(config, settings, pathClassifier, actualIdentityRegistry);
            actualAccumulator.PrepareIdentityMerging(commits);

            int temporalCouplingLimit = config.Metrics?.TemporalCouplingMaxCommitFileCount ?? 20;
            ITemporalCouplingEngine actualTemporalCouplingEngine = _temporalCouplingEngine ?? new TemporalCouplingEngine(temporalCouplingLimit);
            ILeadTimeEngine actualLeadTimeEngine = _leadTimeEngine;
            IMetricProcessorService actualMetricProcessorService = _metricProcessorService;

            var allIncludedCommits = new List<List<string>>();
            foreach (var commit in commits)
            {
                var includedFilesInCommit = new List<string>();
                actualAccumulator.AddCommit(commit, includedFilesInCommit);
                allIncludedCommits.Add(includedFilesInCommit);
            }

            var activeContributorKeys = actualMetricProcessorService.GetActiveContributorKeys(commits);
            IFamiliarityScoringEngine actualScoringEngine = _scoringEngine ?? new FamiliarityScoringEngine(config, activeContributorKeys, settings.Depth);

            var rawFileMetrics = actualScoringEngine.ScoreFiles(actualAccumulator.GetFiles().Values.ToList(), settings.Depth);
            var areaMetrics = actualScoringEngine.ScoreAreas(actualAccumulator.GetAreas().Values.ToList());

            var contributorMetrics = actualMetricProcessorService.RenderContributors(actualAccumulator.GetContributors().Values.ToList());
            var automationMetrics = actualMetricProcessorService.RenderAutomation(actualAccumulator.GetAutomation().Values.ToList());

            var couplingResult = actualTemporalCouplingEngine.CalculateTemporalCoupling(allIncludedCommits);
            var leadTimes = actualLeadTimeEngine.CalculateLeadTimes(commits);

            IWarningCollector actualWarningCollector = _warningCollector;
            var warnings = actualWarningCollector.Collect(
                new WarningContext
                {
                    EmailCollisions = actualAccumulator.GetEmailCollisions(),
                    AliasCount = config.Aliases.Count,
                    ConfiguredBotCount = config.Bots.Count,
                    AutomationMetrics = automationMetrics,
                    LeadTimes = leadTimes,
                    TemporalCoupling = couplingResult,
                    Files = rawFileMetrics
                },
                actualAccumulator.GetWarnings().ToList()
            );

            return new AnalysisPipelineResult
            {
                Files = rawFileMetrics,
                Areas = areaMetrics,
                Contributors = contributorMetrics,
                Automation = automationMetrics,
                TemporalCouplings = couplingResult.Couplings,
                LeadTimes = leadTimes,
                Exclusions = actualAccumulator.GetExclusions(),
                Warnings = warnings,
                IncludedFileChangeCount = actualAccumulator.GetIncludedFileChangeCount()
            };
        }
    }
}