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
        public IFileStatsProvider? FileStatsProvider { get; set; } = null;
        public IGitClient? GitClient { get; set; } = null;
    }

    public interface IRepositoryAnalyzer
    {
        Task<AnalysisResult> AnalyzeAsync(AnalyzeInput input);
    }

    public class RepositoryAnalyzer : IRepositoryAnalyzer
    {
        private readonly IConfigurationEngine _configEngine;
        private readonly IAnalysisPipeline _pipeline;
        private readonly IMetricProcessorService _metricProcessorService;
        private readonly IResultAnonymizer _anonymizer;

        public RepositoryAnalyzer(
            IConfigurationEngine? configEngine = null,
            IAnalysisPipeline? pipeline = null,
            IMetricProcessorService? metricProcessorService = null,
            IResultAnonymizer? anonymizer = null)
        {
            _configEngine = configEngine ?? new ConfigurationEngine();
            _pipeline = pipeline ?? new AnalysisPipeline();
            _metricProcessorService = metricProcessorService ?? new MetricProcessorService();
            _anonymizer = anonymizer ?? new ResultAnonymizer();
        }

        public static async Task<AnalysisResult> AnalyzeRepositoryAsync(AnalyzeInput input)
        {
            IRepositoryAnalyzer analyzer = new RepositoryAnalyzer();
            return await analyzer.AnalyzeAsync(input);
        }

        public async Task<AnalysisResult> AnalyzeAsync(AnalyzeInput input)
        {
            var resolvedConfig = await _configEngine.LoadAndResolveAsync(input);
            var settings = resolvedConfig.Settings;
            var config = resolvedConfig.Config;
            var gitClient = input.GitClient ?? new GitClient(input.RepoRoot);

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

            var pipelineResult = _pipeline.Run(commits, headFiles, config, settings, input.Command);

            var provider = input.FileStatsProvider ?? new DiskFileStatsProvider();
            var fileMetrics = await provider.EnrichFileMetricsAsync(input.RepoRoot, pipelineResult.Files);

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
                    IncludedFileChangeCount = pipelineResult.IncludedFileChangeCount
                },
                Settings = settings,
                Exclusions = pipelineResult.Exclusions,
                Areas = _metricProcessorService.SortAreasForCommand(pipelineResult.Areas, input.Command),
                Files = _metricProcessorService.SortFilesForCommand(fileMetrics, input.Command),
                Contributors = _metricProcessorService.SortContributorsForCommand(pipelineResult.Contributors, input.Command),
                Automation = pipelineResult.Automation,
                TemporalCoupling = pipelineResult.TemporalCouplings,
                LeadTimes = pipelineResult.LeadTimes,
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
                Warnings = pipelineResult.Warnings
            };

            if (settings.Anonymize)
            {
                result = _anonymizer.Anonymize(result);
            }

            return result;
        }
    }
}
