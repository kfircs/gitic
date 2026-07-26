using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Gitic
{
    public class ResolvedConfiguration
    {
        public AnalysisSettings Settings { get; init; } = new();
        public GitizerConfig Config { get; init; } = GitizerConfig.Default;
    }

    public class ConfigurationEngine
    {
        private readonly ConfigValidator _validator;
        private readonly IYamlParser _yamlParser;
        private readonly AnalysisSettingsNormalizer _normalizer;

        public ConfigurationEngine(IYamlParser? yamlParser = null)
        {
            _validator = new ConfigValidator();
            _yamlParser = yamlParser ?? new YamlSubsetParserImpl();
            _normalizer = new AnalysisSettingsNormalizer();
        }

        public string RenderStarterConfig()
        {
            var attention = GitizerConfig.Default.Scoring.Attention;
            return
                "aliases: []\n" +
                "bots: []\n" +
                "excludes: []\n" +
                "areas: []\n" +
                "scoring:\n" +
                "  attention:\n" +
                $"    churn: {attention.Churn.ToString("F2")}\n" +
                $"    recency: {attention.Recency.ToString("F2")}\n" +
                $"    contributor_spread: {attention.ContributorSpread.ToString("F2")}\n" +
                $"    low_familiarity_concentration: {attention.LowFamiliarityConcentration.ToString("F2")}\n" +
                "identity:\n" +
                "  # merge_on_email: merge contributor identities sharing an email (case-insensitive).\n" +
                "  # Default false (raw by default, PRD US-16). Set true to collapse same-person identities.\n" +
                "  merge_on_email: false\n" +
                "metrics:\n" +
                "  # temporal_coupling_max_commit_file_count: max files a commit may touch before being\n" +
                "  # skipped for temporal coupling analysis. Commits above this limit trigger a warning.\n" +
                "  temporal_coupling_max_commit_file_count: 20\n";
        }

        public async Task<ResolvedConfiguration> LoadAndResolveAsync(AnalyzeInput input, LoadGitizerConfigOptions? options = null)
        {
            options ??= new LoadGitizerConfigOptions { RepoRoot = input.RepoRoot };
            
            var loadedConfig = await LoadGitizerConfigInternalAsync(options);
            var mergedConfig = input.Config != null 
                ? MergeConfig(loadedConfig.Config, ConvertToOverrides(input.Config)) 
                : loadedConfig.Config;

            var settings = _normalizer.Normalize(input.Settings ?? new AnalysisSettings());

            return new ResolvedConfiguration
            {
                Settings = settings,
                Config = mergedConfig
            };
        }

        private async Task<LoadedGitizerConfig> LoadGitizerConfigInternalAsync(LoadGitizerConfigOptions options)
        {
            string userHome = options.UserHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string userConfigPath = options.UserConfigPath ?? Path.Combine(userHome, ".config", "gitizer", "config.yml");
            string? repoConfigPath = options.RepoConfigPath ?? (options.RepoRoot == null ? null : Path.Combine(options.RepoRoot, ".gitizer.yml"));

            string? userConfigRaw = await ReadOptionalUtf8Async(userConfigPath);
            string? repoConfigRaw = repoConfigPath == null ? null : await ReadOptionalUtf8Async(repoConfigPath);

            GitizerConfigOverrides? userOverride = userConfigRaw == null
                ? null
                : ParseAndValidateOverride(userConfigRaw, $"user config ({userConfigPath})");

            GitizerConfigOverrides? repoOverride = repoConfigRaw == null
                ? null
                : ParseAndValidateOverride(repoConfigRaw, $"repo config ({repoConfigPath})");

            var merged = MergeConfig(
                MergeConfig(CloneDefaultConfig(), userOverride),
                repoOverride
            );

            _validator.ValidateAttentionWeights(merged.Scoring.Attention, "effective config");

            return new LoadedGitizerConfig
            {
                Config = merged,
                Sources = new ConfigSources
                {
                    User = userConfigRaw == null ? null : userConfigPath,
                    Repo = repoConfigRaw == null ? null : repoConfigPath
                }
            };
        }

        private static async Task<string?> ReadOptionalUtf8Async(string path)
        {
            try
            {
                return await File.ReadAllTextAsync(path);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        private GitizerConfigOverrides ParseAndValidateOverride(string content, string source)
        {
            var parsed = _yamlParser.Parse(content, source);
            return _validator.NormalizeOverride(parsed, source);
        }

        private GitizerConfig CloneDefaultConfig()
        {
            return CloneConfig(GitizerConfig.Default);
        }

        private GitizerConfigOverrides ConvertToOverrides(GitizerConfig config)
        {
            return new GitizerConfigOverrides
            {
                Aliases = config.Aliases,
                Bots = config.Bots,
                Excludes = config.Excludes,
                Areas = config.Areas,
                Scoring = new ScoringConfigOverrides
                {
                    Attention = new AttentionWeightsOverrides
                    {
                        Churn = config.Scoring.Attention.Churn,
                        Recency = config.Scoring.Attention.Recency,
                        ContributorSpread = config.Scoring.Attention.ContributorSpread,
                        LowFamiliarityConcentration = config.Scoring.Attention.LowFamiliarityConcentration
                    }
                },
                Identity = new IdentityConfigOverrides { MergeOnEmail = config.Identity.MergeOnEmail },
                Metrics = new MetricsConfigOverrides { TemporalCouplingMaxCommitFileCount = config.Metrics.TemporalCouplingMaxCommitFileCount }
            };
        }

        private GitizerConfig CloneConfig(GitizerConfig config)
        {
            return new GitizerConfig
            {
                Aliases = config.Aliases.Select(alias => new AliasRule
                {
                    Canonical = new GitIdentity { Name = alias.Canonical.Name, Email = alias.Canonical.Email },
                    Identities = alias.Identities.Select(id => new GitIdentity { Name = id.Name, Email = id.Email }).ToList()
                }).ToList(),
                Bots = config.Bots.Select(bot => new BotRule { Name = bot.Name, Email = bot.Email, Pattern = bot.Pattern }).ToList(),
                Excludes = config.Excludes.Select(ex => new ExcludeRule { Pattern = ex.Pattern, Category = ex.Category }).ToList(),
                Areas = config.Areas.Select(area => new NamedArea { Name = area.Name, Paths = area.Paths.ToList() }).ToList(),
                Scoring = new ScoringConfig
                {
                    Attention = new AttentionWeights
                    {
                        Churn = config.Scoring.Attention.Churn,
                        Recency = config.Scoring.Attention.Recency,
                        ContributorSpread = config.Scoring.Attention.ContributorSpread,
                        LowFamiliarityConcentration = config.Scoring.Attention.LowFamiliarityConcentration
                    }
                },
                Identity = new IdentityConfig { MergeOnEmail = config.Identity.MergeOnEmail },
                Metrics = new MetricsConfig { TemporalCouplingMaxCommitFileCount = config.Metrics.TemporalCouplingMaxCommitFileCount }
            };
        }

        private GitizerConfig MergeConfig(GitizerConfig baseConfig, GitizerConfigOverrides? overrideConfig = null)
        {
            var cloned = CloneConfig(baseConfig);
            if (overrideConfig == null)
            {
                return cloned;
            }

            if (overrideConfig.Aliases != null)
            {
                cloned.Aliases.AddRange(overrideConfig.Aliases);
            }
            if (overrideConfig.Bots != null)
            {
                cloned.Bots.AddRange(overrideConfig.Bots);
            }
            if (overrideConfig.Excludes != null)
            {
                cloned.Excludes.AddRange(overrideConfig.Excludes);
            }
            if (overrideConfig.Areas != null)
            {
                cloned.Areas.AddRange(overrideConfig.Areas);
            }

            if (overrideConfig.Scoring?.Attention != null)
            {
                if (overrideConfig.Scoring.Attention.Churn.HasValue)
                {
                    cloned.Scoring.Attention.Churn = overrideConfig.Scoring.Attention.Churn.Value;
                }
                if (overrideConfig.Scoring.Attention.Recency.HasValue)
                {
                    cloned.Scoring.Attention.Recency = overrideConfig.Scoring.Attention.Recency.Value;
                }
                if (overrideConfig.Scoring.Attention.ContributorSpread.HasValue)
                {
                    cloned.Scoring.Attention.ContributorSpread = overrideConfig.Scoring.Attention.ContributorSpread.Value;
                }
                if (overrideConfig.Scoring.Attention.LowFamiliarityConcentration.HasValue)
                {
                    cloned.Scoring.Attention.LowFamiliarityConcentration = overrideConfig.Scoring.Attention.LowFamiliarityConcentration.Value;
                }
            }

            if (overrideConfig.Identity?.MergeOnEmail.HasValue == true)
            {
                cloned.Identity.MergeOnEmail = overrideConfig.Identity.MergeOnEmail.Value;
            }

            if (overrideConfig.Metrics?.TemporalCouplingMaxCommitFileCount != null)
            {
                cloned.Metrics.TemporalCouplingMaxCommitFileCount = overrideConfig.Metrics.TemporalCouplingMaxCommitFileCount.Value;
            }

            return cloned;
        }
    }
}
