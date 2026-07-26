using System;
using System.Linq;

namespace Gitic
{
    public interface IConfigMerger
    {
        GitizerConfig CloneDefaultConfig();
        GitizerConfigOverrides ConvertToOverrides(GitizerConfig config);
        GitizerConfig CloneConfig(GitizerConfig config);
        GitizerConfig MergeConfig(GitizerConfig baseConfig, GitizerConfigOverrides? overrideConfig = null);
    }

    public class ConfigMerger : IConfigMerger
    {
        public GitizerConfig CloneDefaultConfig()
        {
            return CloneConfig(GitizerConfig.Default);
        }

        public GitizerConfigOverrides ConvertToOverrides(GitizerConfig config)
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

        public GitizerConfig CloneConfig(GitizerConfig config)
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

        public GitizerConfig MergeConfig(GitizerConfig baseConfig, GitizerConfigOverrides? overrideConfig = null)
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
