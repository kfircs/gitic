using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic;

/// <summary>
/// Defines the contract for merging and deep-cloning Gitic configurations.
/// </summary>
public interface IConfigMerger
{
    GiticConfig CloneDefaultConfig();
    GiticConfigOverrides ConvertToOverrides(GiticConfig config);
    GiticConfig CloneConfig(GiticConfig config);
    GiticConfig MergeConfig(GiticConfig baseConfig, GiticConfigOverrides? overrideConfig = null);

    // Deep Interface Overloads
    GiticConfig MergeConfig(GiticConfig baseConfig, GiticConfig? overrideConfig);
    GiticConfig MergeMultiple(GiticConfig baseConfig, params GiticConfigOverrides?[] overrides);
    GiticConfig MergeMultiple(GiticConfig baseConfig, params object?[] overridesOrConfigs);
}

public class ConfigMerger : IConfigMerger
{
    public GiticConfig CloneDefaultConfig() => CloneConfig(GiticConfig.Default);

    public GiticConfigOverrides ConvertToOverrides(GiticConfig config)
    {
        return ConvertToOverridesStatic(config);
    }

    public static GiticConfigOverrides ConvertToOverridesStatic(GiticConfig config)
    {
        if (config == null) return new GiticConfigOverrides();

        return new GiticConfigOverrides
        {
            Aliases = config.Aliases,
            Bots = config.Bots,
            Excludes = config.Excludes,
            Areas = config.Areas,
            Scoring = config.Scoring != null ? new ScoringConfigOverrides
            {
                Attention = config.Scoring.Attention != null ? new AttentionWeightsOverrides
                {
                    Churn = config.Scoring.Attention.Churn,
                    Recency = config.Scoring.Attention.Recency,
                    ContributorSpread = config.Scoring.Attention.ContributorSpread,
                    LowFamiliarityConcentration = config.Scoring.Attention.LowFamiliarityConcentration
                } : null
            } : null,
            Identity = config.Identity != null ? new IdentityConfigOverrides
            {
                MergeOnEmail = config.Identity.MergeOnEmail
            } : null,
            Metrics = config.Metrics != null ? new MetricsConfigOverrides
            {
                TemporalCouplingMaxCommitFileCount = config.Metrics.TemporalCouplingMaxCommitFileCount
            } : null
        };
    }

    public GiticConfig CloneConfig(GiticConfig config)
    {
        if (config == null) return new GiticConfig();
        return new GiticConfig
        {
            Aliases = config.Aliases?.Select(a => new AliasRule
            {
                Canonical = a.Canonical != null ? new GitIdentity { Name = a.Canonical.Name, Email = a.Canonical.Email } : new GitIdentity(),
                Identities = a.Identities?.Select(id => new GitIdentity { Name = id.Name, Email = id.Email }).ToList() ?? new List<GitIdentity>()
            }).ToList() ?? new List<AliasRule>(),

            Bots = config.Bots?.Select(b => new BotRule
            {
                Name = b.Name,
                Email = b.Email,
                Pattern = b.Pattern
            }).ToList() ?? new List<BotRule>(),

            Excludes = config.Excludes?.Select(e => new ExcludeRule
            {
                Pattern = e.Pattern,
                Category = e.Category
            }).ToList() ?? new List<ExcludeRule>(),

            Areas = config.Areas?.Select(area => new NamedArea
            {
                Name = area.Name,
                Paths = area.Paths?.ToList() ?? new List<string>()
            }).ToList() ?? new List<NamedArea>(),

            Scoring = config.Scoring != null ? new ScoringConfig
            {
                Attention = config.Scoring.Attention != null ? new AttentionWeights
                {
                    Churn = config.Scoring.Attention.Churn,
                    Recency = config.Scoring.Attention.Recency,
                    ContributorSpread = config.Scoring.Attention.ContributorSpread,
                    LowFamiliarityConcentration = config.Scoring.Attention.LowFamiliarityConcentration
                } : new AttentionWeights()
            } : new ScoringConfig(),

            Identity = config.Identity != null ? new IdentityConfig
            {
                MergeOnEmail = config.Identity.MergeOnEmail
            } : new IdentityConfig(),

            Metrics = config.Metrics != null ? new MetricsConfig
            {
                TemporalCouplingMaxCommitFileCount = config.Metrics.TemporalCouplingMaxCommitFileCount
            } : new MetricsConfig()
        };
    }

    public GiticConfig MergeConfig(GiticConfig baseConfig, GiticConfigOverrides? overrideConfig = null)
    {
        var cloned = CloneConfig(baseConfig);
        if (overrideConfig != null)
        {
            MergeConfigInPlace(cloned, overrideConfig);
        }
        return cloned;
    }

    // Deep Interface Implementation: Overload that accepts GiticConfig directly, hiding manual override mapping
    public GiticConfig MergeConfig(GiticConfig baseConfig, GiticConfig? overrideConfig)
    {
        if (overrideConfig == null) return CloneConfig(baseConfig);
        return MergeConfig(baseConfig, ConvertToOverridesStatic(overrideConfig));
    }

    // Deep Interface Implementation: Overload that handles chaining multiple typed overrides
    public GiticConfig MergeMultiple(GiticConfig baseConfig, params GiticConfigOverrides?[] overrides)
    {
        var cloned = CloneConfig(baseConfig);
        if (overrides == null) return cloned;

        foreach (var overrideConfig in overrides)
        {
            if (overrideConfig != null)
            {
                MergeConfigInPlace(cloned, overrideConfig);
            }
        }
        return cloned;
    }

    // Deep Interface Implementation: Overload that handles both GiticConfig and GiticConfigOverrides polymorphically
    public GiticConfig MergeMultiple(GiticConfig baseConfig, params object?[] overridesOrConfigs)
    {
        var cloned = CloneConfig(baseConfig);
        if (overridesOrConfigs == null) return cloned;

        foreach (var item in overridesOrConfigs)
        {
            if (item == null) continue;

            if (item is GiticConfig configItem)
            {
                MergeConfigInPlace(cloned, ConvertToOverridesStatic(configItem));
            }
            else if (item is GiticConfigOverrides overridesItem)
            {
                MergeConfigInPlace(cloned, overridesItem);
            }
        }
        return cloned;
    }

    public static void MergeConfigInPlace(GiticConfig target, GiticConfigOverrides source)
    {
        if (target == null || source == null) return;

        if (source.Aliases != null)
        {
            target.Aliases ??= new List<AliasRule>();
            foreach (var item in source.Aliases)
            {
                target.Aliases.Add(item);
            }
        }
        if (source.Bots != null)
        {
            target.Bots ??= new List<BotRule>();
            foreach (var item in source.Bots)
            {
                target.Bots.Add(item);
            }
        }
        if (source.Excludes != null)
        {
            target.Excludes ??= new List<ExcludeRule>();
            foreach (var item in source.Excludes)
            {
                target.Excludes.Add(item);
            }
        }
        if (source.Areas != null)
        {
            target.Areas ??= new List<NamedArea>();
            foreach (var item in source.Areas)
            {
                target.Areas.Add(item);
            }
        }

        if (source.Scoring != null)
        {
            target.Scoring ??= new ScoringConfig();
            if (source.Scoring.Attention != null)
            {
                target.Scoring.Attention ??= new AttentionWeights();
                var sAttention = source.Scoring.Attention;
                var tAttention = target.Scoring.Attention;
                if (sAttention.Churn.HasValue) tAttention.Churn = sAttention.Churn.Value;
                if (sAttention.Recency.HasValue) tAttention.Recency = sAttention.Recency.Value;
                if (sAttention.ContributorSpread.HasValue) tAttention.ContributorSpread = sAttention.ContributorSpread.Value;
                if (sAttention.LowFamiliarityConcentration.HasValue) tAttention.LowFamiliarityConcentration = sAttention.LowFamiliarityConcentration.Value;
            }
        }

        if (source.Identity != null)
        {
            target.Identity ??= new IdentityConfig();
            if (source.Identity.MergeOnEmail.HasValue)
            {
                target.Identity.MergeOnEmail = source.Identity.MergeOnEmail.Value;
            }
        }

        if (source.Metrics != null)
        {
            target.Metrics ??= new MetricsConfig();
            if (source.Metrics.TemporalCouplingMaxCommitFileCount.HasValue)
            {
                target.Metrics.TemporalCouplingMaxCommitFileCount = source.Metrics.TemporalCouplingMaxCommitFileCount.Value;
            }
        }
    }
}
