using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
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

    public class AnalysisPipeline
    {
        public AnalysisPipelineResult Run(
            List<GitCommitRecord> commits,
            HashSet<string> headFiles,
            GitizerConfig config,
            AnalysisSettings settings,
            AnalysisCommand command,
            string repoRoot,
            ITemporalCouplingEngine? temporalCouplingEngine = null,
            ILeadTimeEngine? leadTimeEngine = null)
        {
            var gitignoreRules = PathClassifier.LoadGitignoreRules(repoRoot);
            config.Excludes.AddRange(gitignoreRules);

            var pathClassifier = new PathClassifier(headFiles, config.Excludes, settings.IncludeDeleted, settings.Path);
            bool mergeByEmail = (config.Identity?.MergeOnEmail == true) || (settings.MergeByEmail == true);
            var identityRegistry = new IdentityRegistry(config.Aliases, config.Bots, mergeByEmail);
            IChangeAccumulator accumulator = new ChangeAccumulator(config, settings, pathClassifier, identityRegistry);
            accumulator.PrepareIdentityMerging(commits);

            int temporalCouplingLimit = config.Metrics?.TemporalCouplingMaxCommitFileCount ?? 20;
            ITemporalCouplingEngine actualTemporalCouplingEngine = temporalCouplingEngine ?? new TemporalCouplingEngine(temporalCouplingLimit);
            ILeadTimeEngine actualLeadTimeEngine = leadTimeEngine ?? new LeadTimeEngine();

            foreach (var commit in commits)
            {
                var includedFilesInCommit = new List<string>();
                accumulator.AddCommit(commit, includedFilesInCommit);
                actualTemporalCouplingEngine.TrackCommitFiles(includedFilesInCommit);
            }

            var activeContributorKeys = MetricProcessors.GetActiveContributorKeys(commits);
            IFamiliarityScoringEngine scoringEngine = new FamiliarityScoringEngine(config, activeContributorKeys, settings.Depth);

            var rawFileMetrics = scoringEngine.ScoreFiles(accumulator.GetFiles().Values.ToList(), settings.Depth);
            var areaMetrics = scoringEngine.ScoreAreas(accumulator.GetAreas().Values.ToList());

            var contributorMetrics = MetricProcessors.RenderContributors(accumulator.GetContributors().Values.ToList());
            var automationMetrics = MetricProcessors.RenderAutomation(accumulator.GetAutomation().Values.ToList());

            var topCouplings = actualTemporalCouplingEngine.CalculateTemporalCoupling();
            var leadTimes = actualLeadTimeEngine.CalculateLeadTimes(commits);

            IWarningCollector warningCollector = new WarningCollector();
            var warnings = warningCollector.Collect(
                new WarningContext
                {
                    EmailCollisions = accumulator.GetEmailCollisions(),
                    AliasCount = config.Aliases.Count,
                    ConfiguredBotCount = config.Bots.Count,
                    AutomationMetrics = automationMetrics,
                    LeadTimes = leadTimes,
                    TemporalCouplingEngine = actualTemporalCouplingEngine,
                    Files = rawFileMetrics
                },
                accumulator.GetWarnings().ToList()
            );

            return new AnalysisPipelineResult
            {
                Files = rawFileMetrics,
                Areas = areaMetrics,
                Contributors = contributorMetrics,
                Automation = automationMetrics,
                TemporalCouplings = topCouplings,
                LeadTimes = leadTimes,
                Exclusions = accumulator.GetExclusions(),
                Warnings = warnings,
                IncludedFileChangeCount = accumulator.GetIncludedFileChangeCount()
            };
        }
    }
}