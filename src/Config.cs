using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Gitic
{
    public class AliasRule
    {
        [JsonPropertyName("canonical")]
        public GitIdentity Canonical { get; set; } = new();

        [JsonPropertyName("identities")]
        public List<GitIdentity> Identities { get; set; } = new();
    }

    public class BotRule
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("pattern")]
        public string? Pattern { get; set; }
    }

    public class ExcludeRule
    {
        [JsonPropertyName("pattern")]
        public string Pattern { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;
    }

    public class NamedArea
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("paths")]
        public List<string> Paths { get; set; } = new();
    }

    public class AttentionWeights
    {
        [JsonPropertyName("churn")]
        public double Churn { get; set; } = DefaultAttentionWeights.Churn;

        [JsonPropertyName("recency")]
        public double Recency { get; set; } = DefaultAttentionWeights.Recency;

        [JsonPropertyName("contributor_spread")]
        public double ContributorSpread { get; set; } = DefaultAttentionWeights.ContributorSpread;

        [JsonPropertyName("low_familiarity_concentration")]
        public double LowFamiliarityConcentration { get; set; } = DefaultAttentionWeights.LowFamiliarityConcentration;
    }

    public class IdentityConfig
    {
        [JsonPropertyName("merge_on_email")]
        public bool MergeOnEmail { get; set; } = false;
    }

    public class MetricsConfig
    {
        [JsonPropertyName("temporal_coupling_max_commit_file_count")]
        public int TemporalCouplingMaxCommitFileCount { get; set; } = 20;
    }

    public class ScoringConfig
    {
        [JsonPropertyName("attention")]
        public AttentionWeights Attention { get; set; } = new();
    }

    public class GitizerConfig
    {
        [JsonPropertyName("aliases")]
        public List<AliasRule> Aliases { get; set; } = new();

        [JsonPropertyName("bots")]
        public List<BotRule> Bots { get; set; } = new();

        [JsonPropertyName("excludes")]
        public List<ExcludeRule> Excludes { get; set; } = new();

        [JsonPropertyName("areas")]
        public List<NamedArea> Areas { get; set; } = new();

        [JsonPropertyName("scoring")]
        public ScoringConfig Scoring { get; set; } = new();

        [JsonPropertyName("identity")]
        public IdentityConfig Identity { get; set; } = new();

        [JsonPropertyName("metrics")]
        public MetricsConfig Metrics { get; set; } = new();

        public static GitizerConfig Default => new()
        {
            Aliases = new(),
            Bots = new(),
            Excludes = new(),
            Areas = new(),
            Scoring = new()
            {
                Attention = new()
                {
                    Churn = DefaultAttentionWeights.Churn,
                    Recency = DefaultAttentionWeights.Recency,
                    ContributorSpread = DefaultAttentionWeights.ContributorSpread,
                    LowFamiliarityConcentration = DefaultAttentionWeights.LowFamiliarityConcentration
                }
            },
            Identity = new()
            {
                MergeOnEmail = false
            },
            Metrics = new()
            {
                TemporalCouplingMaxCommitFileCount = 20
            }
        };
    }

    public class GitizerConfigOverrides
    {
        public List<AliasRule>? Aliases { get; set; }
        public List<BotRule>? Bots { get; set; }
        public List<ExcludeRule>? Excludes { get; set; }
        public List<NamedArea>? Areas { get; set; }
        public ScoringConfigOverrides? Scoring { get; set; }
        public IdentityConfigOverrides? Identity { get; set; }
        public MetricsConfigOverrides? Metrics { get; set; }
    }

    public class ScoringConfigOverrides
    {
        public AttentionWeightsOverrides? Attention { get; set; }
    }

    public class AttentionWeightsOverrides
    {
        public double? Churn { get; set; }
        public double? Recency { get; set; }
        public double? ContributorSpread { get; set; }
        public double? LowFamiliarityConcentration { get; set; }
    }

    public class IdentityConfigOverrides
    {
        public bool? MergeOnEmail { get; set; }
    }

    public class MetricsConfigOverrides
    {
        public int? TemporalCouplingMaxCommitFileCount { get; set; }
    }

    public class LoadedGitizerConfig
    {
        public GitizerConfig Config { get; set; } = new();
        public ConfigSources Sources { get; set; } = new();
    }

    public class ConfigSources
    {
        public string? User { get; set; }
        public string? Repo { get; set; }
    }

    public class LoadGitizerConfigOptions
    {
        public string? RepoRoot { get; set; }
        public string? UserHome { get; set; }
        public string? UserConfigPath { get; set; }
        public string? RepoConfigPath { get; set; }
    }

    public static class ConfigLoader
    {
        private static readonly IConfigValidator _validator = new ConfigValidator();

        public static string RenderStarterConfig()
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

        public static async Task<LoadedGitizerConfig> LoadGitizerConfigAsync(LoadGitizerConfigOptions? options = null)
        {
            options ??= new LoadGitizerConfigOptions();

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

        public static GitizerConfig ApplyConfigOverrides(
            GitizerConfig baseConfig,
            GitizerConfigOverrides overrides)
        {
            var merged = MergeConfig(baseConfig, overrides);
            _validator.ValidateAttentionWeights(merged.Scoring.Attention, "effective config");
            return merged;
        }

        public static GitizerConfig CloneConfig(GitizerConfig config)
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

        private static GitizerConfig CloneDefaultConfig()
        {
            return CloneConfig(GitizerConfig.Default);
        }

        public static GitizerConfig MergeConfig(
            GitizerConfig baseConfig,
            GitizerConfigOverrides? overrideConfig = null)
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

            if (overrideConfig.Identity != null && overrideConfig.Identity.MergeOnEmail.HasValue && overrideConfig.Identity.MergeOnEmail.Value)
            {
                cloned.Identity.MergeOnEmail = true;
            }

            if (overrideConfig.Metrics?.TemporalCouplingMaxCommitFileCount != null)
            {
                cloned.Metrics.TemporalCouplingMaxCommitFileCount = overrideConfig.Metrics.TemporalCouplingMaxCommitFileCount.Value;
            }

            return cloned;
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

        private static GitizerConfigOverrides ParseAndValidateOverride(string content, string source)
        {
            var parsed = YamlSubsetParserHelper.ParseYamlSubset(content, source);
            return _validator.NormalizeOverride(parsed, source);
        }
    }
}
