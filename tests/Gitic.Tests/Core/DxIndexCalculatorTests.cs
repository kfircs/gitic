using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Gitic.Tests;

public class DxIndexCompositeScoringTests
{
    [Fact]
    public void Calculate_WithHealthyRepository_ProducesCompositeScoreBetween90And100_AndExcellentRating()
    {
        // Arrange
        var result = new AnalysisResult
        {
            LeadTimes = new LeadTimesInfo
            {
                Merges = new List<MergeLeadTimeRecord>
                {
                    new() { LeadTimeHours = 1.5 },
                    new() { LeadTimeHours = 2.0 },
                    new() { LeadTimeHours = 3.0 },
                    new() { LeadTimeHours = 3.5 }
                }
            },
            Files = new List<FileMetric>
            {
                new() { Path = "Core/A.cs", Area = "Core", ReworkRate = 0.04, Lines = 100 },
                new() { Path = "Core/B.cs", Area = "Core", ReworkRate = 0.05, Lines = 150 },
                new() { Path = "Cli/C.cs", Area = "Cli", ReworkRate = 0.03, Lines = 80 }
            },
            Areas = new List<AreaMetric>
            {
                new()
                {
                    Area = "Core",
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", ActivityShare = 0.40 },
                        new() { Name = "Bob", ActivityShare = 0.35 },
                        new() { Name = "Charlie", ActivityShare = 0.25 }
                    }
                },
                new()
                {
                    Area = "Cli",
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", ActivityShare = 0.50 },
                        new() { Name = "Bob", ActivityShare = 0.50 }
                    }
                }
            },
            TemporalCoupling = new List<TemporalCoupling>
            {
                new() { FileA = "Core/A.cs", FileB = "Core/B.cs", CouplingDegree = 0.8 }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice", Areas = new List<ContributorAreaMetric> { new() { Area = "Core" }, new() { Area = "Cli" } } },
                new() { Name = "Bob", Areas = new List<ContributorAreaMetric> { new() { Area = "Core" } } }
            },
            CuratedReports = new CuratedReports
            {
                CodeRot = new CodeRotMetric { ZombieFileCount = 0, ZombieLines = 0 }
            }
        };

        var calculator = new DxIndexCalculator();

        // Act
        var dxResult = calculator.Calculate(result);

        // Assert
        Assert.NotNull(dxResult);
        Assert.InRange(dxResult.CompositeScore, 90.0, 100.0);
        Assert.Equal(DxRating.Excellent, dxResult.Rating);

        // Verify sub-dimension count & weights
        Assert.Equal(6, dxResult.Dimensions.Count);
        Assert.Equal(0.20, dxResult.Breakdown.DeliveryFlow.Weight);
        Assert.Equal(0.20, dxResult.Breakdown.CodeStability.Weight);
        Assert.Equal(0.20, dxResult.Breakdown.OwnershipHealth.Weight);
        Assert.Equal(0.15, dxResult.Breakdown.ArchitecturalIntegrity.Weight);
        Assert.Equal(0.15, dxResult.Breakdown.CognitiveLoad.Weight);
        Assert.Equal(0.10, dxResult.Breakdown.CodeFreshness.Weight);

        double totalWeight = dxResult.Dimensions.Sum(d => d.Weight);
        Assert.Equal(1.00, Math.Round(totalWeight, 2));
    }

    [Fact]
    public void Calculate_CompositeScore_FollowsExactSubDimensionWeights()
    {
        // Arrange: construct sub-dimension scores and check weighted arithmetic calculation
        var delivery = new DxDimensionScore { Dimension = DxIndexCalculator.DeliveryFlowDimensionName, Score = 80.0, Weight = 0.20 };
        var stability = new DxDimensionScore { Dimension = DxIndexCalculator.CodeStabilityDimensionName, Score = 70.0, Weight = 0.20 };
        var ownership = new DxDimensionScore { Dimension = DxIndexCalculator.OwnershipHealthDimensionName, Score = 60.0, Weight = 0.20 };
        var architecture = new DxDimensionScore { Dimension = DxIndexCalculator.ArchitecturalIntegrityDimensionName, Score = 90.0, Weight = 0.15 };
        var cognitive = new DxDimensionScore { Dimension = DxIndexCalculator.CognitiveLoadDimensionName, Score = 50.0, Weight = 0.15 };
        var freshness = new DxDimensionScore { Dimension = DxIndexCalculator.CodeFreshnessDimensionName, Score = 100.0, Weight = 0.10 };

        double expectedComposite = (80.0 * 0.20) + (70.0 * 0.20) + (60.0 * 0.20) + (90.0 * 0.15) + (50.0 * 0.15) + (100.0 * 0.10);
        // 16.0 + 14.0 + 12.0 + 13.5 + 7.5 + 10.0 = 73.0

        var calculator = new DxIndexCalculator();

        var result = new AnalysisResult
        {
            LeadTimes = new LeadTimesInfo
            {
                Merges = new List<MergeLeadTimeRecord> { new() { LeadTimeHours = 10.0 } }
            },
            Files = new List<FileMetric>
            {
                new() { Path = "Core/F1.cs", Area = "Core", ReworkRate = 0.18 }
            },
            Areas = new List<AreaMetric>
            {
                new()
                {
                    Area = "Core",
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Dev1", ActivityShare = 0.65 },
                        new() { Name = "Dev2", ActivityShare = 0.35 }
                    }
                },
                new()
                {
                    Area = "Cli",
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Dev1", ActivityShare = 0.40 },
                        new() { Name = "Dev2", ActivityShare = 0.60 }
                    }
                },
                new()
                {
                    Area = "Reporting",
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Dev1", ActivityShare = 0.50 },
                        new() { Name = "Dev2", ActivityShare = 0.50 }
                    }
                }
            }
        };

        var dxResult = calculator.Calculate(result);

        // Assert
        Assert.InRange(dxResult.CompositeScore, 0.0, 100.0);
        Assert.Equal(73.0, expectedComposite);
    }

    [Theory]
    [InlineData(95.0, DxRating.Excellent)]
    [InlineData(90.0, DxRating.Excellent)]
    [InlineData(89.9, DxRating.Healthy)]
    [InlineData(70.0, DxRating.Healthy)]
    [InlineData(69.9, DxRating.Watch)]
    [InlineData(50.0, DxRating.Watch)]
    [InlineData(49.9, DxRating.Critical)]
    [InlineData(15.0, DxRating.Critical)]
    public void GetRating_AssignsCorrectQualitativeThresholds(double score, DxRating expectedRating)
    {
        Assert.Equal(expectedRating, DxIndexCalculator.GetRating(score));
    }

    [Fact]
    public void Calculate_WithEmptyAnalysisResult_ProducesValidScoreBetween0And100()
    {
        var result = new AnalysisResult();
        var calculator = new DxIndexCalculator();

        var dxResult = calculator.Calculate(result);

        Assert.NotNull(dxResult);
        Assert.InRange(dxResult.CompositeScore, 0.0, 100.0);
        Assert.Equal(6, dxResult.Dimensions.Count);
    }

    [Fact]
    public void Calculate_WithNullAnalysisResult_ThrowsArgumentNullException()
    {
        var calculator = new DxIndexCalculator();
        Assert.Throws<ArgumentNullException>(() => calculator.Calculate(null!));
    }
}

public class DxIndexDimensionPenaltyTests
{
    private readonly DxIndexCalculator _calculator = new();

    [Fact]
    public void CalculateCodeStability_WithHighReworkRate_PenalizesScoreBelow30()
    {
        // Arrange: files with rework rate > 30% (e.g. 35% and 42%)
        var files = new List<FileMetric>
        {
            new() { Path = "Core/Service.cs", ReworkRate = 0.35 },
            new() { Path = "Core/Repository.cs", ReworkRate = 0.42 }
        };

        // Act
        var stability = _calculator.CalculateCodeStability(files);

        // Assert: High rework rate must be penalized into Critical (< 30)
        Assert.True(stability.Score < 30.0, $"Expected stability score < 30 for >30% rework, but got {stability.Score}");
        Assert.Equal(DxRating.Critical, stability.Rating);
    }

    [Fact]
    public void CalculateCodeStability_WithLowReworkRate_ProducesHighScoreAbove90()
    {
        var files = new List<FileMetric>
        {
            new() { Path = "Core/Service.cs", ReworkRate = 0.05 },
            new() { Path = "Core/Repository.cs", ReworkRate = 0.07 }
        };

        var stability = _calculator.CalculateCodeStability(files);

        Assert.True(stability.Score >= 90.0, $"Expected stability score >= 90 for <10% rework, but got {stability.Score}");
        Assert.Equal(DxRating.Excellent, stability.Rating);
    }

    [Fact]
    public void CalculateOwnershipHealth_WithSevereKnowledgeSilos_PenalizesScoreBelow30()
    {
        // Arrange: > 30% of areas are single-owner silos (e.g. 4 out of 5 areas = 80%)
        var areas = new List<AreaMetric>
        {
            new() { Area = "Area1", Contributors = new List<ContributorShare> { new() { Name = "DevA", ActivityShare = 0.95 } } },
            new() { Area = "Area2", Contributors = new List<ContributorShare> { new() { Name = "DevB", ActivityShare = 0.85 } } },
            new() { Area = "Area3", Contributors = new List<ContributorShare> { new() { Name = "DevC", ActivityShare = 0.90 } } },
            new() { Area = "Area4", Contributors = new List<ContributorShare> { new() { Name = "DevD", ActivityShare = 0.75 } } },
            new() { Area = "Area5", Contributors = new List<ContributorShare> { new() { Name = "DevA", ActivityShare = 0.40 }, new() { Name = "DevB", ActivityShare = 0.60 } } }
        };

        // Act
        var ownership = _calculator.CalculateOwnershipHealth(areas);

        // Assert: Severe silos (>30% of areas) must be penalized into Critical (< 30)
        Assert.True(ownership.Score < 30.0, $"Expected ownership score < 30 for severe silos, but got {ownership.Score}");
        Assert.Equal(DxRating.Critical, ownership.Rating);
    }

    [Fact]
    public void CalculateOwnershipHealth_WithNoSilos_ProducesHighScoreAbove90()
    {
        var areas = new List<AreaMetric>
        {
            new()
            {
                Area = "Area1",
                Contributors = new List<ContributorShare>
                {
                    new() { Name = "DevA", ActivityShare = 0.40 },
                    new() { Name = "DevB", ActivityShare = 0.35 },
                    new() { Name = "DevC", ActivityShare = 0.25 }
                }
            },
            new()
            {
                Area = "Area2",
                Contributors = new List<ContributorShare>
                {
                    new() { Name = "DevA", ActivityShare = 0.45 },
                    new() { Name = "DevB", ActivityShare = 0.30 },
                    new() { Name = "DevC", ActivityShare = 0.25 }
                }
            }
        };

        var ownership = _calculator.CalculateOwnershipHealth(areas);

        Assert.True(ownership.Score >= 90.0, $"Expected ownership score >= 90 when no silo > 50%, but got {ownership.Score}");
        Assert.Equal(DxRating.Excellent, ownership.Rating);
    }

    [Fact]
    public void CalculateDeliveryFlow_WithTailLeadTimes_PenalizesScoreBelow30()
    {
        // Arrange: P90 > 168h (1 week)
        var leadTimes = new LeadTimesInfo
        {
            Merges = new List<MergeLeadTimeRecord>
            {
                new() { LeadTimeHours = 20.0 },
                new() { LeadTimeHours = 40.0 },
                new() { LeadTimeHours = 70.0 },
                new() { LeadTimeHours = 180.0 },
                new() { LeadTimeHours = 220.0 }
            }
        };

        // Act
        var delivery = _calculator.CalculateDeliveryFlow(leadTimes);

        // Assert: Tail lead time > 168h drops into Critical (< 30)
        Assert.True(delivery.Score < 30.0, $"Expected delivery score < 30 for P90 > 168h, but got {delivery.Score}");
        Assert.Equal(DxRating.Critical, delivery.Rating);
    }

    [Fact]
    public void CalculateArchitecturalIntegrity_WithHighCrossBoundaryCoupling_PenalizesScoreBelow30()
    {
        // Arrange: 6 of 10 pairs cross boundaries (60% > 40%)
        var files = new List<FileMetric>
        {
            new() { Path = "Core/A.cs", Area = "Core" },
            new() { Path = "Cli/B.cs", Area = "Cli" },
            new() { Path = "Reporting/C.cs", Area = "Reporting" },
            new() { Path = "Config/D.cs", Area = "Config" }
        };

        var couplings = new List<TemporalCoupling>
        {
            new() { FileA = "Core/A.cs", FileB = "Cli/B.cs" },
            new() { FileA = "Core/A.cs", FileB = "Reporting/C.cs" },
            new() { FileA = "Cli/B.cs", FileB = "Reporting/C.cs" },
            new() { FileA = "Config/D.cs", FileB = "Core/A.cs" },
            new() { FileA = "Config/D.cs", FileB = "Cli/B.cs" },
            new() { FileA = "Config/D.cs", FileB = "Reporting/C.cs" },
            new() { FileA = "Core/A.cs", FileB = "Core/A.cs" },
            new() { FileA = "Cli/B.cs", FileB = "Cli/B.cs" },
            new() { FileA = "Reporting/C.cs", FileB = "Reporting/C.cs" },
            new() { FileA = "Config/D.cs", FileB = "Config/D.cs" }
        };

        // Act
        var architecture = _calculator.CalculateArchitecturalIntegrity(couplings, files);

        // Assert: > 40% cross-boundary coupling drops into Critical (< 30)
        Assert.True(architecture.Score < 30.0, $"Expected architecture score < 30 for >40% cross-boundary coupling, but got {architecture.Score}");
        Assert.Equal(DxRating.Critical, architecture.Rating);
    }

    [Fact]
    public void CalculateCognitiveLoad_WithHighDevBreadth_PenalizesScoreBelow30()
    {
        // Arrange: Devs touch > 8 areas on average
        var contributors = new List<ContributorMetric>
        {
            new()
            {
                Name = "DevA",
                Areas = Enumerable.Range(1, 10).Select(i => new ContributorAreaMetric { Area = $"Area{i}" }).ToList()
            },
            new()
            {
                Name = "DevB",
                Areas = Enumerable.Range(1, 9).Select(i => new ContributorAreaMetric { Area = $"Area{i}" }).ToList()
            }
        };

        // Act
        var cognitive = _calculator.CalculateCognitiveLoad(contributors);

        // Assert: > 8 areas touched per dev drops into Critical (< 30)
        Assert.True(cognitive.Score < 30.0, $"Expected cognitive load score < 30 for >8 areas/dev, but got {cognitive.Score}");
        Assert.Equal(DxRating.Critical, cognitive.Rating);
    }

    [Fact]
    public void CalculateCodeFreshness_WithHighZombieFileRatio_PenalizesScoreBelow30()
    {
        // Arrange: > 25% zombie files (e.g. 35 of 100 files = 35%)
        var codeRot = new CodeRotMetric { ZombieFileCount = 35 };

        // Act
        var freshness = _calculator.CalculateCodeFreshness(codeRot, 100);

        // Assert: > 25% zombie ratio drops into Critical (< 30)
        Assert.True(freshness.Score < 30.0, $"Expected freshness score < 30 for >25% zombie ratio, but got {freshness.Score}");
        Assert.Equal(DxRating.Critical, freshness.Rating);
    }

    [Fact]
    public void Calculate_IdentifiesTopDragFactors_RankedByDragImpact()
    {
        // Arrange: Repository with severe rework rate (stability drag) and knowledge silos (ownership drag)
        var result = new AnalysisResult
        {
            Files = new List<FileMetric>
            {
                new() { Path = "Core/Hotspot.cs", Area = "Core", ReworkRate = 0.45 }
            },
            Areas = new List<AreaMetric>
            {
                new()
                {
                    Area = "Core",
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "SingleDev", ActivityShare = 1.0 }
                    }
                }
            }
        };

        // Act
        var dxResult = _calculator.Calculate(result);

        // Assert
        Assert.NotEmpty(dxResult.TopDragFactors);
        var primaryDrag = dxResult.TopDragFactors.First();
        Assert.True(primaryDrag.DragScore > 0);
        Assert.False(string.IsNullOrWhiteSpace(primaryDrag.Reason));
        Assert.False(string.IsNullOrWhiteSpace(primaryDrag.Recommendation));
    }
}

public class DxIndexSerializationTests
{
    [Fact]
    public void Serialize_AnalysisResultWithDxIndex_SerializesCleanlyToJson()
    {
        // Arrange
        var result = new AnalysisResult
        {
            DxIndex = new DxIndexResult
            {
                CompositeScore = 67.4,
                Rating = DxRating.Watch,
                Breakdown = new DxIndexBreakdown
                {
                    DeliveryFlow = new DxDimensionScore { Dimension = "Delivery Flow", Score = 82.0, Weight = 0.20, Rating = DxRating.Healthy },
                    CodeStability = new DxDimensionScore { Dimension = "Code Stability", Score = 71.0, Weight = 0.20, Rating = DxRating.Healthy },
                    OwnershipHealth = new DxDimensionScore { Dimension = "Ownership Health", Score = 45.0, Weight = 0.20, Rating = DxRating.Critical },
                    ArchitecturalIntegrity = new DxDimensionScore { Dimension = "Architectural Integrity", Score = 78.0, Weight = 0.15, Rating = DxRating.Healthy },
                    CognitiveLoad = new DxDimensionScore { Dimension = "Cognitive Load", Score = 55.0, Weight = 0.15, Rating = DxRating.Watch },
                    CodeFreshness = new DxDimensionScore { Dimension = "Code Freshness", Score = 73.0, Weight = 0.10, Rating = DxRating.Healthy }
                },
                Dimensions = new List<DxDimensionScore>
                {
                    new() { Dimension = "Delivery Flow", Score = 82.0, Weight = 0.20, Rating = DxRating.Healthy },
                    new() { Dimension = "Ownership Health", Score = 45.0, Weight = 0.20, Rating = DxRating.Critical }
                },
                TopDragFactors = new List<DxDragFactor>
                {
                    new()
                    {
                        Dimension = "Ownership Health",
                        DragScore = 11.0,
                        Score = 45.0,
                        Reason = "Knowledge concentration is severe",
                        Recommendation = "Distribute ownership"
                    }
                }
            }
        };

        // Act
        string json = JsonSerializer.Serialize(result, JsonSerializationDefaults.Indented);

        // Assert
        Assert.Contains("\"dx_index\":", json);
        Assert.Contains("\"composite_score\": 67.4", json);
        Assert.Contains("\"rating\": \"Watch\"", json);
        Assert.Contains("\"ownership_health\":", json);
        Assert.Contains("\"top_drag_factors\":", json);
    }

    [Fact]
    public void Deserialize_AnalysisResultWithDxIndex_RoundtripsAccurately()
    {
        // Arrange
        var original = new AnalysisResult
        {
            DxIndex = new DxIndexResult
            {
                CompositeScore = 88.5,
                Rating = DxRating.Healthy,
                Breakdown = new DxIndexBreakdown
                {
                    DeliveryFlow = new DxDimensionScore { Dimension = "Delivery Flow", Score = 90.0, Weight = 0.20, Rating = DxRating.Excellent },
                    CodeStability = new DxDimensionScore { Dimension = "Code Stability", Score = 85.0, Weight = 0.20, Rating = DxRating.Healthy },
                    OwnershipHealth = new DxDimensionScore { Dimension = "Ownership Health", Score = 80.0, Weight = 0.20, Rating = DxRating.Healthy },
                    ArchitecturalIntegrity = new DxDimensionScore { Dimension = "Architectural Integrity", Score = 95.0, Weight = 0.15, Rating = DxRating.Excellent },
                    CognitiveLoad = new DxDimensionScore { Dimension = "Cognitive Load", Score = 88.0, Weight = 0.15, Rating = DxRating.Healthy },
                    CodeFreshness = new DxDimensionScore { Dimension = "Code Freshness", Score = 92.0, Weight = 0.10, Rating = DxRating.Excellent }
                },
                TopDragFactors = new List<DxDragFactor>
                {
                    new()
                    {
                        Dimension = "Ownership Health",
                        DragScore = 4.0,
                        Score = 80.0,
                        Reason = "1 area has moderate silo",
                        Recommendation = "Encourage cross reviews"
                    }
                }
            }
        };

        string json = JsonSerializer.Serialize(original, JsonSerializationDefaults.Indented);

        // Act
        var roundtripped = JsonSerializer.Deserialize<AnalysisResult>(json);

        // Assert
        Assert.NotNull(roundtripped);
        Assert.NotNull(roundtripped.DxIndex);
        Assert.Equal(88.5, roundtripped.DxIndex.CompositeScore);
        Assert.Equal(DxRating.Healthy, roundtripped.DxIndex.Rating);
        Assert.Equal(90.0, roundtripped.DxIndex.Breakdown.DeliveryFlow.Score);
        Assert.Equal(DxRating.Excellent, roundtripped.DxIndex.Breakdown.DeliveryFlow.Rating);
        Assert.Single(roundtripped.DxIndex.TopDragFactors);
        Assert.Equal("Ownership Health", roundtripped.DxIndex.TopDragFactors[0].Dimension);
        Assert.Equal("1 area has moderate silo", roundtripped.DxIndex.TopDragFactors[0].Reason);
    }

    [Fact]
    public void Serialize_AnalysisResultWithNullDxIndex_OmitsDxIndexField()
    {
        // Arrange
        var result = new AnalysisResult { DxIndex = null };

        // Act
        string json = JsonSerializer.Serialize(result, JsonSerializationDefaults.Indented);

        // Assert
        Assert.DoesNotContain("\"dx_index\"", json);
    }

    [Fact]
    public async Task JsonRenderer_RendersAnalysisResultWithDxIndexSuccessfully()
    {
        // Arrange
        var result = new AnalysisResult
        {
            DxIndex = DxIndexCalculator.Compute(new AnalysisResult())
        };

        var renderer = new JsonRenderer();

        // Act
        string renderedJson = await renderer.RenderAsync(result);

        // Assert
        Assert.NotNull(renderedJson);
        Assert.Contains("\"dx_index\":", renderedJson);

        using var doc = JsonDocument.Parse(renderedJson);
        Assert.True(doc.RootElement.TryGetProperty("dx_index", out var dxElem));
        Assert.True(dxElem.TryGetProperty("composite_score", out _));
        Assert.True(dxElem.TryGetProperty("breakdown", out _));
    }
}
