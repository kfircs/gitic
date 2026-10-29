using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Gitic;

namespace Gitic.Tests.Core;

public class ActionEnginePrioritizationTests
{
    [Fact]
    public void Evaluates_HighChurnSilo_TriggeredAsTopPriority()
    {
        // Arrange: Hotspot file with >75% single owner share
        var file = new FileMetric
        {
            Path = "src/Core/HotspotService.cs",
            Area = "Core",
            AttentionScore = 92.5,
            HeatScore = 88.0,
            Touches = 35,
            Churn = 450,
            ReworkRate = 0.22,
            KnowledgeSilo = new KnowledgeSiloMetric
            {
                IsSilo = true,
                TopOwnerShare = 0.85,
                TruckFactor = 1
            },
            Contributors = new List<ContributorShare>
            {
                new() { Name = "Alice", Activity = 30, ActivityShare = 0.85 },
                new() { Name = "Bob", Activity = 5, ActivityShare = 0.15 }
            }
        };

        var result = new AnalysisResult
        {
            Files = new List<FileMetric> { file },
            Areas = new List<AreaMetric> { new() { Area = "Core", FileCount = 1, Touches = 35 } }
        };

        var engine = new ActionEngine();

        // Act
        var actions = engine.Evaluate(result);

        // Assert
        Assert.NotEmpty(actions);
        var siloAction = actions.FirstOrDefault(a => a.DetectionRules.Contains("distribute_silo"));
        Assert.NotNull(siloAction);
        Assert.Equal(1, siloAction.Priority);
        Assert.Equal(ActionCategories.KnowledgeRisk, siloAction.Category);
        Assert.Contains("HotspotService.cs", siloAction.Title);
        Assert.Contains("Alice", siloAction.Narrative);
        Assert.Contains("85%", siloAction.Narrative);
    }

    [Fact]
    public void Evaluates_CouplingHub_AcrossMultipleAreas()
    {
        // Arrange: Hub file with temporal coupling to multiple files in different areas
        var couplings = new List<TemporalCoupling>
        {
            new() { FileA = "src/Core/Types.cs", FileB = "src/Cli/Cli.cs", CouplingDegree = 0.75, SharedCommits = 15 },
            new() { FileA = "src/Core/Types.cs", FileB = "src/Git/GitClient.cs", CouplingDegree = 0.65, SharedCommits = 12 },
            new() { FileA = "src/Core/Types.cs", FileB = "src/Reporting/MarkdownRenderer.cs", CouplingDegree = 0.55, SharedCommits = 10 }
        };

        var result = new AnalysisResult
        {
            Files = new List<FileMetric>
            {
                new() { Path = "src/Core/Types.cs", Area = "Core", Touches = 25, Lines = 500 },
                new() { Path = "src/Cli/Cli.cs", Area = "Cli", Touches = 20 },
                new() { Path = "src/Git/GitClient.cs", Area = "Git", Touches = 18 },
                new() { Path = "src/Reporting/MarkdownRenderer.cs", Area = "Reporting", Touches = 15 }
            },
            TemporalCoupling = couplings
        };

        var engine = new ActionEngine();

        // Act
        var actions = engine.Evaluate(result);

        // Assert
        var hubAction = actions.FirstOrDefault(a => a.DetectionRules.Contains("coupling_hub"));
        Assert.NotNull(hubAction);
        Assert.Equal(2, hubAction.Priority);
        Assert.Equal(ActionCategories.ArchitecturalDrift, hubAction.Category);
        Assert.Contains("Types.cs", hubAction.Title);
        Assert.Contains("external area", hubAction.Narrative);
    }

    [Fact]
    public void Evaluates_RefactorGodFile_WhenLinesOver800TouchesOver20AndCouplingOver3()
    {
        // Arrange: God file matching criteria: lines > 800, touches > 20, coupling > 3
        var file = new FileMetric
        {
            Path = "src/Core/MonolithEngine.cs",
            Area = "Core",
            Lines = 1250,
            Touches = 28,
            Churn = 1400,
            AttentionScore = 95.0,
            Contributors = new List<ContributorShare>
            {
                new() { Name = "Dave", Activity = 28, ActivityShare = 1.0 }
            }
        };

        var couplings = new List<TemporalCoupling>
        {
            new() { FileA = "src/Core/MonolithEngine.cs", FileB = "src/Core/SubA.cs", CouplingDegree = 0.70 },
            new() { FileA = "src/Core/MonolithEngine.cs", FileB = "src/Core/SubB.cs", CouplingDegree = 0.60 },
            new() { FileA = "src/Core/MonolithEngine.cs", FileB = "src/Core/SubC.cs", CouplingDegree = 0.50 },
            new() { FileA = "src/Core/MonolithEngine.cs", FileB = "src/Core/SubD.cs", CouplingDegree = 0.40 }
        };

        var result = new AnalysisResult
        {
            Files = new List<FileMetric> { file },
            TemporalCoupling = couplings
        };

        var engine = new ActionEngine();

        // Act
        var actions = engine.Evaluate(result);

        // Assert
        var godAction = actions.FirstOrDefault(a => a.DetectionRules.Contains("refactor_god_file"));
        Assert.NotNull(godAction);
        Assert.Equal(1, godAction.Priority);
        Assert.Equal(ActionCategories.CodeHealth, godAction.Category);
        Assert.Contains("MonolithEngine.cs", godAction.Title);
        Assert.Contains("1,250", godAction.Narrative);
        Assert.Contains("4 other files", godAction.Narrative);
    }

    [Fact]
    public void Evaluates_ReviewBottleneck_WhenSingleReviewerExceeds30Percent()
    {
        // Arrange: Review collaboration where Carol handles 45% of reviews
        var result = new AnalysisResult
        {
            CuratedReports = new CuratedReports
            {
                ReviewCollaboration = new ReviewCollaborationMetric
                {
                    ReviewerSilos = 1,
                    Pairs = new List<ReviewPair>
                    {
                        new() { Author = "Alice", Reviewer = "Carol", PrCount = 18 },
                        new() { Author = "Bob", Reviewer = "Carol", PrCount = 9 },
                        new() { Author = "Dave", Reviewer = "Eve", PrCount = 15 },
                        new() { Author = "Eve", Reviewer = "Frank", PrCount = 18 }
                    }
                }
            }
        };

        var engine = new ActionEngine();

        // Act
        var actions = engine.Evaluate(result);

        // Assert
        var reviewAction = actions.FirstOrDefault(a => a.DetectionRules.Contains("review_bottleneck"));
        Assert.NotNull(reviewAction);
        Assert.Equal(2, reviewAction.Priority);
        Assert.Equal(ActionCategories.DeliveryBottleneck, reviewAction.Category);
        Assert.Contains("Carol", reviewAction.Title);
        Assert.Contains("45%", reviewAction.Narrative);
    }

    [Fact]
    public void Evaluates_AcceleratingRot_WhenZombieFilesGrowOver20PercentAgainstBaseline()
    {
        // Arrange: Previous baseline with 10 zombie files, current result with 16 zombie files (60% growth)
        var baseline = new BaselineSnapshot
        {
            Summary = new BaselineSummaryMetrics
            {
                ZombieFiles = 10,
                ZombieLines = 1500
            }
        };

        var result = new AnalysisResult
        {
            CuratedReports = new CuratedReports
            {
                CodeRot = new CodeRotMetric
                {
                    ZombieFileCount = 16,
                    ZombieLines = 2400,
                    ThresholdDays = 365
                }
            }
        };

        var engine = new ActionEngine();

        // Act
        var actions = engine.Evaluate(result, baseline);

        // Assert
        var rotAction = actions.FirstOrDefault(a => a.DetectionRules.Contains("accelerating_rot"));
        Assert.NotNull(rotAction);
        Assert.Equal(3, rotAction.Priority);
        Assert.Equal(ActionCategories.CodeHealth, rotAction.Category);
        Assert.Contains("60%", rotAction.Narrative);
        Assert.Contains("10 → 16 files", rotAction.Narrative);
    }

    [Fact]
    public void Ranks_Recommendations_StrictlyByPriorityAscending()
    {
        // Arrange: Result triggering P1 (god file & silo), P2 (hub & review), P3 (rot)
        var result = new AnalysisResult
        {
            Files = new List<FileMetric>
            {
                new()
                {
                    Path = "src/Core/BigHotspot.cs",
                    Area = "Core",
                    AttentionScore = 95.0,
                    Lines = 1100,
                    Touches = 25,
                    KnowledgeSilo = new KnowledgeSiloMetric { IsSilo = true, TopOwnerShare = 0.90 },
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", Activity = 25, ActivityShare = 0.90 },
                        new() { Name = "Bob", Activity = 3, ActivityShare = 0.10 }
                    }
                },
                new() { Path = "src/Cli/Cli.cs", Area = "Cli", Touches = 15 },
                new() { Path = "src/Git/Git.cs", Area = "Git", Touches = 12 },
                new() { Path = "src/Reporting/Rep.cs", Area = "Reporting", Touches = 10 }
            },
            TemporalCoupling = new List<TemporalCoupling>
            {
                new() { FileA = "src/Core/BigHotspot.cs", FileB = "src/Cli/Cli.cs", CouplingDegree = 0.7 },
                new() { FileA = "src/Core/BigHotspot.cs", FileB = "src/Git/Git.cs", CouplingDegree = 0.6 },
                new() { FileA = "src/Core/BigHotspot.cs", FileB = "src/Reporting/Rep.cs", CouplingDegree = 0.5 },
                new() { FileA = "src/Core/BigHotspot.cs", FileB = "src/Core/Other.cs", CouplingDegree = 0.4 }
            },
            CuratedReports = new CuratedReports
            {
                ReviewCollaboration = new ReviewCollaborationMetric
                {
                    Pairs = new List<ReviewPair>
                    {
                        new() { Author = "Alice", Reviewer = "Carol", PrCount = 10 },
                        new() { Author = "Bob", Reviewer = "Carol", PrCount = 10 }
                    }
                },
                CodeRot = new CodeRotMetric { ZombieFileCount = 10, ZombieLines = 2000 }
            }
        };

        var engine = new ActionEngine();

        // Act
        var actions = engine.Evaluate(result);

        // Assert
        Assert.True(actions.Count >= 3);
        for (int i = 0; i < actions.Count - 1; i++)
        {
            Assert.True(actions[i].Priority <= actions[i + 1].Priority);
            Assert.Equal($"ACT-{(i + 1):D3}", actions[i].Id);
        }
    }

    [Fact]
    public void CleanCodebase_ProducesNoRecommendations()
    {
        // Arrange: Clean, well-distributed codebase
        var result = new AnalysisResult
        {
            Files = new List<FileMetric>
            {
                new()
                {
                    Path = "src/Core/Small.cs",
                    Area = "Core",
                    Lines = 150,
                    Touches = 5,
                    AttentionScore = 20.0,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", Activity = 3, ActivityShare = 0.60 },
                        new() { Name = "Bob", Activity = 2, ActivityShare = 0.40 }
                    }
                }
            }
        };

        var engine = new ActionEngine();

        // Act
        var actions = engine.Evaluate(result);

        // Assert
        Assert.Empty(actions);
    }
}

public class ActionRationaleAndAssigneeTests
{
    [Fact]
    public void DistributeSiloRule_SuggestsSecondaryDeveloper_WithFamiliarity()
    {
        // Arrange
        var file = new FileMetric
        {
            Path = "src/Core/PaymentEngine.cs",
            Area = "Core",
            AttentionScore = 89.0,
            Touches = 30,
            KnowledgeSilo = new KnowledgeSiloMetric { IsSilo = true, TopOwnerShare = 0.80 },
            Contributors = new List<ContributorShare>
            {
                new() { Name = "Alice", Activity = 24, ActivityShare = 0.80 },
                new() { Name = "Bob", Activity = 6, ActivityShare = 0.20 }
            }
        };

        var result = new AnalysisResult
        {
            Files = new List<FileMetric> { file },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice", TotalActivity = 50 },
                new() { Name = "Bob", TotalActivity = 20 }
            }
        };

        var engine = new ActionEngine();

        // Act
        var actions = engine.Evaluate(result);

        // Assert
        var action = actions.First(a => a.DetectionRules.Contains("distribute_silo"));
        Assert.Contains("Bob", action.SuggestedAssignee);
        Assert.Contains("20%", action.SuggestedAssignee);
        Assert.Contains("coordination savings", action.EstimatedImpact);
    }

    [Fact]
    public void DistributeSiloRule_FallsBackToAreaDeveloper_WhenNoSecondaryOnFile()
    {
        // Arrange: File only touched by Alice, but Carol has familiarity in "Core"
        var file = new FileMetric
        {
            Path = "src/Core/IsolatedFeature.cs",
            Area = "Core",
            AttentionScore = 91.0,
            Touches = 20,
            KnowledgeSilo = new KnowledgeSiloMetric { IsSilo = true, TopOwnerShare = 1.0 },
            Contributors = new List<ContributorShare>
            {
                new() { Name = "Alice", Activity = 20, ActivityShare = 1.0 }
            }
        };

        var result = new AnalysisResult
        {
            Files = new List<FileMetric> { file },
            Contributors = new List<ContributorMetric>
            {
                new()
                {
                    Name = "Alice",
                    TotalActivity = 50,
                    Areas = new List<ContributorAreaMetric> { new() { Area = "Core", Activity = 50, ActivityShare = 0.80, FamiliarityScore = 80 } }
                },
                new()
                {
                    Name = "Carol",
                    TotalActivity = 30,
                    Areas = new List<ContributorAreaMetric> { new() { Area = "Core", Activity = 20, ActivityShare = 0.20, FamiliarityScore = 45 } }
                }
            }
        };

        var engine = new ActionEngine();

        // Act
        var actions = engine.Evaluate(result);

        // Assert
        var action = actions.First(a => a.DetectionRules.Contains("distribute_silo"));
        Assert.Contains("Carol", action.SuggestedAssignee);
        Assert.Contains("45%", action.SuggestedAssignee);
    }

    [Fact]
    public void RefactorGodFileRule_SuggestsPrimaryAuthor()
    {
        // Arrange
        var file = new FileMetric
        {
            Path = "src/Core/GiantService.cs",
            Area = "Core",
            Lines = 950,
            Touches = 25,
            Contributors = new List<ContributorShare>
            {
                new() { Name = "Alice", Activity = 20, ActivityShare = 0.80 }
            }
        };

        var couplings = new List<TemporalCoupling>
        {
            new() { FileA = "src/Core/GiantService.cs", FileB = "src/A.cs", CouplingDegree = 0.8 },
            new() { FileA = "src/Core/GiantService.cs", FileB = "src/B.cs", CouplingDegree = 0.7 },
            new() { FileA = "src/Core/GiantService.cs", FileB = "src/C.cs", CouplingDegree = 0.6 },
            new() { FileA = "src/Core/GiantService.cs", FileB = "src/D.cs", CouplingDegree = 0.5 }
        };

        var result = new AnalysisResult
        {
            Files = new List<FileMetric> { file },
            TemporalCoupling = couplings
        };

        var engine = new ActionEngine();

        // Act
        var actions = engine.Evaluate(result);

        // Assert
        var action = actions.First(a => a.DetectionRules.Contains("refactor_god_file"));
        Assert.Contains("Alice", action.SuggestedAssignee);
        Assert.Contains("primary author", action.SuggestedAssignee);
        Assert.NotEmpty(action.Narrative);
        Assert.NotEmpty(action.EstimatedImpact);
    }

    [Fact]
    public void ReviewBottleneckRule_SuggestsRedistributingToAlternativeReviewers()
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
                        new() { Author = "Alice", Reviewer = "Carol", PrCount = 20 },
                        new() { Author = "Dave", Reviewer = "Bob", PrCount = 5 },
                        new() { Author = "Eve", Reviewer = "Frank", PrCount = 5 }
                    }
                }
            }
        };

        var engine = new ActionEngine();

        // Act
        var actions = engine.Evaluate(result);

        // Assert
        var action = actions.First(a => a.DetectionRules.Contains("review_bottleneck"));
        Assert.Contains("Carol", action.Narrative);
        Assert.Contains("Redistribute reviews", action.SuggestedAssignee);
    }

    [Fact]
    public void AllActions_ContainEvidenceAndDetectionRules()
    {
        // Arrange
        var file = new FileMetric
        {
            Path = "src/Core/Hotspot.cs",
            Area = "Core",
            AttentionScore = 90.0,
            Touches = 30,
            KnowledgeSilo = new KnowledgeSiloMetric { IsSilo = true, TopOwnerShare = 0.90 }
        };

        var result = new AnalysisResult
        {
            Files = new List<FileMetric> { file }
        };

        var engine = new ActionEngine();

        // Act
        var actions = engine.Evaluate(result);

        // Assert
        foreach (var action in actions)
        {
            Assert.NotEmpty(action.DetectionRules);
            Assert.NotEmpty(action.Evidence);
            Assert.False(string.IsNullOrWhiteSpace(action.Narrative));
            Assert.False(string.IsNullOrWhiteSpace(action.EstimatedImpact));
            Assert.False(string.IsNullOrWhiteSpace(action.SuggestedAssignee));
        }
    }
}

public class ActionFormattingTests
{
    [Fact]
    public void FormatCliTable_RendersFormattedTableWithHeadersAndRows()
    {
        // Arrange
        var actions = new List<ActionRecommendation>
        {
            new()
            {
                Id = "ACT-001",
                Priority = 1,
                Category = ActionCategories.KnowledgeRisk,
                Title = "Distribute ownership of Scoring.cs",
                SuggestedAssignee = "Bob (15% familiarity)",
                EstimatedImpact = "~3hrs/week coordination savings"
            },
            new()
            {
                Id = "ACT-002",
                Priority = 2,
                Category = ActionCategories.ArchitecturalDrift,
                Title = "Decouple Types.cs",
                SuggestedAssignee = "Alice (lead maintainer)",
                EstimatedImpact = "Prevents cross-module ripple effects"
            }
        };

        var engine = new ActionEngine();

        // Act
        string table = engine.FormatCliTable(actions);

        // Assert
        Assert.Contains("ACT-001", table);
        Assert.Contains("ACT-002", table);
        Assert.Contains("P1", table);
        Assert.Contains("Distribute ownership", table);
        Assert.Contains("Decouple Types.cs", table);
        Assert.Contains("Bob", table);
    }

    [Fact]
    public void FormatCliTable_HandlesEmptyActionsGracefully()
    {
        // Arrange
        var engine = new ActionEngine();

        // Act
        string table = engine.FormatCliTable(new List<ActionRecommendation>());

        // Assert
        Assert.Contains("No proactive action recommendations", table);
    }

    [Fact]
    public void FormatMarkdown_RendersHeaderSummaryTableAndNarratives()
    {
        // Arrange
        var actions = new List<ActionRecommendation>
        {
            new()
            {
                Id = "ACT-001",
                Priority = 1,
                Category = ActionCategories.KnowledgeRisk,
                Title = "Distribute ownership of Scoring.cs",
                Narrative = "Scoring.cs is owned 83% by Alice. If Alice is unavailable, downstream work stalls.",
                SuggestedAssignee = "Bob (12% familiarity)",
                EstimatedImpact = "~3hrs/week coordination savings",
                DetectionRules = new List<string> { "distribute_silo" }
            }
        };

        var engine = new ActionEngine();

        // Act
        string markdown = engine.FormatMarkdown(actions);

        // Assert
        Assert.Contains("## 🎯 Recommended Actions", markdown);
        Assert.Contains("| ID | Priority | Category | Action | Suggested Assignee | Estimated Impact |", markdown);
        Assert.Contains("| **ACT-001** | P1 | `knowledge_risk` | Distribute ownership of Scoring.cs |", markdown);
        Assert.Contains("### 📋 Action Details & Rationale", markdown);
        Assert.Contains("#### ACT-001: Distribute ownership of Scoring.cs", markdown);
        Assert.Contains("> **Rationale:** Scoring.cs is owned 83% by Alice.", markdown);
        Assert.Contains("`distribute_silo`", markdown);
    }

    [Fact]
    public void FormatMarkdown_HandlesEmptyActionsGracefully()
    {
        // Arrange
        var engine = new ActionEngine();

        // Act
        string markdown = engine.FormatMarkdown(new List<ActionRecommendation>());

        // Assert
        Assert.Contains("## 🎯 Recommended Actions", markdown);
        Assert.Contains("No proactive action recommendations at this time", markdown);
    }
}
