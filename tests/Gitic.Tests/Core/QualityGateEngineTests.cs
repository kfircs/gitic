using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kfc.Cli.Core;
using Xunit;

namespace Gitic.Tests;

#region Test Helpers

internal class GateTestConsoleReporter : IConsoleReporter
{
    public List<string> Messages { get; } = new();
    public List<string> ErrorMessages { get; } = new();

    public void Write(string message) => Messages.Add(message);
    public void WriteLine(string message = "") => Messages.Add(message + "\n");
    public void WriteError(string message) => ErrorMessages.Add(message);
    public void WriteErrorLine(string message) => ErrorMessages.Add(message + "\n");
    public void WriteWarning(string message) => Messages.Add("Warning: " + message + "\n");

    public string Output => string.Join("", Messages);
    public string Error => string.Join("", ErrorMessages);
}

internal class FakeRepositoryAnalyzerForGate : IRepositoryAnalyzer
{
    public AnalysisResult ResultToReturn { get; set; }

    public FakeRepositoryAnalyzerForGate(AnalysisResult? result = null)
    {
        ResultToReturn = result ?? new AnalysisResult();
    }

    public Task<AnalysisResult> AnalyzeAsync(AnalyzeInput input, CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultToReturn);
}

internal static class QualityGateTestDataBuilder
{
    public static AnalysisResult CreateHealthyAnalysisResult()
    {
        return new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                RepoRoot = "/test/repo",
                Command = AnalysisCommand.Gate,
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                CommitCount = 100
            },
            Files = new List<FileMetric>
            {
                new()
                {
                    Path = "src/Core/A.cs",
                    Area = "Core",
                    AttentionScore = 20.0,
                    HeatScore = 25.0,
                    Touches = 5,
                    Churn = 100,
                    ReworkRate = 0.05,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", ActivityShare = 0.5 },
                        new() { Name = "Bob", ActivityShare = 0.5 }
                    }
                },
                new()
                {
                    Path = "src/Core/B.cs",
                    Area = "Core",
                    AttentionScore = 15.0,
                    HeatScore = 20.0,
                    Touches = 4,
                    Churn = 80,
                    ReworkRate = 0.05,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", ActivityShare = 0.5 },
                        new() { Name = "Bob", ActivityShare = 0.5 }
                    }
                },
                new()
                {
                    Path = "src/Cli/C.cs",
                    Area = "Cli",
                    AttentionScore = 10.0,
                    HeatScore = 15.0,
                    Touches = 3,
                    Churn = 50,
                    ReworkRate = 0.04,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Carol", ActivityShare = 0.5 },
                        new() { Name = "Dave", ActivityShare = 0.5 }
                    }
                }
            },
            Areas = new List<AreaMetric>
            {
                new() { Area = "Core", FileCount = 2, AttentionScore = 17.5, ReworkRate = 0.05, Touches = 9, Churn = 180 },
                new() { Area = "Cli", FileCount = 1, AttentionScore = 10.0, ReworkRate = 0.04, Touches = 3, Churn = 50 }
            },
            LeadTimes = new LeadTimesInfo
            {
                AverageLeadTimeHours = 12.0,
                Merges = new List<MergeLeadTimeRecord>
                {
                    new() { Hash = "abc1", LeadTimeHours = 10.0 },
                    new() { Hash = "abc2", LeadTimeHours = 14.0 },
                    new() { Hash = "abc3", LeadTimeHours = 18.0 }
                }
            },
            DxIndex = new DxIndexResult
            {
                CompositeScore = 85.0,
                Rating = DxRating.Healthy
            }
        };
    }

    public static AnalysisResult CreateUnhealthyAnalysisResult()
    {
        return new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                RepoRoot = "/test/repo",
                Command = AnalysisCommand.Gate,
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                CommitCount = 100
            },
            Files = new List<FileMetric>
            {
                new()
                {
                    Path = "src/Core/Hotspot1.cs",
                    Area = "Core",
                    AttentionScore = 85.0,
                    Touches = 50,
                    Churn = 2000,
                    ReworkRate = 0.35,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", ActivityShare = 1.0 }
                    }
                },
                new()
                {
                    Path = "src/Core/Hotspot2.cs",
                    Area = "Core",
                    AttentionScore = 75.0,
                    Touches = 40,
                    Churn = 1500,
                    ReworkRate = 0.30,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", ActivityShare = 1.0 }
                    }
                },
                new()
                {
                    Path = "src/Core/Hotspot3.cs",
                    Area = "Core",
                    AttentionScore = 70.0,
                    Touches = 30,
                    Churn = 1000,
                    ReworkRate = 0.28,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", ActivityShare = 1.0 }
                    }
                },
                new()
                {
                    Path = "src/Core/Normal.cs",
                    Area = "Core",
                    AttentionScore = 20.0,
                    Touches = 5,
                    Churn = 100,
                    ReworkRate = 0.10,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Bob", ActivityShare = 1.0 }
                    }
                }
            },
            Areas = new List<AreaMetric>
            {
                new() { Area = "Core", FileCount = 4, AttentionScore = 62.5, ReworkRate = 0.26 }
            },
            LeadTimes = new LeadTimesInfo
            {
                AverageLeadTimeHours = 80.0,
                Merges = new List<MergeLeadTimeRecord>
                {
                    new() { Hash = "m1", LeadTimeHours = 60.0 },
                    new() { Hash = "m2", LeadTimeHours = 95.0 },
                    new() { Hash = "m3", LeadTimeHours = 120.0 }
                }
            },
            DxIndex = new DxIndexResult
            {
                CompositeScore = 40.0,
                Rating = DxRating.Critical
            }
        };
    }
}

#endregion

/// <summary>
/// Gate 1: quality gate engine evaluates configured thresholds against metrics and reports violations.
/// CHECK: dotnet test --filter "FullyQualifiedName~QualityGateThresholdEvaluationTests"
/// </summary>
public class QualityGateThresholdEvaluationTests
{
    [Fact]
    public void EvaluateGates_AllMetricsWithinDefaultThresholds_PassesWithNoViolations()
    {
        var engine = new QualityGateEngine();
        var healthyResult = QualityGateTestDataBuilder.CreateHealthyAnalysisResult();

        var eval = engine.EvaluateGates(healthyResult);

        Assert.True(eval.IsPassed);
        Assert.Empty(eval.Violations);
        Assert.Equal(0, eval.RulesFailed);
        Assert.True(eval.RulesEvaluated >= 3);
        Assert.True(eval.RulesPassed >= 3);
        Assert.NotNull(healthyResult.QualityGate);
        Assert.True(healthyResult.QualityGate.IsPassed);
    }

    [Fact]
    public void EvaluateGates_HotspotDensityExceedsMaxThreshold_ReportsViolation()
    {
        var engine = new QualityGateEngine();
        var unhealthyResult = QualityGateTestDataBuilder.CreateUnhealthyAnalysisResult();

        // 3 of 4 files have attention score >= 60 -> hotspot density = 0.75 (max is 0.35)
        var eval = engine.EvaluateGates(unhealthyResult);

        Assert.False(eval.IsPassed);
        var violation = eval.Violations.FirstOrDefault(v => v.MetricName == "hotspot_density");
        Assert.NotNull(violation);
        Assert.Equal(GateViolationType.MaxThresholdExceeded, violation.ViolationType);
        Assert.Equal(0.75, violation.ActualValue);
        Assert.Equal(0.35, violation.ThresholdValue);
        Assert.Contains("exceeds 35%", violation.Message);
    }

    [Fact]
    public void EvaluateGates_P90LeadTimeExceedsMaxThreshold_ReportsViolation()
    {
        var engine = new QualityGateEngine();
        var unhealthyResult = QualityGateTestDataBuilder.CreateUnhealthyAnalysisResult();

        // Lead times: 60h, 95h, 120h -> P90 = 115h (max is 72h)
        var eval = engine.EvaluateGates(unhealthyResult);

        Assert.False(eval.IsPassed);
        var violation = eval.Violations.FirstOrDefault(v => v.MetricName == "p90_lead_time");
        Assert.NotNull(violation);
        Assert.Equal(GateViolationType.MaxThresholdExceeded, violation.ViolationType);
        Assert.True(violation.ActualValue > 72.0);
        Assert.Equal(72.0, violation.ThresholdValue);
        Assert.Contains("P90 lead time exceeds 72h", violation.Message);
    }

    [Fact]
    public void EvaluateGates_DxIndexBelowMinThreshold_ReportsViolation()
    {
        var engine = new QualityGateEngine();
        var unhealthyResult = QualityGateTestDataBuilder.CreateUnhealthyAnalysisResult();

        // DX index is 40.0 (min required is 55.0)
        var eval = engine.EvaluateGates(unhealthyResult);

        Assert.False(eval.IsPassed);
        var violation = eval.Violations.FirstOrDefault(v => v.MetricName == "dx_index");
        Assert.NotNull(violation);
        Assert.Equal(GateViolationType.MinThresholdBreached, violation.ViolationType);
        Assert.Equal(40.0, violation.ActualValue);
        Assert.Equal(55.0, violation.ThresholdValue);
        Assert.Contains("DX Index", violation.Message);
    }

    [Fact]
    public void EvaluateGates_PerAreaScope_EvaluatesEachAreaSeparately()
    {
        var engine = new QualityGateEngine();
        var result = new AnalysisResult
        {
            Files = new List<FileMetric>
            {
                // Area "SiloArea": 2 files, 100% single owner
                new()
                {
                    Path = "src/Silo/A.cs",
                    Area = "SiloArea",
                    ContributorCount = 1,
                    Contributors = new List<ContributorShare> { new() { Name = "Alice", ActivityShare = 1.0 } }
                },
                new()
                {
                    Path = "src/Silo/B.cs",
                    Area = "SiloArea",
                    ContributorCount = 1,
                    Contributors = new List<ContributorShare> { new() { Name = "Alice", ActivityShare = 1.0 } }
                },
                // Area "HealthyArea": 2 files, shared ownership (50/50)
                new()
                {
                    Path = "src/Healthy/C.cs",
                    Area = "HealthyArea",
                    ContributorCount = 2,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", ActivityShare = 0.5 },
                        new() { Name = "Bob", ActivityShare = 0.5 }
                    }
                },
                new()
                {
                    Path = "src/Healthy/D.cs",
                    Area = "HealthyArea",
                    ContributorCount = 2,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", ActivityShare = 0.5 },
                        new() { Name = "Bob", ActivityShare = 0.5 }
                    }
                }
            },
            Areas = new List<AreaMetric>
            {
                new() { Area = "SiloArea", FileCount = 2 },
                new() { Area = "HealthyArea", FileCount = 2 }
            }
        };

        var customRules = new List<QualityGateRule>
        {
            new()
            {
                MetricName = "single_owner_ratio",
                Scope = "per_area",
                MaxThreshold = 0.60,
                Message = "Area {area} has >60% single-owner files — pair programming required"
            }
        };

        var eval = engine.EvaluateGates(result, customRules: customRules);

        Assert.False(eval.IsPassed);
        Assert.Single(eval.Violations);

        var violation = eval.Violations[0];
        Assert.Equal("single_owner_ratio", violation.MetricName);
        Assert.Equal("SiloArea", violation.Area);
        Assert.Equal(1.0, violation.ActualValue);
        Assert.Equal(0.60, violation.ThresholdValue);
        Assert.Contains("Area SiloArea has >60% single-owner files", violation.Message);
    }

    [Fact]
    public void EvaluateGates_CustomRules_EvaluatesConfiguredRules()
    {
        var engine = new QualityGateEngine();
        var result = new AnalysisResult
        {
            Files = new List<FileMetric>
            {
                new() { Path = "File1.cs", ReworkRate = 0.40 }
            },
            TemporalCoupling = new List<TemporalCoupling>
            {
                new() { FileA = "src/A.cs", FileB = "src/B.cs", CouplingDegree = 0.8 }
            }
        };

        var customRules = new List<QualityGateRule>
        {
            new("rework_rate", scope: "global", max: 0.20, message: "Rework rate exceeds 20%"),
            new("cross_boundary_coupling_rate", scope: "global", max: 0.50, message: "Cross boundary coupling too high")
        };

        var eval = engine.EvaluateGates(result, customRules: customRules);

        Assert.False(eval.IsPassed);
        Assert.Contains(eval.Violations, v => v.MetricName == "rework_rate");
    }

    [Fact]
    public void EvaluateGates_FailFast_StopsOnFirstViolation()
    {
        var engine = new QualityGateEngine();
        var unhealthyResult = QualityGateTestDataBuilder.CreateUnhealthyAnalysisResult();

        var evalFull = engine.EvaluateGates(unhealthyResult, failFast: false);
        var evalFast = engine.EvaluateGates(unhealthyResult, failFast: true);

        Assert.True(evalFull.Violations.Count > 1, "Full evaluation should detect multiple violations.");
        Assert.Single(evalFast.Violations);
        Assert.False(evalFast.IsPassed);
    }

    [Fact]
    public void FormatTerminalSummary_OutputsPassedStatusWhenPassing()
    {
        var engine = new QualityGateEngine();
        var healthyResult = QualityGateTestDataBuilder.CreateHealthyAnalysisResult();

        var eval = engine.EvaluateGates(healthyResult);
        string summary = eval.FormatTerminalSummary();

        Assert.Contains("PASSED", summary);
        Assert.Contains("All quality gate assertions passed successfully", summary);
    }

    [Fact]
    public void FormatTerminalSummary_OutputsFailedStatusAndViolationsWhenFailing()
    {
        var engine = new QualityGateEngine();
        var unhealthyResult = QualityGateTestDataBuilder.CreateUnhealthyAnalysisResult();

        var eval = engine.EvaluateGates(unhealthyResult);
        string summary = eval.FormatTerminalSummary();

        Assert.Contains("FAILED", summary);
        Assert.Contains("hotspot_density", summary);
        Assert.Contains("p90_lead_time", summary);
    }

    [Fact]
    public void FormatGitHubAnnotations_OutputsEmptyWhenPassing()
    {
        var engine = new QualityGateEngine();
        var healthyResult = QualityGateTestDataBuilder.CreateHealthyAnalysisResult();

        var eval = engine.EvaluateGates(healthyResult);
        string annotations = eval.FormatGitHubAnnotations();

        Assert.Empty(annotations);
    }

    [Fact]
    public void FormatGitHubAnnotations_OutputsWorkflowCommandErrorsWhenFailing()
    {
        var engine = new QualityGateEngine();
        var unhealthyResult = QualityGateTestDataBuilder.CreateUnhealthyAnalysisResult();

        var eval = engine.EvaluateGates(unhealthyResult);
        string annotations = eval.FormatGitHubAnnotations();

        Assert.Contains("::error", annotations);
        Assert.Contains("title=Quality Gate:", annotations);
        Assert.Contains("hotspot_density", annotations);
    }

    [Fact]
    public void ParseRulesFromYaml_ParsesRulesCorrectly()
    {
        var engine = new QualityGateEngine();
        string yaml = @"
gates:
  - metric: hotspot_density
    scope: global
    max: 0.35
    message: 'Hotspot density exceeds 35%'
  - metric: single_owner_ratio
    scope: per_area
    max: 0.60
    message: 'Area {area} has >60% single-owner files'
  - metric: dx_index
    scope: global
    min: 55
    regression_max: 10
    message: 'DX Index dropped significantly'
";

        var rules = engine.ParseRulesFromYaml(yaml);

        Assert.Equal(3, rules.Count);
        Assert.Equal("hotspot_density", rules[0].MetricName);
        Assert.Equal("global", rules[0].Scope);
        Assert.Equal(0.35, rules[0].MaxThreshold);

        Assert.Equal("single_owner_ratio", rules[1].MetricName);
        Assert.Equal("per_area", rules[1].Scope);
        Assert.Equal(0.60, rules[1].MaxThreshold);

        Assert.Equal("dx_index", rules[2].MetricName);
        Assert.Equal(55.0, rules[2].MinThreshold);
        Assert.Equal(10.0, rules[2].MaxRegressionAllowed);
    }
}

/// <summary>
/// Gate 2: gate CLI exits with code 1 on failed assertions and formats GitHub annotations.
/// CHECK: dotnet test --filter "FullyQualifiedName~QualityGateCliExitCodeTests"
/// </summary>
public class QualityGateCliExitCodeTests
{
    [Fact]
    public async Task GateCli_AllAssertionsPass_ExitsWithCode0()
    {
        var healthyResult = QualityGateTestDataBuilder.CreateHealthyAnalysisResult();
        var analyzer = new FakeRepositoryAnalyzerForGate(healthyResult);
        var reporter = new GateTestConsoleReporter();

        var command = new GateCommand(new[] { "gate" }, analyzer: analyzer);
        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("PASSED", reporter.Output);
    }

    [Fact]
    public async Task GateCli_GateAssertionViolated_ExitsWithCode1()
    {
        var unhealthyResult = QualityGateTestDataBuilder.CreateUnhealthyAnalysisResult();
        var analyzer = new FakeRepositoryAnalyzerForGate(unhealthyResult);
        var reporter = new GateTestConsoleReporter();

        var command = new GateCommand(new[] { "gate" }, analyzer: analyzer);
        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("FAILED", reporter.Output);
        Assert.Contains("hotspot_density", reporter.Output);
    }

    [Fact]
    public async Task GateCli_GitHubAnnotationsFormat_WritesErrorAnnotationsAndExitsCode1()
    {
        var unhealthyResult = QualityGateTestDataBuilder.CreateUnhealthyAnalysisResult();
        var analyzer = new FakeRepositoryAnalyzerForGate(unhealthyResult);
        var reporter = new GateTestConsoleReporter();

        var command = new GateCommand(new[] { "gate", "--format", "github-annotations" }, analyzer: analyzer);
        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(1, result.ExitCode);
        string stdout = reporter.Output;
        Assert.Contains("::error", stdout);
        Assert.Contains("title=Quality Gate:", stdout);
        Assert.Contains("Hotspot density exceeds 35%", stdout);
    }

    [Fact]
    public async Task GateCli_JsonFormat_WritesValidJsonAndExitsCode1OnViolation()
    {
        var unhealthyResult = QualityGateTestDataBuilder.CreateUnhealthyAnalysisResult();
        var analyzer = new FakeRepositoryAnalyzerForGate(unhealthyResult);
        var reporter = new GateTestConsoleReporter();

        var command = new GateCommand(new[] { "gate", "--format", "json" }, analyzer: analyzer);
        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(1, result.ExitCode);
        using var doc = JsonDocument.Parse(reporter.Output);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("is_passed").GetBoolean());
        Assert.True(root.GetProperty("violations").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GateCli_JsonFlag_OutputsJsonAndExitsCode0WhenHealthy()
    {
        var healthyResult = QualityGateTestDataBuilder.CreateHealthyAnalysisResult();
        var analyzer = new FakeRepositoryAnalyzerForGate(healthyResult);
        var reporter = new GateTestConsoleReporter();

        var command = new GateCommand(new[] { "gate", "--json" }, analyzer: analyzer);
        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(0, result.ExitCode);
        using var doc = JsonDocument.Parse(reporter.Output);
        Assert.True(doc.RootElement.GetProperty("is_passed").GetBoolean());
    }

    [Fact]
    public async Task GateCli_NonExistentBaseline_ExitsWithCode2()
    {
        var analyzer = new FakeRepositoryAnalyzerForGate();
        var reporter = new GateTestConsoleReporter();

        var command = new GateCommand(new[] { "gate", "--baseline", "missing_baseline_snapshot_xyz.json" }, analyzer: analyzer);
        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Operational error", reporter.Error);
    }

    [Fact]
    public async Task GateCli_BaselineLatestWithNoBaselines_ExitsWithCode2()
    {
        string emptyTempDir = Path.Combine(Path.GetTempPath(), "gate_test_empty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyTempDir);
        try
        {
            var analyzer = new FakeRepositoryAnalyzerForGate();
            var reporter = new GateTestConsoleReporter();

            var command = new GateCommand(new[] { "gate", "--baseline", "latest", "--storage", emptyTempDir }, analyzer: analyzer);
            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("No baseline snapshots found", reporter.Error);
        }
        finally
        {
            if (Directory.Exists(emptyTempDir)) Directory.Delete(emptyTempDir, true);
        }
    }

    [Fact]
    public async Task GateCli_FailFast_TerminatesOnFirstViolation()
    {
        var unhealthyResult = QualityGateTestDataBuilder.CreateUnhealthyAnalysisResult();
        var analyzer = new FakeRepositoryAnalyzerForGate(unhealthyResult);
        var reporter = new GateTestConsoleReporter();

        var command = new GateCommand(new[] { "gate", "--fail-fast", "--format", "json" }, analyzer: analyzer);
        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(1, result.ExitCode);
        using var doc = JsonDocument.Parse(reporter.Output);
        Assert.Equal(1, doc.RootElement.GetProperty("violations").GetArrayLength());
    }

    [Fact]
    public async Task GateCli_CustomConfigFile_LoadsAndEvaluatesConfiguredGates()
    {
        string tempConfig = Path.Combine(Path.GetTempPath(), "test_gitic_gates_" + Guid.NewGuid().ToString("N") + ".yml");
        await File.WriteAllTextAsync(tempConfig, @"
gates:
  - metric: rework_rate
    scope: global
    max: 0.02
    message: 'Rework rate exceeds 2%'
");

        try
        {
            var healthyResult = QualityGateTestDataBuilder.CreateHealthyAnalysisResult(); // Rework rate is 0.05 > 0.02
            var analyzer = new FakeRepositoryAnalyzerForGate(healthyResult);
            var reporter = new GateTestConsoleReporter();

            var command = new GateCommand(new[] { "gate", "--config", tempConfig }, analyzer: analyzer);
            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Rework rate exceeds 2%", reporter.Output);
        }
        finally
        {
            if (File.Exists(tempConfig)) File.Delete(tempConfig);
        }
    }

    [Fact]
    public void GateCli_CommandLineParser_ParsesGateCommandOptions()
    {
        var parser = new CommandLineParser(new[] { "gate", "--baseline", "latest", "--format", "github-annotations", "--fail-fast" });
        var parsed = parser.Parse();

        Assert.Equal("gate", parsed.Command);
        Assert.Equal("latest", parsed.GateBaseline);
        Assert.Equal("github-annotations", parsed.GateFormat);
        Assert.True(parsed.GateFailFast);
    }
}

/// <summary>
/// Gate 3: baseline regression assertions detect deteriorating metrics compared against previous snapshot.
/// CHECK: dotnet test --filter "FullyQualifiedName~QualityGateBaselineRegressionTests"
/// </summary>
public class QualityGateBaselineRegressionTests
{
    [Fact]
    public void BaselineRegression_DxIndexDropExceedsMaxRegression_ReportsViolation()
    {
        var engine = new QualityGateEngine();

        // Baseline had DX Index 80.0
        var baseline = new BaselineSnapshot
        {
            Id = "base-01",
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["dx_index"] = 80.0
            }
        };

        // Current has DX Index 65.0 (drop of 15.0 pts, exceeds max allowed regression of 10.0 pts)
        var current = new AnalysisResult
        {
            DxIndex = new DxIndexResult { CompositeScore = 65.0, Rating = DxRating.Watch },
            Files = new List<FileMetric>(),
            Areas = new List<AreaMetric>()
        };

        var eval = engine.EvaluateGates(current, baseline: baseline);

        Assert.False(eval.IsPassed);
        var violation = eval.Violations.FirstOrDefault(v => v.MetricName == "dx_index");
        Assert.NotNull(violation);
        Assert.Equal(GateViolationType.RegressionExceeded, violation.ViolationType);
        Assert.Equal(65.0, violation.ActualValue);
        Assert.Equal(80.0, violation.BaselineValue);
        Assert.Equal(15.0, violation.RegressionValue);
        Assert.Equal(10.0, violation.ThresholdValue);
        Assert.Contains("DX Index dropped significantly", violation.Message);
    }

    [Fact]
    public void BaselineRegression_DxIndexDropWithinMaxRegression_Passes()
    {
        var engine = new QualityGateEngine();

        // Baseline had DX Index 80.0
        var baseline = new BaselineSnapshot
        {
            Id = "base-01",
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["dx_index"] = 80.0
            }
        };

        // Current has DX Index 75.0 (drop of 5.0 pts <= 10.0 pts allowed regression)
        var current = new AnalysisResult
        {
            DxIndex = new DxIndexResult { CompositeScore = 75.0, Rating = DxRating.Healthy },
            Files = new List<FileMetric>(),
            Areas = new List<AreaMetric>()
        };

        var eval = engine.EvaluateGates(current, baseline: baseline);

        Assert.True(eval.IsPassed);
        Assert.Empty(eval.Violations);
    }

    [Fact]
    public void BaselineRegression_DxIndexImproves_Passes()
    {
        var engine = new QualityGateEngine();

        var baseline = new BaselineSnapshot
        {
            Id = "base-01",
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["dx_index"] = 60.0
            }
        };

        var current = new AnalysisResult
        {
            DxIndex = new DxIndexResult { CompositeScore = 80.0, Rating = DxRating.Healthy },
            Files = new List<FileMetric>(),
            Areas = new List<AreaMetric>()
        };

        var eval = engine.EvaluateGates(current, baseline: baseline);

        Assert.True(eval.IsPassed);
        Assert.Empty(eval.Violations);
    }

    [Fact]
    public void BaselineRegression_LowerIsBetter_HotspotDensityRegression_ReportsViolation()
    {
        var engine = new QualityGateEngine();

        var baseline = new BaselineSnapshot
        {
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["hotspot_density"] = 0.10
            }
        };

        // Current has 1 of 4 files as hotspot = 0.25 (regression of 0.15 vs baseline 0.10)
        var current = new AnalysisResult
        {
            Files = new List<FileMetric>
            {
                new() { Path = "H1.cs", AttentionScore = 80.0 },
                new() { Path = "N1.cs", AttentionScore = 20.0 },
                new() { Path = "N2.cs", AttentionScore = 20.0 },
                new() { Path = "N3.cs", AttentionScore = 20.0 }
            }
        };

        var customRules = new List<QualityGateRule>
        {
            new()
            {
                MetricName = "hotspot_density",
                Scope = "global",
                MaxRegressionAllowed = 0.05,
                Message = "Hotspot density regressed more than 5%"
            }
        };

        var eval = engine.EvaluateGates(current, baseline: baseline, customRules: customRules);

        Assert.False(eval.IsPassed);
        var violation = eval.Violations.FirstOrDefault(v => v.MetricName == "hotspot_density");
        Assert.NotNull(violation);
        Assert.Equal(GateViolationType.RegressionExceeded, violation.ViolationType);
        Assert.Equal(0.25, violation.ActualValue);
        Assert.Equal(0.10, violation.BaselineValue);
        Assert.Equal(0.15, violation.RegressionValue);
    }

    [Fact]
    public void BaselineRegression_PerArea_DetectsAreaRegression()
    {
        var engine = new QualityGateEngine();

        var baseline = new BaselineSnapshot();
        baseline.Areas["Core"] = new AreaBaselineMetrics
        {
            Area = "Core",
            ReworkRate = 0.08,
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["rework_rate"] = 0.08
            }
        };

        var current = new AnalysisResult
        {
            Areas = new List<AreaMetric>
            {
                new() { Area = "Core", ReworkRate = 0.22 }
            },
            Files = new List<FileMetric>
            {
                new() { Path = "Core/F1.cs", Area = "Core", ReworkRate = 0.22 }
            }
        };

        var customRules = new List<QualityGateRule>
        {
            new()
            {
                MetricName = "rework_rate",
                Scope = "per_area",
                MaxRegressionAllowed = 0.05,
                Message = "Area {area} rework rate regressed beyond 5%"
            }
        };

        var eval = engine.EvaluateGates(current, baseline: baseline, customRules: customRules);

        Assert.False(eval.IsPassed);
        Assert.Single(eval.Violations);
        var v = eval.Violations[0];
        Assert.Equal("rework_rate", v.MetricName);
        Assert.Equal("Core", v.Area);
        Assert.Equal(GateViolationType.RegressionExceeded, v.ViolationType);
        Assert.Equal(0.22, v.ActualValue);
        Assert.Equal(0.08, v.BaselineValue);
        Assert.Equal(0.14, Math.Round(v.RegressionValue!.Value, 2));
        Assert.Contains("Area Core rework rate regressed", v.Message);
    }

    [Fact]
    public void BaselineRegression_NoBaselineProvided_DoesNotReportRegressionViolation()
    {
        var engine = new QualityGateEngine();
        var current = new AnalysisResult
        {
            DxIndex = new DxIndexResult { CompositeScore = 60.0, Rating = DxRating.Watch },
            Files = new List<FileMetric>(),
            Areas = new List<AreaMetric>()
        };

        var rules = new List<QualityGateRule>
        {
            new("dx_index", scope: "global", min: 55.0, maxRegression: 5.0)
        };

        var eval = engine.EvaluateGates(current, baseline: null, customRules: rules);

        // Since min is 55.0 and current is 60.0, and baseline is null, it passes
        Assert.True(eval.IsPassed);
        Assert.Empty(eval.Violations);
    }

    [Fact]
    public async Task GateCli_WithBaselineOption_EvaluatesRegressionAndExitsCode1()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "gate_baseline_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string baselinePath = Path.Combine(tempDir, "base.json");

        try
        {
            var baseline = new BaselineSnapshot
            {
                Id = "base-001",
                Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["dx_index"] = 85.0
                }
            };
            await File.WriteAllTextAsync(baselinePath, JsonSerializer.Serialize(baseline, JsonSerializationDefaults.Indented));

            var current = new AnalysisResult
            {
                DxIndex = new DxIndexResult { CompositeScore = 60.0, Rating = DxRating.Watch },
                Files = new List<FileMetric>(),
                Areas = new List<AreaMetric>()
            };

            var analyzer = new FakeRepositoryAnalyzerForGate(current);
            var reporter = new GateTestConsoleReporter();

            var command = new GateCommand(new[] { "gate", "--baseline", baselinePath }, analyzer: analyzer);
            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("FAILED", reporter.Output);
            Assert.Contains("dx_index", reporter.Output);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
