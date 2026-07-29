using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Gitic.Tests
{
    public class ConfigMergerRefactoredTests
    {
        private readonly ConfigMerger _merger = new();

        [Fact]
        public void Test_CloneDefaultConfig_ReturnsValidInstance()
        {
            var config = _merger.CloneDefaultConfig();
            Assert.NotNull(config);
            Assert.NotNull(config.Aliases);
            Assert.NotNull(config.Scoring?.Attention);
            Assert.NotNull(config.Identity);
            Assert.NotNull(config.Metrics);
        }

        [Fact]
        public void Test_CloneConfig_CreatesIndependentDeepCopy()
        {
            var original = new GiticConfig
            {
                Aliases = new List<AliasRule>
                {
                    new AliasRule
                    {
                        Canonical = new GitIdentity { Name = "Canonical", Email = "canonical@example.com" },
                        Identities = new List<GitIdentity>
                        {
                            new GitIdentity { Name = "Alias 1", Email = "alias1@example.com" }
                        }
                    }
                },
                Bots = new List<BotRule>
                {
                    new BotRule { Name = "bot", Email = "bot@example.com", Pattern = ".*-bot" }
                },
                Excludes = new List<ExcludeRule>
                {
                    new ExcludeRule { Pattern = "bin/", Category = "binary" }
                },
                Areas = new List<NamedArea>
                {
                    new NamedArea { Name = "Src", Paths = new List<string> { "src/" } }
                },
                Scoring = new ScoringConfig
                {
                    Attention = new AttentionWeights
                    {
                        Churn = 2.5,
                        Recency = 1.8,
                        ContributorSpread = 3.2,
                        LowFamiliarityConcentration = 4.1
                    }
                },
                Identity = new IdentityConfig { MergeOnEmail = true },
                Metrics = new MetricsConfig { TemporalCouplingMaxCommitFileCount = 42 }
            };

            var clone = _merger.CloneConfig(original);

            // Verify they are identical in content
            Assert.Single(clone.Aliases);
            Assert.Equal("Canonical", clone.Aliases[0].Canonical.Name);
            Assert.Equal("canonical@example.com", clone.Aliases[0].Canonical.Email);
            Assert.Single(clone.Aliases[0].Identities);
            Assert.Equal("Alias 1", clone.Aliases[0].Identities[0].Name);

            Assert.Single(clone.Bots);
            Assert.Equal("bot", clone.Bots[0].Name);

            Assert.Single(clone.Excludes);
            Assert.Equal("bin/", clone.Excludes[0].Pattern);

            Assert.Single(clone.Areas);
            Assert.Equal("Src", clone.Areas[0].Name);
            Assert.Equal("src/", clone.Areas[0].Paths[0]);

            Assert.Equal(2.5, clone.Scoring.Attention.Churn);
            Assert.Equal(1.8, clone.Scoring.Attention.Recency);
            Assert.Equal(3.2, clone.Scoring.Attention.ContributorSpread);
            Assert.Equal(4.1, clone.Scoring.Attention.LowFamiliarityConcentration);

            Assert.True(clone.Identity.MergeOnEmail);
            Assert.Equal(42, clone.Metrics.TemporalCouplingMaxCommitFileCount);

            // Verify deep copy (modifying clone doesn't affect original)
            clone.Aliases[0].Canonical.Name = "Modified Canonical";
            clone.Aliases[0].Identities.Add(new GitIdentity { Name = "New Alias", Email = "new@example.com" });
            clone.Bots[0].Name = "Modified Bot";
            clone.Excludes[0].Pattern = "obj/";
            clone.Areas[0].Paths.Add("tests/");
            clone.Scoring.Attention.Churn = 9.9;
            clone.Identity.MergeOnEmail = false;
            clone.Metrics.TemporalCouplingMaxCommitFileCount = 100;

            Assert.Equal("Canonical", original.Aliases[0].Canonical.Name);
            Assert.Single(original.Aliases[0].Identities);
            Assert.Equal("bot", original.Bots[0].Name);
            Assert.Equal("bin/", original.Excludes[0].Pattern);
            Assert.Single(original.Areas[0].Paths);
            Assert.Equal(2.5, original.Scoring.Attention.Churn);
            Assert.True(original.Identity.MergeOnEmail);
            Assert.Equal(42, original.Metrics.TemporalCouplingMaxCommitFileCount);
        }

        [Fact]
        public void Test_ConvertToOverrides_MapsAllPropertiesCorrectly()
        {
            var config = new GiticConfig
            {
                Aliases = new List<AliasRule>
                {
                    new AliasRule { Canonical = new GitIdentity { Name = "C", Email = "c@example.com" } }
                },
                Bots = new List<BotRule> { new BotRule { Name = "B" } },
                Excludes = new List<ExcludeRule> { new ExcludeRule { Pattern = "P" } },
                Areas = new List<NamedArea> { new NamedArea { Name = "A" } },
                Scoring = new ScoringConfig
                {
                    Attention = new AttentionWeights
                    {
                        Churn = 1.1,
                        Recency = 2.2,
                        ContributorSpread = 3.3,
                        LowFamiliarityConcentration = 4.4
                    }
                },
                Identity = new IdentityConfig { MergeOnEmail = true },
                Metrics = new MetricsConfig { TemporalCouplingMaxCommitFileCount = 50 }
            };

            var overrides = _merger.ConvertToOverrides(config);

            Assert.NotNull(overrides);
            Assert.Same(config.Aliases, overrides.Aliases);
            Assert.Same(config.Bots, overrides.Bots);
            Assert.Same(config.Excludes, overrides.Excludes);
            Assert.Same(config.Areas, overrides.Areas);

            Assert.NotNull(overrides.Scoring?.Attention);
            Assert.Equal(1.1, overrides.Scoring.Attention.Churn);
            Assert.Equal(2.2, overrides.Scoring.Attention.Recency);
            Assert.Equal(3.3, overrides.Scoring.Attention.ContributorSpread);
            Assert.Equal(4.4, overrides.Scoring.Attention.LowFamiliarityConcentration);

            Assert.NotNull(overrides.Identity);
            Assert.True(overrides.Identity.MergeOnEmail);

            Assert.NotNull(overrides.Metrics);
            Assert.Equal(50, overrides.Metrics.TemporalCouplingMaxCommitFileCount);
        }

        [Fact]
        public void Test_MergeConfig_AppendsListsAndOverridesValues()
        {
            var baseConfig = new GiticConfig
            {
                Aliases = new List<AliasRule>
                {
                    new AliasRule { Canonical = new GitIdentity { Name = "Base C" } }
                },
                Bots = new List<BotRule> { new BotRule { Name = "Base B" } },
                Excludes = new List<ExcludeRule> { new ExcludeRule { Pattern = "Base P" } },
                Areas = new List<NamedArea> { new NamedArea { Name = "Base A" } },
                Scoring = new ScoringConfig
                {
                    Attention = new AttentionWeights
                    {
                        Churn = 1.0,
                        Recency = 1.0,
                        ContributorSpread = 1.0,
                        LowFamiliarityConcentration = 1.0
                    }
                },
                Identity = new IdentityConfig { MergeOnEmail = false },
                Metrics = new MetricsConfig { TemporalCouplingMaxCommitFileCount = 10 }
            };

            var overrides = new GiticConfigOverrides
            {
                Aliases = new List<AliasRule>
                {
                    new AliasRule { Canonical = new GitIdentity { Name = "Override C" } }
                },
                Bots = new List<BotRule> { new BotRule { Name = "Override B" } },
                Excludes = new List<ExcludeRule> { new ExcludeRule { Pattern = "Override P" } },
                Areas = new List<NamedArea> { new NamedArea { Name = "Override A" } },
                Scoring = new ScoringConfigOverrides
                {
                    Attention = new AttentionWeightsOverrides
                    {
                        Churn = 5.0,
                        // Recency is left null (should keep base value of 1.0)
                        ContributorSpread = 7.0
                        // LowFamiliarityConcentration is left null
                    }
                },
                Identity = new IdentityConfigOverrides { MergeOnEmail = true },
                Metrics = new MetricsConfigOverrides { TemporalCouplingMaxCommitFileCount = 99 }
            };

            var merged = _merger.MergeConfig(baseConfig, overrides);

            // Lists should be appended (base + overrides)
            Assert.Equal(2, merged.Aliases.Count);
            Assert.Equal("Base C", merged.Aliases[0].Canonical.Name);
            Assert.Equal("Override C", merged.Aliases[1].Canonical.Name);

            Assert.Equal(2, merged.Bots.Count);
            Assert.Equal("Base B", merged.Bots[0].Name);
            Assert.Equal("Override B", merged.Bots[1].Name);

            Assert.Equal(2, merged.Excludes.Count);
            Assert.Equal("Base P", merged.Excludes[0].Pattern);
            Assert.Equal("Override P", merged.Excludes[1].Pattern);

            Assert.Equal(2, merged.Areas.Count);
            Assert.Equal("Base A", merged.Areas[0].Name);
            Assert.Equal("Override A", merged.Areas[1].Name);

            // Primitive values should be overridden if provided
            Assert.Equal(5.0, merged.Scoring.Attention.Churn);
            Assert.Equal(1.0, merged.Scoring.Attention.Recency); // Unchanged
            Assert.Equal(7.0, merged.Scoring.Attention.ContributorSpread);
            Assert.Equal(1.0, merged.Scoring.Attention.LowFamiliarityConcentration); // Unchanged

            Assert.True(merged.Identity.MergeOnEmail);
            Assert.Equal(99, merged.Metrics.TemporalCouplingMaxCommitFileCount);
        }

        [Fact]
        public void Test_MergeConfig_WithNullOverrides_ReturnsDeepClone()
        {
            var baseConfig = new GiticConfig
            {
                Identity = new IdentityConfig { MergeOnEmail = true }
            };

            var merger = new ConfigMerger();
            var result = merger.MergeConfig(baseConfig, (GiticConfigOverrides?)null);

            Assert.NotSame(baseConfig, result);
            Assert.True(result.Identity.MergeOnEmail);
        }
    }
}
