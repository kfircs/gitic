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
        public IAnalysisSettingsNormalizer? SettingsNormalizer { get; set; } = null;
    }

    public interface IRepositoryAnalyzer
    {
        Task<AnalysisResult> AnalyzeAsync(AnalyzeInput input);
    }

    public class RepositoryAnalyzer : IRepositoryAnalyzer
    {
        [Obsolete("Use IAnalysisSettingsNormalizer instead.")]
        public static AnalysisSettings NormalizeSettings(AnalysisSettings settings)
        {
            return new AnalysisSettingsNormalizer().Normalize(settings);
        }

        public static async Task<AnalysisResult> AnalyzeRepositoryAsync(AnalyzeInput input)
        {
            IRepositoryAnalyzer analyzer = new RepositoryAnalyzer();
            return await analyzer.AnalyzeAsync(input);
        }

        public async Task<AnalysisResult> AnalyzeAsync(AnalyzeInput input)
        {
            var normalizer = input.SettingsNormalizer ?? new AnalysisSettingsNormalizer();
            var settings = normalizer.Normalize(input.Settings);
            var config = input.Config ?? GitizerConfig.Default;
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

            var pipeline = new AnalysisPipeline();
            var pipelineResult = pipeline.Run(commits, headFiles, config, settings, input.Command, input.RepoRoot);

            var filePaths = pipelineResult.Files.Select(f => f.Path).ToList();

            var provider = input.FileStatsProvider ?? new DiskFileStatsProvider();
            var fileStats = await provider.ComputeFileStatsAsync(input.RepoRoot, filePaths);
            var fileMetrics = pipelineResult.Files.Select(f =>
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
                Areas = MetricProcessors.SortAreasForCommand(pipelineResult.Areas, input.Command),
                Files = MetricProcessors.SortFilesForCommand(fileMetrics, input.Command),
                Contributors = MetricProcessors.SortContributorsForCommand(pipelineResult.Contributors, input.Command),
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

            return result;
        }
    }
}
