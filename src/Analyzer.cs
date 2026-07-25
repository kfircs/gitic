using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gitic
{
    public class AnalyzeInput
    {
        public string RepoRoot { get; set; } = string.Empty;
        public AnalysisCommand Command { get; set; }
        public AnalysisSettings Settings { get; set; } = new();
        public GitizerConfig? Config { get; set; }
        public string? ContributorName { get; set; }
    }

    public static class RepositoryAnalyzer
    {
        public static AnalysisSettings NormalizeSettings(AnalysisSettings settings)
        {
            var defaults = DefaultAnalysisSettings.Create();
            return new AnalysisSettings
            {
                Json = settings.Json,
                AllTime = settings.AllTime,
                Since = settings.Since ?? defaults.Since,
                IncludeMerges = settings.IncludeMerges,
                IncludeDeleted = settings.IncludeDeleted,
                MergeByEmail = settings.MergeByEmail ?? defaults.MergeByEmail,
                Path = settings.Path ?? defaults.Path,
                Anonymize = settings.Anonymize,
                Depth = settings.Depth != 0 ? settings.Depth : defaults.Depth
            };
        }

        public static async Task<AnalysisResult> AnalyzeRepositoryAsync(AnalyzeInput input)
        {
            var settings = NormalizeSettings(input.Settings);
            var config = input.Config ?? GitizerConfig.Default;
            var gitClient = new GitClient(input.RepoRoot);

            var commitsTask = gitClient.ExtractHistoryAsync(new GitHistoryExtractorOptions
            {
                IncludeMerges = settings.IncludeMerges,
                AllTime = settings.AllTime,
                Since = settings.Since
            });
            var headFilesTask = gitClient.ListHeadFilesAsync();

            await Task.WhenAll(commitsTask, headFilesTask);

            var commits = await commitsTask;
            var headFiles = await headFilesTask;

            var gitignoreRules = PathClassifier.LoadGitignoreRules(input.RepoRoot);
            config.Excludes.AddRange(gitignoreRules);

            var accumulator = new ChangeAccumulator(config, settings, headFiles);
            accumulator.PrepareIdentityMerging(commits);

            int temporalCouplingLimit = config.Metrics?.TemporalCouplingMaxCommitFileCount ?? 20;
            var metricsEngine = new MetricsEngineCoordinator(temporalCouplingLimit);

            foreach (var commit in commits)
            {
                var includedFilesInCommit = new List<string>();
                accumulator.AddCommit(commit, includedFilesInCommit);
                metricsEngine.TrackCommit(includedFilesInCommit);
            }

            var activeContributorKeys = MetricProcessors.GetActiveContributorKeys(commits);
            var scoringEngine = new FamiliarityScoringEngine(config, activeContributorKeys, settings.Depth);

            var rawFileMetrics = scoringEngine.ScoreFiles(accumulator.GetFiles().Values.ToList(), settings.Depth);
            var filePaths = rawFileMetrics.Select(f => f.Path).ToList();

            var fileStats = await FileStats.ComputeFileStatsAsync(input.RepoRoot, filePaths);
            var fileMetrics = rawFileMetrics.Select(f =>
            {
                if (fileStats.TryGetValue(f.Path, out var stats))
                {
                    f.Size = stats.Size;
                    f.Width = stats.Width;
                    f.Lines = stats.Lines;
                }
                else
                {
                    f.Size = 0;
                    f.Width = 0;
                    f.Lines = 0;
                }
                return f;
            }).ToList();

            var areaMetrics = scoringEngine.ScoreAreas(accumulator.GetAreas().Values.ToList());

            var contributorMetrics = MetricProcessors.RenderContributors(accumulator.GetContributors().Values.ToList());
            var automationMetrics = MetricProcessors.RenderAutomation(accumulator.GetAutomation().Values.ToList());

            var (topCouplings, leadTimes) = metricsEngine.Calculate(commits);

            var result = new AnalysisResult
            {
                SchemaVersion = "1.0",
                Tool = "gitizer",
                Analysis = new AnalysisMetadata
                {
                    RepoRoot = input.RepoRoot,
                    Command = input.Command,
                    GeneratedAt = DateTime.UtcNow.ToString("o"),
                    CommitCount = commits.Count,
                    IncludedFileChangeCount = accumulator.GetIncludedFileChangeCount()
                },
                Settings = settings,
                Exclusions = accumulator.GetExclusions(),
                Areas = MetricProcessors.SortAreasForCommand(areaMetrics, input.Command),
                Files = MetricProcessors.SortFilesForCommand(fileMetrics, input.Command),
                Contributors = MetricProcessors.SortContributorsForCommand(contributorMetrics, input.Command),
                Automation = automationMetrics,
                TemporalCoupling = topCouplings,
                LeadTimes = leadTimes,
                Configuration = new AnalysisConfiguration
                {
                    Scoring = new ScoringConfiguration
                    {
                        Attention = new AttentionWeights
                        {
                            Churn = config.Scoring.Attention.Churn,
                            Recency = config.Scoring.Attention.Recency,
                            ContributorSpread = config.Scoring.Attention.ContributorSpread,
                            LowFamiliarityConcentration = config.Scoring.Attention.LowFamiliarityConcentration
                        }
                    },
                    ConfiguredAliasCount = config.Aliases.Count,
                    ConfiguredBotCount = config.Bots.Count,
                    ConfiguredExcludeCount = config.Excludes.Count,
                    ConfiguredAreaCount = config.Areas.Count,
                    Identity = new IdentityConfigInfo
                    {
                        MergeOnEmail = config.Identity.MergeOnEmail
                    }
                },
                Warnings = new WarningCollector().Collect(
                    new WarningContext
                    {
                        EmailCollisions = accumulator.GetEmailCollisions(),
                        AliasCount = config.Aliases.Count,
                        ConfiguredBotCount = config.Bots.Count,
                        AutomationMetrics = automationMetrics,
                        LeadTimes = leadTimes,
                        TemporalCouplingEngine = metricsEngine.GetTemporalCouplingEngine(),
                        Files = fileMetrics
                    },
                    accumulator.GetWarnings().ToList()
                )
            };

            return result;
        }
    }
}
