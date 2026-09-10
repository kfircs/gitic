using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Gitic.Tests;

public class ReviewLoadOverloadDetectionTests
{
    [Fact]
    public void OverloadDetection_SingleOverloadedReviewer_Exceeds2xMean_ClassifiedAsOverloaded()
    {
        // Arrange
        var result = new AnalysisResult
        {
            CuratedReports = new CuratedReports
            {
                ReviewCollaboration = new ReviewCollaborationMetric
                {
                    Pairs = new List<ReviewPair>
                    {
                        new() { Reviewer = "Carol", Author = "Alice", PrCount = 20 },
                        new() { Reviewer = "Carol", Author = "Bob", PrCount = 10 },
                        new() { Reviewer = "Bob", Author = "Alice", PrCount = 5 },
                        new() { Reviewer = "Dave", Author = "Alice", PrCount = 5 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice", Email = "alice@example.com", TotalActivity = 30 },
                new() { Name = "Bob", Email = "bob@example.com", TotalActivity = 25 },
                new() { Name = "Carol", Email = "carol@example.com", TotalActivity = 30 },
                new() { Name = "Dave", Email = "dave@example.com", TotalActivity = 15 }
            }
        };

        var analyzer = new ReviewLoadAnalyzer();

        // Act
        var loadResult = analyzer.AnalyzeReviewLoad(result);

        // Assert
        // Total reviews = 20 + 10 + 5 + 5 = 40 across 4 team members -> mean = 10 (25% share)
        // Overload threshold (> 2x mean) = > 20 reviews (or > 50% share)
        // Carol has 30 reviews (75% share) -> Overloaded!
        var carolProfile = loadResult.Profiles.FirstOrDefault(p => p.Reviewer == "Carol");
        Assert.NotNull(carolProfile);
        Assert.Equal(ReviewLoadClassification.Overloaded, carolProfile.LoadClassification);
        Assert.True(carolProfile.IsOverloaded);
        Assert.Equal(30, carolProfile.ReviewCount);
        Assert.Equal(0.75, carolProfile.ReviewShare);
        Assert.Equal(2, carolProfile.ReviewDiversity);
        Assert.Contains("Alice", carolProfile.AuthorsReviewed);
        Assert.Contains("Bob", carolProfile.AuthorsReviewed);

        // Bob and Dave have 5 reviews each (12.5% share)
        var bobProfile = loadResult.Profiles.FirstOrDefault(p => p.Reviewer == "Bob");
        Assert.NotNull(bobProfile);
        Assert.False(bobProfile.IsOverloaded);

        // Alice conducted 0 reviews -> Underutilized
        var aliceProfile = loadResult.Profiles.FirstOrDefault(p => p.Reviewer == "Alice");
        Assert.NotNull(aliceProfile);
        Assert.Equal(ReviewLoadClassification.Underutilized, aliceProfile.LoadClassification);
        Assert.True(aliceProfile.IsUnderutilized);

        Assert.Equal(1, loadResult.OverloadedReviewersCount);
        Assert.Equal(40, loadResult.TotalReviews);
        Assert.Equal(10.0, loadResult.AverageReviewsPerReviewer);
        Assert.True(loadResult.GiniCoefficient > 0.4, $"Gini was {loadResult.GiniCoefficient}, expected > 0.4");
    }

    [Fact]
    public void OverloadDetection_BalancedReviews_NoReviewerExceeds2xMean()
    {
        // Arrange: 4 contributors each conducting 10 reviews
        var result = new AnalysisResult
        {
            CuratedReports = new CuratedReports
            {
                ReviewCollaboration = new ReviewCollaborationMetric
                {
                    Pairs = new List<ReviewPair>
                    {
                        new() { Reviewer = "Alice", Author = "Bob", PrCount = 10 },
                        new() { Reviewer = "Bob", Author = "Carol", PrCount = 10 },
                        new() { Reviewer = "Carol", Author = "Dave", PrCount = 10 },
                        new() { Reviewer = "Dave", Author = "Alice", PrCount = 10 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice", TotalActivity = 20 },
                new() { Name = "Bob", TotalActivity = 20 },
                new() { Name = "Carol", TotalActivity = 20 },
                new() { Name = "Dave", TotalActivity = 20 }
            }
        };

        var analyzer = new ReviewLoadAnalyzer();

        // Act
        var loadResult = analyzer.AnalyzeReviewLoad(result);

        // Assert
        Assert.Equal(0, loadResult.OverloadedReviewersCount);
        Assert.Equal(0.0, loadResult.GiniCoefficient);
        Assert.All(loadResult.Profiles, p => Assert.Equal(ReviewLoadClassification.Balanced, p.LoadClassification));
        Assert.All(loadResult.Profiles, p => Assert.Equal(0.25, p.ReviewShare));
    }

    [Fact]
    public void OverloadDetection_UnderutilizedReviewers_DetectedCorrectly()
    {
        // Arrange
        var result = new AnalysisResult
        {
            CuratedReports = new CuratedReports
            {
                ReviewCollaboration = new ReviewCollaborationMetric
                {
                    Pairs = new List<ReviewPair>
                    {
                        new() { Reviewer = "Alice", Author = "Bob", PrCount = 20 },
                        new() { Reviewer = "Bob", Author = "Carol", PrCount = 20 },
                        new() { Reviewer = "Carol", Author = "Alice", PrCount = 20 },
                        new() { Reviewer = "Dave", Author = "Alice", PrCount = 1 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice" },
                new() { Name = "Bob" },
                new() { Name = "Carol" },
                new() { Name = "Dave" },
                new() { Name = "Eve" }
            }
        };

        var analyzer = new ReviewLoadAnalyzer();

        // Act
        var loadResult = analyzer.AnalyzeReviewLoad(result);

        // Assert
        var dave = loadResult.Profiles.FirstOrDefault(p => p.Reviewer == "Dave");
        var eve = loadResult.Profiles.FirstOrDefault(p => p.Reviewer == "Eve");

        Assert.NotNull(dave);
        Assert.Equal(ReviewLoadClassification.Underutilized, dave.LoadClassification);

        Assert.NotNull(eve);
        Assert.Equal(ReviewLoadClassification.Underutilized, eve.LoadClassification);
        Assert.Equal(0, eve.ReviewCount);
    }

    [Fact]
    public void OverloadDetection_SingleReviewer_DoesNotCrash_IsBalanced()
    {
        // Arrange
        var result = new AnalysisResult
        {
            CuratedReports = new CuratedReports
            {
                ReviewCollaboration = new ReviewCollaborationMetric
                {
                    Pairs = new List<ReviewPair>
                    {
                        new() { Reviewer = "Alice", Author = "Alice", PrCount = 10 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice" }
            }
        };

        var analyzer = new ReviewLoadAnalyzer();

        // Act
        var loadResult = analyzer.AnalyzeReviewLoad(result);

        // Assert
        Assert.Single(loadResult.Profiles);
        Assert.Equal(ReviewLoadClassification.Balanced, loadResult.Profiles[0].LoadClassification);
        Assert.Equal(0, loadResult.OverloadedReviewersCount);
        Assert.Equal(0.0, loadResult.GiniCoefficient);
    }

    [Fact]
    public void OverloadDetection_ZeroTotalReviews_EmptyPool_HandlesGracefully()
    {
        // Arrange
        var result = new AnalysisResult();
        var analyzer = new ReviewLoadAnalyzer();

        // Act
        var loadResult = analyzer.AnalyzeReviewLoad(result);

        // Assert
        Assert.Empty(loadResult.Profiles);
        Assert.Equal(0, loadResult.TotalReviews);
        Assert.Equal(0.0, loadResult.GiniCoefficient);
        Assert.Equal(0, loadResult.OverloadedReviewersCount);
    }

    [Fact]
    public void OverloadDetection_ReviewDiversity_AndAuthorsReviewed_TrackedCorrectly()
    {
        // Arrange
        var result = new AnalysisResult
        {
            CuratedReports = new CuratedReports
            {
                ReviewCollaboration = new ReviewCollaborationMetric
                {
                    Pairs = new List<ReviewPair>
                    {
                        new() { Reviewer = "Carol", Author = "Alice", PrCount = 10 },
                        new() { Reviewer = "Carol", Author = "Bob", PrCount = 5 },
                        new() { Reviewer = "Carol", Author = "Dave", PrCount = 3 }
                    }
                }
            }
        };

        var analyzer = new ReviewLoadAnalyzer();

        // Act
        var loadResult = analyzer.AnalyzeReviewLoad(result);

        // Assert
        var carol = loadResult.Profiles.FirstOrDefault(p => p.Reviewer == "Carol");
        Assert.NotNull(carol);
        Assert.Equal(18, carol.ReviewCount);
        Assert.Equal(3, carol.ReviewDiversity);
        Assert.Equal(new List<string> { "Alice", "Bob", "Dave" }, carol.AuthorsReviewed);
    }

    [Fact]
    public void OverloadDetection_GiniCoefficient_CalculatesAccurately()
    {
        // Equal
        Assert.Equal(0.0, ReviewLoadAnalyzer.CalculateGiniCoefficient(new List<double> { 10, 10, 10, 10 }));

        // Moderate inequality
        double modGini = ReviewLoadAnalyzer.CalculateGiniCoefficient(new List<double> { 0, 10, 20, 30 });
        Assert.True(modGini > 0.2 && modGini < 0.6);

        // Extreme inequality
        double highGini = ReviewLoadAnalyzer.CalculateGiniCoefficient(new List<double> { 0, 0, 0, 100 });
        Assert.Equal(0.75, highGini);

        // Edge cases
        Assert.Equal(0.0, ReviewLoadAnalyzer.CalculateGiniCoefficient(new List<double>()));
        Assert.Equal(0.0, ReviewLoadAnalyzer.CalculateGiniCoefficient(new List<double> { 5 }));
        Assert.Equal(0.0, ReviewLoadAnalyzer.CalculateGiniCoefficient(new List<double> { 0, 0, 0 }));
    }
}

public class ReviewLoadRedistributionRecommendationTests
{
    [Fact]
    public void Redistribution_SuggestsAlternativeReviewer_BasedOnAreaFamiliarity()
    {
        // Arrange
        var result = new AnalysisResult
        {
            CuratedReports = new CuratedReports
            {
                ReviewCollaboration = new ReviewCollaborationMetric
                {
                    Pairs = new List<ReviewPair>
                    {
                        new() { Reviewer = "Carol", Author = "Alice", PrCount = 40 },
                        new() { Reviewer = "Bob", Author = "Carol", PrCount = 5 },
                        new() { Reviewer = "Dave", Author = "Carol", PrCount = 2 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new()
                {
                    Name = "Alice",
                    Email = "alice@example.com",
                    TotalActivity = 30,
                    Areas = new List<ContributorAreaMetric>
                    {
                        new() { Area = "Core", Activity = 25 },
                        new() { Area = "Cli", Activity = 5 }
                    }
                },
                new()
                {
                    Name = "Carol",
                    Email = "carol@example.com",
                    TotalActivity = 40,
                    Areas = new List<ContributorAreaMetric>
                    {
                        new() { Area = "Core", Activity = 10 },
                        new() { Area = "Git", Activity = 30 }
                    }
                },
                new()
                {
                    Name = "Bob",
                    Email = "bob@example.com",
                    TotalActivity = 20,
                    Areas = new List<ContributorAreaMetric>
                    {
                        new() { Area = "Reporting", Activity = 20 }
                    }
                },
                new()
                {
                    Name = "Dave",
                    Email = "dave@example.com",
                    TotalActivity = 15,
                    Areas = new List<ContributorAreaMetric>
                    {
                        new() { Area = "Core", Activity = 12 },
                        new() { Area = "Cli", Activity = 3 }
                    }
                }
            }
        };

        var analyzer = new ReviewLoadAnalyzer();

        // Act
        var loadResult = analyzer.AnalyzeReviewLoad(result);

        // Assert
        var carol = loadResult.Profiles.FirstOrDefault(p => p.Reviewer == "Carol");
        Assert.NotNull(carol);
        Assert.True(carol.IsOverloaded);

        // Dave has familiarity in Core and Cli (Alice's areas) and low review share (2/47 = ~4.3%)
        // Bob has familiarity only in Reporting (no overlap with Alice's areas)
        // Dave should be the top suggested alternative reviewer for Alice's PRs!
        Assert.NotEmpty(carol.Candidates);
        var topCandidate = carol.Candidates[0];
        Assert.Equal("Dave", topCandidate.CandidateName);
        Assert.Contains("Core", topCandidate.Area);
        Assert.True(topCandidate.FamiliarityScore > 0);
        Assert.Contains("Dave", carol.RedistributionSuggestion);

        // Actionable recommendations should mention routing to Dave
        Assert.Contains(loadResult.ActionableRecommendations, r => r.Contains("Dave") && r.Contains("Alice"));
    }

    [Fact]
    public void Redistribution_FindAlternativeReviewers_ExcludesOverloadedReviewerAndAuthor()
    {
        // Arrange
        var result = new AnalysisResult
        {
            CuratedReports = new CuratedReports
            {
                ReviewCollaboration = new ReviewCollaborationMetric
                {
                    Pairs = new List<ReviewPair>
                    {
                        new() { Reviewer = "Carol", Author = "Alice", PrCount = 30 },
                        new() { Reviewer = "Bob", Author = "Alice", PrCount = 5 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice", TotalActivity = 20, Areas = new() { new() { Area = "Core", Activity = 20 } } },
                new() { Name = "Carol", TotalActivity = 30, Areas = new() { new() { Area = "Core", Activity = 15 } } },
                new() { Name = "Bob", TotalActivity = 15, Areas = new() { new() { Area = "Core", Activity = 10 } } },
                new() { Name = "Dave", TotalActivity = 10, Areas = new() { new() { Area = "Core", Activity = 5 } } }
            }
        };

        var analyzer = new ReviewLoadAnalyzer();

        // Act
        var candidates = analyzer.FindAlternativeReviewers("Carol", "Alice", result);

        // Assert
        Assert.DoesNotContain(candidates, c => c.CandidateName == "Carol");
        Assert.DoesNotContain(candidates, c => c.CandidateName == "Alice");
        Assert.Contains(candidates, c => c.CandidateName == "Bob");
        Assert.Contains(candidates, c => c.CandidateName == "Dave");
    }

    [Fact]
    public void Redistribution_FallbackWhenNoAreaHistory_UsesGeneralCodebaseFamiliarity()
    {
        // Arrange
        var result = new AnalysisResult
        {
            CuratedReports = new CuratedReports
            {
                ReviewCollaboration = new ReviewCollaborationMetric
                {
                    Pairs = new List<ReviewPair>
                    {
                        new() { Reviewer = "Carol", Author = "Alice", PrCount = 30 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice", TotalActivity = 10 },
                new() { Name = "Carol", TotalActivity = 30 },
                new() { Name = "Dave", TotalActivity = 25 }
            }
        };

        var analyzer = new ReviewLoadAnalyzer();

        // Act
        var candidates = analyzer.FindAlternativeReviewers("Carol", "Alice", result);

        // Assert
        Assert.Single(candidates);
        Assert.Equal("Dave", candidates[0].CandidateName);
        Assert.Contains("General codebase familiarity", candidates[0].Reason);
    }

    [Fact]
    public void Redistribution_HandlesNoAlternativeCandidates_Gracefully()
    {
        // Arrange: only Alice and Carol exist in the repo
        var result = new AnalysisResult
        {
            CuratedReports = new CuratedReports
            {
                ReviewCollaboration = new ReviewCollaborationMetric
                {
                    Pairs = new List<ReviewPair>
                    {
                        new() { Reviewer = "Carol", Author = "Alice", PrCount = 30 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice", TotalActivity = 10 },
                new() { Name = "Carol", TotalActivity = 30 }
            }
        };

        var analyzer = new ReviewLoadAnalyzer();

        // Act
        var loadResult = analyzer.AnalyzeReviewLoad(result);

        // Assert
        var carol = loadResult.Profiles.FirstOrDefault(p => p.Reviewer == "Carol");
        Assert.NotNull(carol);
        Assert.True(carol.IsOverloaded);
        Assert.Empty(carol.Candidates);
        Assert.NotEmpty(carol.RedistributionSuggestion);
        Assert.NotEmpty(loadResult.ActionableRecommendations);
    }
}

public class ReviewLoadMetricEnrichmentTests
{
    [Fact]
    public void MetricEnrichment_EnrichesReviewCollaboration_WithRedistributionNotes()
    {
        // Arrange
        var result = new AnalysisResult
        {
            CuratedReports = new CuratedReports
            {
                ReviewCollaboration = new ReviewCollaborationMetric
                {
                    Pairs = new List<ReviewPair>
                    {
                        new() { Reviewer = "Carol", Author = "Alice", PrCount = 40 },
                        new() { Reviewer = "Bob", Author = "Alice", PrCount = 4 },
                        new() { Reviewer = "Dave", Author = "Alice", PrCount = 2 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice", TotalActivity = 20, Areas = new() { new() { Area = "Core", Activity = 20 } } },
                new() { Name = "Carol", TotalActivity = 40, Areas = new() { new() { Area = "Core", Activity = 10 } } },
                new() { Name = "Bob", TotalActivity = 15, Areas = new() { new() { Area = "Core", Activity = 10 } } },
                new() { Name = "Dave", TotalActivity = 10, Areas = new() { new() { Area = "Core", Activity = 8 } } }
            }
        };

        var analyzer = new ReviewLoadAnalyzer();

        // Act
        analyzer.EnrichReviewCollaboration(result);

        // Assert
        Assert.NotNull(result.ReviewLoad);
        Assert.NotNull(result.CuratedReports);
        Assert.NotNull(result.CuratedReports.ReviewCollaboration);
        Assert.NotEmpty(result.CuratedReports.ReviewCollaboration.RedistributionNotes);
        Assert.Equal(result.ReviewLoad.RedistributionNotes, result.CuratedReports.ReviewCollaboration.RedistributionNotes);
        Assert.Contains(result.CuratedReports.ReviewCollaboration.RedistributionNotes, n => n.Contains("Carol") && n.Contains("Alice"));
    }

    [Fact]
    public void MetricEnrichment_AnalyzeReviewLoad_AutomaticallyAttachesToAnalysisResult()
    {
        // Arrange
        var result = new AnalysisResult
        {
            CuratedReports = new CuratedReports
            {
                ReviewCollaboration = new ReviewCollaborationMetric
                {
                    Pairs = new List<ReviewPair>
                    {
                        new() { Reviewer = "Carol", Author = "Alice", PrCount = 30 },
                        new() { Reviewer = "Dave", Author = "Bob", PrCount = 5 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice" },
                new() { Name = "Bob" },
                new() { Name = "Carol" },
                new() { Name = "Dave" }
            }
        };

        var analyzer = new ReviewLoadAnalyzer();

        // Act
        var returnedResult = analyzer.AnalyzeReviewLoad(result);

        // Assert
        Assert.Same(returnedResult, result.ReviewLoad);
        Assert.NotEmpty(result.CuratedReports.ReviewCollaboration.RedistributionNotes);
    }

    [Fact]
    public void MetricEnrichment_WithNullCuratedReports_InstantiatesAndEnriches()
    {
        // Arrange
        var result = new AnalysisResult
        {
            CuratedReports = null,
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice" },
                new() { Name = "Bob" }
            }
        };

        var analyzer = new ReviewLoadAnalyzer();

        // Act
        var loadResult = analyzer.AnalyzeReviewLoad(result);

        // Assert
        Assert.NotNull(result.CuratedReports);
        Assert.NotNull(result.CuratedReports.ReviewCollaboration);
        Assert.NotNull(result.ReviewLoad);
        Assert.NotEmpty(result.CuratedReports.ReviewCollaboration.RedistributionNotes);
    }

    [Fact]
    public void MetricEnrichment_FormattingMethods_RenderFormattedOutput()
    {
        // Arrange
        var result = new AnalysisResult
        {
            CuratedReports = new CuratedReports
            {
                ReviewCollaboration = new ReviewCollaborationMetric
                {
                    Pairs = new List<ReviewPair>
                    {
                        new() { Reviewer = "Carol", Author = "Alice", PrCount = 40 },
                        new() { Reviewer = "Dave", Author = "Alice", PrCount = 5 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice", Areas = new() { new() { Area = "Core", Activity = 10 } } },
                new() { Name = "Carol" },
                new() { Name = "Dave", Areas = new() { new() { Area = "Core", Activity = 10 } } }
            }
        };

        var analyzer = new ReviewLoadAnalyzer();
        var loadResult = analyzer.AnalyzeReviewLoad(result);

        // Act
        string summary = analyzer.FormatSummary(loadResult);
        string markdown = analyzer.FormatMarkdown(loadResult);
        string table = analyzer.FormatCliTable(loadResult);

        // Assert
        Assert.Contains("Review Load Balance", summary);
        Assert.Contains("Carol", summary);
        Assert.Contains("Overloaded", summary);

        Assert.Contains("## ⚖️ Review Load Balancer", markdown);
        Assert.Contains("Carol", markdown);
        Assert.Contains("Dave", markdown);

        Assert.Contains("Carol", table);
        Assert.Contains("Dave", table);
    }

    [Fact]
    public void MetricEnrichment_JsonSerializationRoundtrip_PreservesAllFields()
    {
        // Arrange
        var original = new ReviewLoadAnalysisResult
        {
            TotalReviews = 50,
            GiniCoefficient = 0.65,
            AverageReviewsPerReviewer = 12.5,
            ActionableRecommendations = new List<string> { "Rebalance Carol -> Dave" },
            RedistributionNotes = new List<string> { "Route Alice -> Dave" },
            Profiles = new List<ReviewLoadProfile>
            {
                new()
                {
                    Reviewer = "Carol",
                    ReviewerEmail = "carol@example.com",
                    ReviewCount = 40,
                    ReviewShare = 0.80,
                    AuthorsReviewed = new List<string> { "Alice" },
                    ReviewDiversity = 1,
                    LoadClassification = ReviewLoadClassification.Overloaded,
                    RedistributionSuggestion = "Route to Dave",
                    AuthorCommitCount = 5,
                    AuthorShare = 0.10,
                    Candidates = new List<AlternativeReviewerCandidate>
                    {
                        new()
                        {
                            CandidateName = "Dave",
                            CandidateEmail = "dave@example.com",
                            TargetAuthor = "Alice",
                            Area = "Core",
                            FamiliarityScore = 15.0,
                            CurrentReviewShare = 0.10,
                            Reason = "Familiar with Core"
                        }
                    }
                }
            }
        };

        // Act
        string json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ReviewLoadAnalysisResult>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.TotalReviews, deserialized.TotalReviews);
        Assert.Equal(original.GiniCoefficient, deserialized.GiniCoefficient);
        Assert.Equal(original.AverageReviewsPerReviewer, deserialized.AverageReviewsPerReviewer);
        Assert.Equal(original.ActionableRecommendations, deserialized.ActionableRecommendations);
        Assert.Equal(original.RedistributionNotes, deserialized.RedistributionNotes);
        Assert.Single(deserialized.Profiles);
        Assert.Equal("Carol", deserialized.Profiles[0].Reviewer);
        Assert.Equal(ReviewLoadClassification.Overloaded, deserialized.Profiles[0].LoadClassification);
        Assert.Single(deserialized.Profiles[0].Candidates);
        Assert.Equal("Dave", deserialized.Profiles[0].Candidates[0].CandidateName);
    }
}
