using System;
using System.Collections.Generic;
using Xunit;

namespace Gitic.Tests
{
    public class ConfigValidatorRefactoredTests
    {
        [Fact]
        public void TestAliasValidator_ValidAndInvalid()
        {
            var errors = new List<string>();

            // Valid aliases mapping object
            var validAlias = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "canonical", new Dictionary<string, object?> { { "name", "John Doe" }, { "email", "john@example.com" } } },
                    { "identities", new List<object?>
                        {
                            new Dictionary<string, object?> { { "name", "John D" }, { "email", "john.d@example.com" } }
                        }
                    }
                }
            };
            AliasValidator.ValidateAliases(validAlias, "test", errors);
            Assert.Empty(errors);

            // Invalid aliases: not an array
            errors.Clear();
            AliasValidator.ValidateAliases("not-an-array", "test", errors);
            Assert.Single(errors);
            Assert.Contains("must be an array", errors[0]);

            // Invalid alias entry: missing field
            errors.Clear();
            var invalidAlias = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "canonical", new Dictionary<string, object?> { { "name", "" }, { "email", "john@example.com" } } },
                    { "identities", new List<object?>() }
                }
            };
            AliasValidator.ValidateAliases(invalidAlias, "test", errors);
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void TestBotValidator_ValidAndInvalid()
        {
            var errors = new List<string>();

            // Valid bot mapping object
            var validBots = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "name", "Dependabot" },
                    { "email", "dependabot@github.com" },
                    { "pattern", ".*dependabot.*" }
                }
            };
            BotValidator.ValidateBots(validBots, "test", errors);
            Assert.Empty(errors);

            // Invalid bot: empty fields
            errors.Clear();
            var invalidBots = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "name", null },
                    { "email", null },
                    { "pattern", null }
                }
            };
            BotValidator.ValidateBots(invalidBots, "test", errors);
            Assert.NotEmpty(errors);
            Assert.Contains("must define at least one of name, email, or pattern", errors[0]);
        }

        [Fact]
        public void TestExcludeValidator_ValidAndInvalid()
        {
            var errors = new List<string>();

            // Valid excludes
            var validExcludes = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "pattern", "node_modules/**" },
                    { "category", "dependency" }
                }
            };
            ExcludeValidator.ValidateExcludes(validExcludes, "test", errors);
            Assert.Empty(errors);

            // Invalid excludes: missing category
            errors.Clear();
            var invalidExcludes = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "pattern", "node_modules/**" }
                }
            };
            ExcludeValidator.ValidateExcludes(invalidExcludes, "test", errors);
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void TestAreaValidator_ValidAndInvalid()
        {
            var errors = new List<string>();

            // Valid areas
            var validAreas = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "name", "Frontend" },
                    { "paths", new List<object?> { "src/ui/**", "src/styles/**" } }
                }
            };
            AreaValidator.ValidateAreas(validAreas, "test", errors);
            Assert.Empty(errors);

            // Invalid area: empty paths
            errors.Clear();
            var invalidAreas = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "name", "Frontend" },
                    { "paths", new List<object?>() }
                }
            };
            AreaValidator.ValidateAreas(invalidAreas, "test", errors);
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void TestScoringValidator_ValidAndInvalid()
        {
            var errors = new List<string>();

            // Valid scoring attention weights
            var validScoring = new Dictionary<string, object?>
            {
                { "attention", new Dictionary<string, object?>
                    {
                        { "churn", 0.25 },
                        { "recency", 0.25 },
                        { "contributor_spread", 0.25 },
                        { "low_familiarity_concentration", 0.25 }
                    }
                }
            };
            ScoringValidator.ValidateScoring(validScoring, "test", errors);
            Assert.Empty(errors);

            // Invalid attention weights: don't sum to 1.0
            errors.Clear();
            var invalidScoring = new Dictionary<string, object?>
            {
                { "attention", new Dictionary<string, object?>
                    {
                        { "churn", 0.5 },
                        { "recency", 0.5 },
                        { "contributor_spread", 0.5 },
                        { "low_familiarity_concentration", 0.5 }
                    }
                }
            };
            ScoringValidator.ValidateScoring(invalidScoring, "test", errors);
            Assert.NotEmpty(errors);
            Assert.Contains("weights must sum to 1", errors[0]);

            // Invalid attention weights: non-numeric value
            errors.Clear();
            var nonNumericScoring = new Dictionary<string, object?>
            {
                { "attention", new Dictionary<string, object?>
                    {
                        { "churn", "not-a-number" },
                        { "recency", 0.25 },
                        { "contributor_spread", 0.25 },
                        { "low_familiarity_concentration", 0.25 }
                    }
                }
            };
            ScoringValidator.ValidateScoring(nonNumericScoring, "test", errors);
            Assert.NotEmpty(errors);
            Assert.Contains("must be a finite number", errors[0]);

            // Invalid attention weights: out of bounds numeric value
            errors.Clear();
            var outOfBoundsScoring = new Dictionary<string, object?>
            {
                { "attention", new Dictionary<string, object?>
                    {
                        { "churn", 1.5 },
                        { "recency", 0.25 },
                        { "contributor_spread", 0.25 },
                        { "low_familiarity_concentration", 0.25 }
                    }
                }
            };
            ScoringValidator.ValidateScoring(outOfBoundsScoring, "test", errors);
            Assert.NotEmpty(errors);
            Assert.Contains("must be between 0 and 1", errors[0]);

            // Valid attention weights with other numeric types (int)
            errors.Clear();
            var intScoring = new Dictionary<string, object?>
            {
                { "attention", new Dictionary<string, object?>
                    {
                        { "churn", 1 },
                        { "recency", 0 },
                        { "contributor_spread", 0 },
                        { "low_familiarity_concentration", 0 }
                    }
                }
            };
            ScoringValidator.ValidateScoring(intScoring, "test", errors);
            Assert.Empty(errors);
        }

        [Fact]
        public void TestIdentityConfigValidator_ValidAndInvalid()
        {
            var errors = new List<string>();

            // Valid identity config
            var validIdConfig = new Dictionary<string, object?>
            {
                { "merge_on_email", true }
            };
            IdentityConfigValidator.ValidateIdentityConfig(validIdConfig, "test", errors);
            Assert.Empty(errors);

            // Invalid identity config: not a boolean
            errors.Clear();
            var invalidIdConfig = new Dictionary<string, object?>
            {
                { "merge_on_email", "not-a-bool" }
            };
            IdentityConfigValidator.ValidateIdentityConfig(invalidIdConfig, "test", errors);
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void TestMetricsConfigValidator_ValidAndInvalid()
        {
            var errors = new List<string>();

            // Valid metrics config
            var validMetrics = new Dictionary<string, object?>
            {
                { "temporal_coupling_max_commit_file_count", 50 }
            };
            MetricsConfigValidator.ValidateMetricsConfig(validMetrics, "test", errors);
            Assert.Empty(errors);

            // Invalid metrics config: negative integer
            errors.Clear();
            var invalidMetrics = new Dictionary<string, object?>
            {
                { "temporal_coupling_max_commit_file_count", -5 }
            };
            MetricsConfigValidator.ValidateMetricsConfig(invalidMetrics, "test", errors);
            Assert.NotEmpty(errors);
        }
    }
}
