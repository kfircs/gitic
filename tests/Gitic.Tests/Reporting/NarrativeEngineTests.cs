using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Gitic.Tests.Reporting;

public class NarrativeProseGenerationTests
{
    private static (AnalysisResult Current, BaselineSnapshot Previous) CreateSampleData()
    {
        var prev = new BaselineSnapshot
        {
            Id = "snap-prev-001",
            Tag = "sprint-41",
            Timestamp = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero),
            Summary = new BaselineSummaryMetrics
            {
                CommitCount = 42,
                AverageLeadTimeHours = 18.0,
                ZombieFiles = 34,
                ZombieLines = 1200,
                Features = 6,
                Bugs = 3,
                TechnicalDebt = 2,
                Chores = 1
            },
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["dx_index"] = 65.0,
                ["p50_lead_time"] = 18.0,
                ["rework_rate"] = 0.22,
                ["hotspot_density"] = 0.35,
                ["zombie_files"] = 34.0
            },
            Areas = new Dictionary<string, AreaBaselineMetrics>(StringComparer.OrdinalIgnoreCase)
            {
                ["Core/"] = new AreaBaselineMetrics
                {
                    Area = "Core/",
                    Touches = 50,
                    Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["hotspot_density"] = 0.35,
                        ["rework_rate"] = 0.22
                    }
                }
            }
        };

        var curr = new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                RepoRoot = "/home/user/project",
                GeneratedAt = "2026-09-08T10:00:00Z",
                CommitCount = 56
            },
            Areas = new List<AreaMetric>
            {
                new()
                {
                    Area = "Core/",
                    Touches = 80,
                    Churn = 1400,
                    FileCount = 10,
                    ReworkRate = 0.18,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", ActivityShare = 0.83 },
                        new() { Name = "Bob", ActivityShare = 0.17 }
                    }
                },
                new()
                {
                    Area = "Git/",
                    Touches = 35,
                    Churn = 600,
                    FileCount = 8,
                    ReworkRate = 0.12,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Bob", ActivityShare = 0.60 },
                        new() { Name = "Carol", ActivityShare = 0.40 }
                    }
                }
            },
            Files = new List<FileMetric>
            {
                new()
                {
                    Path = "src/Core/Scoring.cs",
                    Area = "Core/",
                    Touches = 28,
                    Churn = 520,
                    AttentionScore = 87.0,
                    ReworkRate = 0.24,
                    ContributorCount = 1,
                    KnowledgeSilo = new KnowledgeSiloMetric
                    {
                        IsSilo = true,
                        TopOwnerShare = 0.83,
                        TruckFactor = 1
                    },
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", ActivityShare = 0.83 }
                    }
                },
                new()
                {
                    Path = "src/Core/Analyzer.cs",
                    Area = "Core/",
                    Touches = 20,
                    Churn = 400,
                    AttentionScore = 75.0,
                    ReworkRate = 0.18,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", ActivityShare = 0.70 },
                        new() { Name = "Bob", ActivityShare = 0.30 }
                    }
                },
                new()
                {
                    Path = "src/Git/Gitgraph.cs",
                    Area = "Git/",
                    Touches = 15,
                    Churn = 300,
                    AttentionScore = 65.0,
                    ReworkRate = 0.10,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Bob", ActivityShare = 0.75 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice", TotalActivity = 60 },
                new() { Name = "Bob", TotalActivity = 40 },
                new() { Name = "Carol", TotalActivity = 20 }
            },
            LeadTimes = new LeadTimesInfo
            {
                AverageLeadTimeHours = 12.0,
                Merges = new List<MergeLeadTimeRecord>
                {
                    new() { Hash = "m1", LeadTimeHours = 8.0 },
                    new() { Hash = "m2", LeadTimeHours = 12.0 },
                    new() { Hash = "m3", LeadTimeHours = 16.0 },
                    new() { Hash = "m4", LeadTimeHours = 24.0 }
                }
            },
            CuratedReports = new CuratedReports
            {
                CodeRot = new CodeRotMetric
                {
                    ZombieFileCount = 28,
                    ZombieLines = 950
                },
                WorkClassification = new WorkClassificationMetrics
                {
                    Features = 9,
                    Bugs = 2,
                    TechnicalDebt = 3,
                    Chores = 1
                },
                AiCodeStrain = new AiCodeStrainMetric
                {
                    HighVolumeCommits = 2
                }
            },
            TemporalCoupling = new List<TemporalCoupling>
            {
                new()
                {
                    FileA = "src/Git/Gitgraph.cs",
                    FileB = "src/Core/Analyzer.cs",
                    SharedCommits = 12,
                    CouplingDegree = 0.67
                }
            }
        };

        return (curr, prev);
    }

    [Fact]
    public void TestGenerateNarrative_ProducesAllRequiredParagraphs_FromDeltas()
    {
        var (curr, prev) = CreateSampleData();
        var engine = new NarrativeEngine();

        var report = engine.GenerateNarrative(curr, prev);

        Assert.NotNull(report);
        Assert.False(string.IsNullOrWhiteSpace(report.Title));
        Assert.Contains("Repository Health Summary", report.Title);
        Assert.Contains("September 2026", report.Title);

        // Executive Summary
        Assert.False(string.IsNullOrWhiteSpace(report.ExecutiveSummaryParagraph));
        Assert.Contains("Developer Experience (DX) Index", report.ExecutiveSummaryParagraph);

        // Velocity and Delivery
        Assert.False(string.IsNullOrWhiteSpace(report.VelocityAndDeliveryParagraph));
        Assert.Contains("lead time", report.VelocityAndDeliveryParagraph, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("feature enhancements", report.VelocityAndDeliveryParagraph, StringComparison.OrdinalIgnoreCase);

        // Ownership and Risk
        Assert.False(string.IsNullOrWhiteSpace(report.OwnershipAndRiskParagraph));
        Assert.Contains("Core/", report.OwnershipAndRiskParagraph);
        Assert.Contains("Scoring.cs", report.OwnershipAndRiskParagraph);
        Assert.Contains("Alice", report.OwnershipAndRiskParagraph);

        // Recommendations
        Assert.False(string.IsNullOrWhiteSpace(report.RecommendationsParagraph));
        Assert.Contains("Recommended focus", report.RecommendationsParagraph);
        Assert.Contains("Scoring.cs", report.RecommendationsParagraph);
        Assert.True(report.KeyRecommendations.Count >= 2);

        // Summary Deltas
        Assert.NotEmpty(report.SummaryDeltas);
    }

    [Fact]
    public void TestGenerateNarrative_StandaloneWithoutBaseline_ProducesCohesiveProse()
    {
        var (curr, _) = CreateSampleData();
        var engine = new NarrativeEngine();

        var report = engine.GenerateNarrative(curr);

        Assert.NotNull(report);
        Assert.Null(report.PreviousDxIndex);
        Assert.Null(report.DeltaDxIndex);
        Assert.True(report.CurrentDxIndex > 0);

        Assert.False(string.IsNullOrWhiteSpace(report.ExecutiveSummaryParagraph));
        Assert.False(string.IsNullOrWhiteSpace(report.VelocityAndDeliveryParagraph));
        Assert.False(string.IsNullOrWhiteSpace(report.OwnershipAndRiskParagraph));
        Assert.False(string.IsNullOrWhiteSpace(report.RecommendationsParagraph));
        Assert.NotEmpty(report.KeyRecommendations);
    }

    [Fact]
    public void TestGenerateNarrative_HandlesEmptyOrMinimalAnalysisResult()
    {
        var emptyResult = new AnalysisResult();
        var engine = new NarrativeEngine();

        var report = engine.GenerateNarrative(emptyResult);

        Assert.NotNull(report);
        Assert.False(string.IsNullOrWhiteSpace(report.Title));
        Assert.False(string.IsNullOrWhiteSpace(report.ExecutiveSummaryParagraph));
        Assert.False(string.IsNullOrWhiteSpace(report.VelocityAndDeliveryParagraph));
        Assert.False(string.IsNullOrWhiteSpace(report.OwnershipAndRiskParagraph));
        Assert.False(string.IsNullOrWhiteSpace(report.RecommendationsParagraph));
    }

    [Fact]
    public void TestGenerateNarrative_NullResult_ThrowsArgumentNullException()
    {
        var engine = new NarrativeEngine();
        Assert.Throws<ArgumentNullException>(() => engine.GenerateNarrative(null!));
    }

    [Fact]
    public void TestGenerateNarrative_CapturesTemporalCouplingLeakage()
    {
        var (curr, prev) = CreateSampleData();
        var engine = new NarrativeEngine();

        var report = engine.GenerateNarrative(curr, prev);

        Assert.Contains("Gitgraph.cs", report.OwnershipAndRiskParagraph);
        Assert.Contains("Analyzer.cs", report.OwnershipAndRiskParagraph);
        Assert.Contains("architectural boundary", report.OwnershipAndRiskParagraph, StringComparison.OrdinalIgnoreCase);
    }
}

public class NarrativeToneTrajectoryTests
{
    [Fact]
    public void TestDetermineTone_ImprovingTrajectory_WhenDxScoreIncreases()
    {
        var prev = new BaselineSnapshot
        {
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["dx_index"] = 60.0
            }
        };

        var curr = new AnalysisResult
        {
            DxIndex = new DxIndexResult { CompositeScore = 75.0 }
        };

        var engine = new NarrativeEngine();
        var report = engine.GenerateNarrative(curr, prev);

        Assert.Equal(NarrativeTone.Improving, report.Tone);
        Assert.Equal(15.0, report.DeltaDxIndex);
        Assert.Contains("improved notably", report.ExecutiveSummaryParagraph, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestDetermineTone_RegressingTrajectory_WhenDxScoreDecreases()
    {
        var prev = new BaselineSnapshot
        {
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["dx_index"] = 80.0
            }
        };

        var curr = new AnalysisResult
        {
            DxIndex = new DxIndexResult { CompositeScore = 62.0 }
        };

        var engine = new NarrativeEngine();
        var report = engine.GenerateNarrative(curr, prev);

        Assert.Equal(NarrativeTone.Regressing, report.Tone);
        Assert.Equal(-18.0, report.DeltaDxIndex);
        Assert.Contains("regression", report.ExecutiveSummaryParagraph, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestDetermineTone_StableTrajectory_WhenDxScoreRemainsClose()
    {
        var prev = new BaselineSnapshot
        {
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["dx_index"] = 70.0
            }
        };

        var curr = new AnalysisResult
        {
            DxIndex = new DxIndexResult { CompositeScore = 70.5 }
        };

        var engine = new NarrativeEngine();
        var report = engine.GenerateNarrative(curr, prev);

        Assert.Equal(NarrativeTone.Stable, report.Tone);
        Assert.Equal(0.5, report.DeltaDxIndex);
        Assert.Contains("stable", report.ExecutiveSummaryParagraph, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestDetermineTone_StandaloneThresholds()
    {
        var engine = new NarrativeEngine();

        // High score -> Improving
        var high = new AnalysisResult { DxIndex = new DxIndexResult { CompositeScore = 85.0 } };
        var repHigh = engine.GenerateNarrative(high);
        Assert.Equal(NarrativeTone.Improving, repHigh.Tone);

        // Low score -> Regressing
        var low = new AnalysisResult { DxIndex = new DxIndexResult { CompositeScore = 48.0 } };
        var repLow = engine.GenerateNarrative(low);
        Assert.Equal(NarrativeTone.Regressing, repLow.Tone);

        // Mid score -> Stable
        var mid = new AnalysisResult { DxIndex = new DxIndexResult { CompositeScore = 68.0 } };
        var repMid = engine.GenerateNarrative(mid);
        Assert.Equal(NarrativeTone.Stable, repMid.Tone);
    }

    [Fact]
    public void TestVelocityProse_AdjustsAccordingToLeadTimeTrend()
    {
        var prev = new BaselineSnapshot
        {
            Summary = new BaselineSummaryMetrics { AverageLeadTimeHours = 24.0 }
        };

        // Case 1: Improving lead time (24h -> 12h)
        var improvedResult = new AnalysisResult
        {
            LeadTimes = new LeadTimesInfo { AverageLeadTimeHours = 12.0 }
        };

        var engine = new NarrativeEngine();
        var report1 = engine.GenerateNarrative(improvedResult, prev);
        Assert.Contains("improved notably", report1.VelocityAndDeliveryParagraph, StringComparison.OrdinalIgnoreCase);

        // Case 2: Regressing lead time (24h -> 48h)
        var regressedResult = new AnalysisResult
        {
            LeadTimes = new LeadTimesInfo { AverageLeadTimeHours = 48.0 }
        };

        var report2 = engine.GenerateNarrative(regressedResult, prev);
        Assert.Contains("latency regression", report2.VelocityAndDeliveryParagraph, StringComparison.OrdinalIgnoreCase);
    }
}

public class NarrativeEmbeddingTests
{
    private static NarrativeReport CreateSampleReport()
    {
        return new NarrativeReport
        {
            Title = "Repository Health Summary: ProjectX — September 2026",
            Tone = NarrativeTone.Improving,
            CurrentDxIndex = 76.0,
            PreviousDxIndex = 68.0,
            DeltaDxIndex = 8.0,
            ExecutiveSummaryParagraph = "Your team's overall codebase health improved notably this period.",
            VelocityAndDeliveryParagraph = "Delivery velocity accelerated with median lead times dropping from 20h to 12h.",
            OwnershipAndRiskParagraph = "Core module hotspot density stabilized at 32%, with Scoring.cs ownership distributed.",
            RecommendationsParagraph = "**Recommended focus for the upcoming cycle:**\n1. Pair on Core/ hotspots.\n2. Clean up 6 zombie files.",
            KeyRecommendations = new List<string>
            {
                "Pair on Core/ hotspots.",
                "Clean up 6 zombie files."
            }
        };
    }

    [Fact]
    public void TestGenerateProseMarkdown_IncludesAllParagraphSectionsAndHeadings()
    {
        var engine = new NarrativeEngine();
        var report = CreateSampleReport();

        string markdown = engine.GenerateProseMarkdown(report);

        Assert.NotNull(markdown);
        Assert.Contains($"# 📝 {report.Title}", markdown);
        Assert.Contains("## Executive Summary", markdown);
        Assert.Contains(report.ExecutiveSummaryParagraph, markdown);
        Assert.Contains("## Velocity & Delivery", markdown);
        Assert.Contains(report.VelocityAndDeliveryParagraph, markdown);
        Assert.Contains("## Ownership & Risk Analysis", markdown);
        Assert.Contains(report.OwnershipAndRiskParagraph, markdown);
        Assert.Contains("## Recommended Actions", markdown);
        Assert.Contains(report.RecommendationsParagraph, markdown);
    }

    [Fact]
    public void TestGenerateProseHtml_GeneratesCompleteStyledDocumentWithBadges()
    {
        var engine = new NarrativeEngine();
        var report = CreateSampleReport();

        string html = engine.GenerateProseHtml(report);

        Assert.NotNull(html);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("<html", html);
        Assert.Contains("<head>", html);
        Assert.Contains("<body>", html);
        Assert.Contains("badge-improving", html);
        Assert.Contains("Improving", html);
        Assert.Contains("Executive Summary", html);
        Assert.Contains(report.ExecutiveSummaryParagraph, html);
        Assert.Contains("Velocity & Delivery", html);
        Assert.Contains(report.VelocityAndDeliveryParagraph, html);
        Assert.Contains("Ownership & Risk Analysis", html);
        Assert.Contains(report.OwnershipAndRiskParagraph, html);
        Assert.Contains("Recommended Focus & Actions", html);
    }

    [Fact]
    public void TestEmbedIntoMarkdown_InsertsNarrativeSectionCorrectly()
    {
        var engine = new NarrativeEngine();
        var report = CreateSampleReport();

        string baseMarkdown = @"# 📊 Gitic Analysis Report: project
Generated on: Sep 8, 2026

## 📈 Repository Overview
- **Repository Root:** `/home/user/project`
- **Total Files Analyzed:** 50
";

        string embedded = engine.EmbedIntoMarkdown(baseMarkdown, report);

        Assert.Contains("## 📝 Executive Narrative Summary", embedded);
        Assert.Contains(report.ExecutiveSummaryParagraph, embedded);
        Assert.Contains("### Velocity & Delivery", embedded);
        Assert.Contains("### Ownership & Risk Analysis", embedded);
        Assert.Contains("### Recommended Actions", embedded);
        Assert.Contains("## 📈 Repository Overview", embedded);
    }

    [Fact]
    public void TestEmbedIntoHtml_InsertsNarrativeCardInsideBody()
    {
        var engine = new NarrativeEngine();
        var report = CreateSampleReport();

        string baseHtml = @"<!DOCTYPE html>
<html>
<head><title>Dashboard</title></head>
<body>
<div class=""dashboard"">Existing Dashboard</div>
</body>
</html>";

        string embedded = engine.EmbedIntoHtml(baseHtml, report);

        Assert.Contains("narrative-embed", embedded);
        Assert.Contains("📝 Executive Narrative Summary", embedded);
        Assert.Contains(report.ExecutiveSummaryParagraph, embedded);
        Assert.Contains("Existing Dashboard", embedded);
    }

    [Fact]
    public async Task TestMarkdownRenderer_IntegratesNarrative_WhenPresentOnAnalysisResult()
    {
        var report = CreateSampleReport();
        var result = new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                RepoRoot = "/home/user/project",
                GeneratedAt = "2026-09-08T10:00:00Z"
            },
            Narrative = report
        };

        var renderer = new MarkdownRenderer();
        string md = await renderer.RenderAsync(result);

        Assert.Contains("## 📝 Executive Narrative Summary", md);
        Assert.Contains(report.ExecutiveSummaryParagraph, md);
        Assert.Contains("### Velocity & Delivery", md);
        Assert.Contains("### Ownership & Risk Highlights", md);
        Assert.Contains("### Recommended Actions", md);
    }

    [Fact]
    public void TestHtmlEncodingAndSafety_EscapesHtmlEntities()
    {
        var engine = new NarrativeEngine();
        var maliciousReport = new NarrativeReport
        {
            Title = "<script>alert('xss')</script> Project",
            Tone = NarrativeTone.Regressing,
            ExecutiveSummaryParagraph = "Alert: <script>bad()</script> & risk > 50%",
            VelocityAndDeliveryParagraph = "P90 < 10h & merges > 5",
            OwnershipAndRiskParagraph = "File <test.cs> is single-owned.",
            RecommendationsParagraph = "1. Fix <risk> & avoid <tags>"
        };

        string html = engine.GenerateProseHtml(maliciousReport);

        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;alert", html);
        Assert.Contains("&amp;", html);
        Assert.Contains("&gt;", html);
        Assert.Contains("&lt;", html);
    }
}
