using System;
using System.Collections.Generic;
using Xunit;

namespace Gitic.Tests
{
    public class ConfigValidatorRefactoredTests
    {
        [Fact]
        public void AliasValidator_WithValidAliases_ShouldHaveNoErrors()
        {
            // Arrange
            var errors = new List<string>();
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

            // Act
            AliasValidator.ValidateAliases(validAlias, "test", errors);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void AliasValidator_WithNonArray_ShouldHaveFormatError()
        {
            // Arrange
            var errors = new List<string>();

            // Act
            AliasValidator.ValidateAliases("not-an-array", "test", errors);

            // Assert
            Assert.Single(errors);
            Assert.Contains("must be an array", errors[0]);
        }

        [Fact]
        public void AliasValidator_WithMissingField_ShouldHaveValidationError()
        {
            // Arrange
            var errors = new List<string>();
            var invalidAlias = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "canonical", new Dictionary<string, object?> { { "name", "" }, { "email", "john@example.com" } } },
                    { "identities", new List<object?>() }
                }
            };

            // Act
            AliasValidator.ValidateAliases(invalidAlias, "test", errors);

            // Assert
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void BotValidator_WithValidBots_ShouldHaveNoErrors()
        {
            // Arrange
            var errors = new List<string>();
            var validBots = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "name", "Dependabot" },
                    { "email", "dependabot@github.com" },
                    { "pattern", ".*dependabot.*" }
                }
            };

            // Act
            BotValidator.ValidateBots(validBots, "test", errors);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void BotValidator_WithInvalidBots_ShouldHaveValidationError()
        {
            // Arrange
            var errors = new List<string>();
            var invalidBots = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "name", null },
                    { "email", null },
                    { "pattern", null }
                }
            };

            // Act
            BotValidator.ValidateBots(invalidBots, "test", errors);

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains("must define at least one of name, email, or pattern", errors[0]);
        }

        [Fact]
        public void ExcludeValidator_WithValidExcludes_ShouldHaveNoErrors()
        {
            // Arrange
            var errors = new List<string>();
            var validExcludes = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "pattern", "node_modules/**" },
                    { "category", "dependency" }
                }
            };

            // Act
            ExcludeValidator.ValidateExcludes(validExcludes, "test", errors);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void ExcludeValidator_WithMissingCategory_ShouldHaveValidationError()
        {
            // Arrange
            var errors = new List<string>();
            var invalidExcludes = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "pattern", "node_modules/**" }
                }
            };

            // Act
            ExcludeValidator.ValidateExcludes(invalidExcludes, "test", errors);

            // Assert
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void AreaValidator_WithValidAreas_ShouldHaveNoErrors()
        {
            // Arrange
            var errors = new List<string>();
            var validAreas = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "name", "Frontend" },
                    { "paths", new List<object?> { "src/ui/**", "src/styles/**" } }
                }
            };

            // Act
            AreaValidator.ValidateAreas(validAreas, "test", errors);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void AreaValidator_WithEmptyPaths_ShouldHaveValidationError()
        {
            // Arrange
            var errors = new List<string>();
            var invalidAreas = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    { "name", "Frontend" },
                    { "paths", new List<object?>() }
                }
            };

            // Act
            AreaValidator.ValidateAreas(invalidAreas, "test", errors);

            // Assert
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void ScoringValidator_WithValidScoring_ShouldHaveNoErrors()
        {
            // Arrange
            var errors = new List<string>();
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

            // Act
            ScoringValidator.ValidateScoring(validScoring, "test", errors);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void ScoringValidator_WithAttentionWeightsNotSummingToOne_ShouldHaveValidationError()
        {
            // Arrange
            var errors = new List<string>();
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

            // Act
            ScoringValidator.ValidateScoring(invalidScoring, "test", errors);

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains("weights must sum to 1", errors[0]);
        }

        [Fact]
        public void ScoringValidator_WithNonNumericAttentionWeight_ShouldHaveValidationError()
        {
            // Arrange
            var errors = new List<string>();
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

            // Act
            ScoringValidator.ValidateScoring(nonNumericScoring, "test", errors);

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains("must be a finite number", errors[0]);
        }

        [Fact]
        public void ScoringValidator_WithAttentionWeightOutOfBounds_ShouldHaveValidationError()
        {
            // Arrange
            var errors = new List<string>();
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

            // Act
            ScoringValidator.ValidateScoring(outOfBoundsScoring, "test", errors);

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains("must be between 0 and 1", errors[0]);
        }

        [Fact]
        public void ScoringValidator_WithIntAttentionWeightsSummingToOne_ShouldHaveNoErrors()
        {
            // Arrange
            var errors = new List<string>();
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

            // Act
            ScoringValidator.ValidateScoring(intScoring, "test", errors);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void IdentityConfigValidator_WithValidIdentityConfig_ShouldHaveNoErrors()
        {
            // Arrange
            var errors = new List<string>();
            var validIdConfig = new Dictionary<string, object?>
            {
                { "merge_on_email", true }
            };

            // Act
            IdentityConfigValidator.ValidateIdentityConfig(validIdConfig, "test", errors);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void IdentityConfigValidator_WithNonBooleanMergeOnEmail_ShouldHaveValidationError()
        {
            // Arrange
            var errors = new List<string>();
            var invalidIdConfig = new Dictionary<string, object?>
            {
                { "merge_on_email", "not-a-bool" }
            };

            // Act
            IdentityConfigValidator.ValidateIdentityConfig(invalidIdConfig, "test", errors);

            // Assert
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void MetricsConfigValidator_WithValidMetricsConfig_ShouldHaveNoErrors()
        {
            // Arrange
            var errors = new List<string>();
            var validMetrics = new Dictionary<string, object?>
            {
                { "temporal_coupling_max_commit_file_count", 50 }
            };

            // Act
            MetricsConfigValidator.ValidateMetricsConfig(validMetrics, "test", errors);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void MetricsConfigValidator_WithNegativeTemporalCouplingMaxCommitFileCount_ShouldHaveValidationError()
        {
            // Arrange
            var errors = new List<string>();
            var invalidMetrics = new Dictionary<string, object?>
            {
                { "temporal_coupling_max_commit_file_count", -5 }
            };

            // Act
            MetricsConfigValidator.ValidateMetricsConfig(invalidMetrics, "test", errors);

            // Assert
            Assert.NotEmpty(errors);
        }
    }
}