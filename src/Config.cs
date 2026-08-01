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

    public class GiticConfig
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

        public static GiticConfig Default => new()
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

    public class GiticConfigOverrides
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

    public class LoadedGiticConfig
    {
        public GiticConfig Config { get; set; } = new();
        public ConfigSources Sources { get; set; } = new();
    }

    public class ConfigSources
    {
        public string? User { get; set; }
        public string? Repo { get; set; }
    }

    public class LoadGiticConfigOptions
    {
        public string? RepoRoot { get; set; }
        public string? UserHome { get; set; }
        public string? UserConfigPath { get; set; }
        public string? RepoConfigPath { get; set; }
    }

    public static class ConfigUtils
    {
        public static double ConvertToDouble(object? val) => val switch
        {
            null => 0.0,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            string s when double.TryParse(s, out var d) => d,
            _ => Convert.ToDouble(val)
        };
    }
}
