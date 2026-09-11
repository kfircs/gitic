using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kfc.Cli.Core;
using Xunit;

namespace Gitic.Tests;

#region Test Helpers

internal class MockConsoleReporter : IConsoleReporter
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

internal class FakeGitClientForImpact : IGitClient
{
    public string RepoRoot { get; set; } = "/repo";
    public List<string> StagedFiles { get; set; } = new();
    public HashSet<string> HeadFiles { get; set; } = new();
    public List<GitCommitRecord> History { get; set; } = new();

    public Task<string?> GetRepositoryRootAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(RepoRoot);

    public Task<HashSet<string>> ListHeadFilesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(HeadFiles);

    public Task<List<GitCommitRecord>> ExtractHistoryAsync(GitHistoryExtractorOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(History);

    public Task<List<string>> GetStagedFilesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<string>(StagedFiles));
}

internal class FakeRepositoryAnalyzerForImpact : IRepositoryAnalyzer
{
    public AnalysisResult ResultToReturn { get; set; }

    public FakeRepositoryAnalyzerForImpact(AnalysisResult? result = null)
    {
        ResultToReturn = result ?? new AnalysisResult();
    }

    public Task<AnalysisResult> AnalyzeAsync(AnalyzeInput input, CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultToReturn);
}

internal static class ImpactTestDataBuilder
{
    public static AnalysisResult CreateStandardAnalysisResult()
    {
        return new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                RepoRoot = "/repo",
                Command = AnalysisCommand.Impact,
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                CommitCount = 100
            },
            TemporalCoupling = new List<TemporalCoupling>
            {
                new() { FileA = "src/Core/Types.cs", FileB = "src/Core/Scoring.cs", SharedCommits = 32, CouplingDegree = 0.78 },
                new() { FileA = "src/Core/AnalysisPipeline.cs", FileB = "src/Core/Types.cs", SharedCommits = 28, CouplingDegree = 0.71 },
                new() { FileA = "src/Core/Types.cs", FileB = "src/Reporting/JsonRenderer.cs", SharedCommits = 22, CouplingDegree = 0.65 },
                new() { FileA = "src/Core/Types.cs", FileB = "tests/Gitic.Tests/PortedModulesTests.cs", SharedCommits = 10, CouplingDegree = 0.45 },
                new() { FileA = "src/Cli/TuiNode.cs", FileB = "src/Core/Types.cs", SharedCommits = 8, CouplingDegree = 0.38 },
                new() { FileA = "src/Core/Types.cs", FileB = "src/Core/OldHelper.cs", SharedCommits = 2, CouplingDegree = 0.15 }
            },
            Files = new List<FileMetric>
            {
                new()
                {
                    Path = "src/Core/Types.cs",
                    Area = "src/Core",
                    Touches = 50,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", Email = "alice@example.com", Activity = 40, ActivityShare = 0.80 }
                    }
                },
                new()
                {
                    Path = "src/Core/Scoring.cs",
                    Area = "src/Core",
                    Touches = 45,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", Email = "alice@example.com", Activity = 37, ActivityShare = 0.83 }
                    }
                },
                new()
                {
                    Path = "src/Core/AnalysisPipeline.cs",
                    Area = "src/Core",
                    Touches = 40,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Bob", Email = "bob@example.com", Activity = 30, ActivityShare = 0.75 }
                    }
                },
                new()
                {
                    Path = "src/Reporting/JsonRenderer.cs",
                    Area = "src/Reporting",
                    Touches = 30,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Carol", Email = "carol@example.com", Activity = 20, ActivityShare = 0.67 }
                    }
                },
                new()
                {
                    Path = "tests/Gitic.Tests/PortedModulesTests.cs",
                    Area = "tests/Gitic.Tests",
                    Touches = 25,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Dave", Email = "dave@example.com", Activity = 18, ActivityShare = 0.72 }
                    }
                },
                new()
                {
                    Path = "src/Cli/TuiNode.cs",
                    Area = "src/Cli",
                    Touches = 20,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Eve", Email = "eve@example.com", Activity = 15, ActivityShare = 0.75 }
                    }
                },
                new()
                {
                    Path = "src/Core/OldHelper.cs",
                    Area = "src/Core",
                    Touches = 5,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Frank", Email = "frank@example.com", Activity = 5, ActivityShare = 1.0 }
                    }
                }
            },
            Areas = new List<AreaMetric>
            {
                new() { Area = "src/Core", FileCount = 4 },
                new() { Area = "src/Reporting", FileCount = 1 },
                new() { Area = "tests/Gitic.Tests", FileCount = 1 },
                new() { Area = "src/Cli", FileCount = 1 }
            }
        };
    }
}

#endregion

/// <summary>
/// Gate 1: impact predictor returns ranked co-change probability list for given input file.
/// CHECK: dotnet test --filter "FullyQualifiedName~ImpactPredictionRankingTests"
/// </summary>
public class ImpactPredictionRankingTests
{
    [Fact]
    public void PredictImpact_ReturnsRankedCoChangeProbabilityList_ForGivenInputFile()
    {
        var predictor = new ImpactPredictor();
        var analysisResult = ImpactTestDataBuilder.CreateStandardAnalysisResult();

        var report = predictor.PredictImpact(analysisResult, "src/Core/Types.cs");

        Assert.NotNull(report);
        Assert.Single(report.TargetFiles);
        Assert.Equal("src/Core/Types.cs", report.TargetFiles[0]);
        Assert.Equal(6, report.Predictions.Count);

        // Verify strictly descending order of CouplingDegree
        for (int i = 0; i < report.Predictions.Count - 1; i++)
        {
            Assert.True(report.Predictions[i].CouplingDegree >= report.Predictions[i + 1].CouplingDegree,
                $"Item {i} ({report.Predictions[i].CouplingDegree}) should be >= item {i + 1} ({report.Predictions[i + 1].CouplingDegree})");
        }

        // Check exact ranking
        Assert.Equal("src/Core/Scoring.cs", report.Predictions[0].File);
        Assert.Equal(0.78, report.Predictions[0].CouplingDegree);
        Assert.Equal(32, report.Predictions[0].SharedCommits);

        Assert.Equal("src/Core/AnalysisPipeline.cs", report.Predictions[1].File);
        Assert.Equal(0.71, report.Predictions[1].CouplingDegree);
        Assert.Equal(28, report.Predictions[1].SharedCommits);

        Assert.Equal("src/Reporting/JsonRenderer.cs", report.Predictions[2].File);
        Assert.Equal(0.65, report.Predictions[2].CouplingDegree);

        Assert.Equal("tests/Gitic.Tests/PortedModulesTests.cs", report.Predictions[3].File);
        Assert.Equal(0.45, report.Predictions[3].CouplingDegree);

        Assert.Equal("src/Cli/TuiNode.cs", report.Predictions[4].File);
        Assert.Equal(0.38, report.Predictions[4].CouplingDegree);

        Assert.Equal("src/Core/OldHelper.cs", report.Predictions[5].File);
        Assert.Equal(0.15, report.Predictions[5].CouplingDegree);
    }

    [Fact]
    public void PredictImpact_CategorizesCouplingProbabilities_IntoHighModerateAndLow()
    {
        var predictor = new ImpactPredictor();
        var analysisResult = ImpactTestDataBuilder.CreateStandardAnalysisResult();

        var report = predictor.PredictImpact(analysisResult, "src/Core/Types.cs");

        // High: > 60% (0.78, 0.71, 0.65) -> 3 items
        Assert.Equal(3, report.HighProbabilityPredictions.Count);
        Assert.All(report.HighProbabilityPredictions, p => Assert.Equal(CouplingProbabilityLevel.High, p.ProbabilityLevel));
        Assert.All(report.HighProbabilityPredictions, p => Assert.True(p.CouplingDegree > 0.60));
        Assert.True(report.HasHighImpact);

        // Moderate: 30-60% (0.45, 0.38) -> 2 items
        Assert.Equal(2, report.ModerateProbabilityPredictions.Count);
        Assert.All(report.ModerateProbabilityPredictions, p => Assert.Equal(CouplingProbabilityLevel.Moderate, p.ProbabilityLevel));
        Assert.All(report.ModerateProbabilityPredictions, p => Assert.True(p.CouplingDegree >= 0.30 && p.CouplingDegree <= 0.60));

        // Low: < 30% (0.15) -> 1 item
        Assert.Single(report.LowProbabilityPredictions);
        Assert.Equal(CouplingProbabilityLevel.Low, report.LowProbabilityPredictions[0].ProbabilityLevel);
        Assert.True(report.LowProbabilityPredictions[0].CouplingDegree < 0.30);
    }

    [Fact]
    public void PredictImpact_SecondarySortsBySharedCommits_WhenCouplingDegreeTied()
    {
        var predictor = new ImpactPredictor();
        var analysisResult = new AnalysisResult
        {
            TemporalCoupling = new List<TemporalCoupling>
            {
                new() { FileA = "src/A.cs", FileB = "src/B.cs", SharedCommits = 10, CouplingDegree = 0.50 },
                new() { FileA = "src/A.cs", FileB = "src/C.cs", SharedCommits = 25, CouplingDegree = 0.50 }
            }
        };

        var report = predictor.PredictImpact(analysisResult, "src/A.cs");

        Assert.Equal(2, report.Predictions.Count);
        Assert.Equal("src/C.cs", report.Predictions[0].File);
        Assert.Equal(25, report.Predictions[0].SharedCommits);
        Assert.Equal("src/B.cs", report.Predictions[1].File);
        Assert.Equal(10, report.Predictions[1].SharedCommits);
    }

    [Fact]
    public void PredictImpact_IdentifiesTopContributorForCoupledFiles_AndBuildsCoordinationList()
    {
        var predictor = new ImpactPredictor();
        var analysisResult = ImpactTestDataBuilder.CreateStandardAnalysisResult();

        var report = predictor.PredictImpact(analysisResult, "src/Core/Types.cs", warnThreshold: 0.5);

        var scoring = report.Predictions.First(p => p.File == "src/Core/Scoring.cs");
        Assert.Equal("Alice", scoring.TopContributor);
        Assert.Equal("alice@example.com", scoring.TopContributorEmail);
        Assert.Equal(0.83, scoring.TopContributorShare);

        var pipeline = report.Predictions.First(p => p.File == "src/Core/AnalysisPipeline.cs");
        Assert.Equal("Bob", pipeline.TopContributor);
        Assert.Equal("bob@example.com", pipeline.TopContributorEmail);

        // Owner coordinations should identify top contributors of heavily coupled files
        Assert.NotEmpty(report.OwnerCoordinations);
        Assert.Contains(report.OwnerCoordinations, o => o.Owner == "Alice" && o.Files.Contains("src/Core/Scoring.cs"));
        Assert.Contains(report.OwnerCoordinations, o => o.Owner == "Bob" && o.Files.Contains("src/Core/AnalysisPipeline.cs"));
    }

    [Fact]
    public void PredictImpact_HandlesEmptyOrNullCouplings_Gracefully()
    {
        var predictor = new ImpactPredictor();

        var reportNull = predictor.PredictImpact(new AnalysisResult(), "src/Unknown.cs");
        Assert.Empty(reportNull.Predictions);
        Assert.False(reportNull.HasHighImpact);
        Assert.False(reportNull.HasCrossBoundaryLeak);

        var reportEmpty = predictor.PredictImpact(new AnalysisResult { TemporalCoupling = new List<TemporalCoupling>() }, "src/Unknown.cs");
        Assert.Empty(reportEmpty.Predictions);
    }

    [Fact]
    public void PredictImpact_HandlesPathNormalization_AndRelativePaths()
    {
        var predictor = new ImpactPredictor();
        var analysisResult = ImpactTestDataBuilder.CreateStandardAnalysisResult();

        // Forward slash with leading ./
        var reportRel = predictor.PredictImpact(analysisResult, "./src/Core/Types.cs");
        Assert.Equal(6, reportRel.Predictions.Count);

        // Windows style backslashes
        var reportBackslash = predictor.PredictImpact(analysisResult, "src\\Core\\Types.cs");
        Assert.Equal(6, reportBackslash.Predictions.Count);

        // Filename only
        var reportNameOnly = predictor.PredictImpact(analysisResult, "Types.cs");
        Assert.Equal(6, reportNameOnly.Predictions.Count);
    }

    [Fact]
    public void PredictImpact_MultipleTargetFiles_AggregatesRankedPredictions()
    {
        var predictor = new ImpactPredictor();
        var analysisResult = new AnalysisResult
        {
            TemporalCoupling = new List<TemporalCoupling>
            {
                new() { FileA = "src/A.cs", FileB = "src/Shared1.cs", SharedCommits = 15, CouplingDegree = 0.80 },
                new() { FileA = "src/B.cs", FileB = "src/Shared2.cs", SharedCommits = 12, CouplingDegree = 0.60 }
            }
        };

        var report = predictor.PredictImpact(analysisResult, new[] { "src/A.cs", "src/B.cs" });

        Assert.Equal(2, report.TargetFiles.Count);
        Assert.Equal(2, report.Predictions.Count);
        Assert.Equal("src/Shared1.cs", report.Predictions[0].File);
        Assert.Equal("src/Shared2.cs", report.Predictions[1].File);
    }

    [Fact]
    public void RenderHumanReport_OutputsBoxedFormat_WithRequiredSections()
    {
        var predictor = new ImpactPredictor();
        var analysisResult = ImpactTestDataBuilder.CreateStandardAnalysisResult();

        var report = predictor.PredictImpact(analysisResult, "src/Core/Types.cs");
        string humanOutput = report.RenderHumanReport();

        Assert.Contains("Impact Analysis: src/Core/Types.cs", humanOutput);
        Assert.Contains("HIGH PROBABILITY CO-CHANGES (>60% coupling):", humanOutput);
        Assert.Contains("src/Core/Scoring.cs", humanOutput);
        Assert.Contains("78% (32 shared)", humanOutput);
        Assert.Contains("MODERATE PROBABILITY (30-60%):", humanOutput);
        Assert.Contains("tests/Gitic.Tests/PortedModulesTests.cs", humanOutput);
        Assert.Contains("CROSS-BOUNDARY ALERT:", humanOutput);
        Assert.Contains("OWNER COORDINATION NEEDED:", humanOutput);
    }
}

/// <summary>
/// Gate 2: impact CLI detects staged git changes and flags cross-boundary coupling alerts.
/// CHECK: dotnet test --filter "FullyQualifiedName~ImpactCliStagedDetectionTests"
/// </summary>
public class ImpactCliStagedDetectionTests
{
    [Fact]
    public async Task ImpactCli_StagedMode_DetectsStagedFiles_AndPredictsImpact()
    {
        var analysisResult = ImpactTestDataBuilder.CreateStandardAnalysisResult();
        var fakeGit = new FakeGitClientForImpact
        {
            RepoRoot = "/repo",
            StagedFiles = new List<string> { "src/Core/Types.cs" }
        };
        var fakeAnalyzer = new FakeRepositoryAnalyzerForImpact(analysisResult);
        var reporter = new MockConsoleReporter();

        var command = new ImpactCommand(
            new[] { "--staged" },
            predictor: new ImpactPredictor(),
            analyzer: fakeAnalyzer,
            gitClient: fakeGit);

        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(0, result.ExitCode);
        string stdout = reporter.Output;
        Assert.Contains("src/Core/Types.cs", stdout);
        Assert.Contains("src/Core/Scoring.cs", stdout);
        Assert.Contains("78%", stdout);
    }

    [Fact]
    public async Task ImpactCli_FlagsCrossBoundaryCouplingAlerts_WhenCoupledFilesDifferInModule()
    {
        var analysisResult = ImpactTestDataBuilder.CreateStandardAnalysisResult();
        var fakeGit = new FakeGitClientForImpact
        {
            StagedFiles = new List<string> { "src/Core/Types.cs" }
        };
        var fakeAnalyzer = new FakeRepositoryAnalyzerForImpact(analysisResult);
        var reporter = new MockConsoleReporter();

        var command = new ImpactCommand(
            new[] { "--staged" },
            predictor: new ImpactPredictor(),
            analyzer: fakeAnalyzer,
            gitClient: fakeGit);

        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(0, result.ExitCode);
        string stdout = reporter.Output;

        // Cross-boundary alert should detect that 3 coupled files are in different areas (Reporting, tests, Cli)
        Assert.Contains("CROSS-BOUNDARY ALERT:", stdout);
        Assert.Contains("coupled files are in different modules", stdout);
        Assert.Contains("leaking across boundaries", stdout);
    }

    [Fact]
    public async Task ImpactCli_PredictorDirect_FlagsCrossBoundaryAlertProperly()
    {
        var predictor = new ImpactPredictor();
        var analysisResult = ImpactTestDataBuilder.CreateStandardAnalysisResult();

        var report = predictor.PredictImpact(analysisResult, "src/Core/Types.cs");

        Assert.True(report.HasCrossBoundaryLeak);
        Assert.Single(report.CrossBoundaryAlerts);

        var alert = report.CrossBoundaryAlerts[0];
        Assert.Equal("src/Core/Types.cs", alert.TargetFile);
        Assert.Equal("src/Core", alert.TargetArea);
        Assert.Equal(3, alert.CrossBoundaryCount); // JsonRenderer (Reporting), PortedModulesTests (tests), TuiNode (Cli)
        Assert.Equal(6, alert.TotalCoupledCount);
        Assert.Contains("src/Reporting", alert.CrossBoundaryAreas);
        Assert.Contains("src/Reporting/JsonRenderer.cs", alert.CrossBoundaryFiles);
    }

    [Fact]
    public async Task ImpactCli_NoStagedFiles_OutputsGracefulMessageAndExitsZero()
    {
        var fakeGit = new FakeGitClientForImpact
        {
            StagedFiles = new List<string>() // Empty staged files
        };
        var fakeAnalyzer = new FakeRepositoryAnalyzerForImpact();
        var reporter = new MockConsoleReporter();

        var command = new ImpactCommand(
            new[] { "--staged" },
            predictor: new ImpactPredictor(),
            analyzer: fakeAnalyzer,
            gitClient: fakeGit);

        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No staged files detected", reporter.Output);
    }

    [Fact]
    public async Task ImpactCli_JsonOutput_EmitsValidJsonReport_WithAlertsAndPredictions()
    {
        var analysisResult = ImpactTestDataBuilder.CreateStandardAnalysisResult();
        var fakeGit = new FakeGitClientForImpact
        {
            StagedFiles = new List<string> { "src/Core/Types.cs" }
        };
        var fakeAnalyzer = new FakeRepositoryAnalyzerForImpact(analysisResult);
        var reporter = new MockConsoleReporter();

        var command = new ImpactCommand(
            new[] { "--staged", "--json" },
            predictor: new ImpactPredictor(),
            analyzer: fakeAnalyzer,
            gitClient: fakeGit);

        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(0, result.ExitCode);
        using var jsonDoc = JsonDocument.Parse(reporter.Output);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("target_files", out var targetFilesElem));
        Assert.Equal("src/Core/Types.cs", targetFilesElem[0].GetString());

        Assert.True(root.TryGetProperty("predictions", out var predictionsElem));
        Assert.Equal(6, predictionsElem.GetArrayLength());

        Assert.True(root.TryGetProperty("cross_boundary_alerts", out var alertsElem));
        Assert.True(alertsElem.GetArrayLength() > 0);

        Assert.True(root.TryGetProperty("owner_coordinations", out var ownersElem));
        Assert.True(ownersElem.GetArrayLength() > 0);
    }

    [Fact]
    public async Task ImpactCli_QuietMode_OutputsConciseWarningForGitHook()
    {
        var analysisResult = ImpactTestDataBuilder.CreateStandardAnalysisResult();
        var fakeGit = new FakeGitClientForImpact
        {
            StagedFiles = new List<string> { "src/Core/Types.cs" }
        };
        var fakeAnalyzer = new FakeRepositoryAnalyzerForImpact(analysisResult);
        var reporter = new MockConsoleReporter();

        var command = new ImpactCommand(
            new[] { "--staged", "--quiet", "--warn-threshold", "0.6" },
            predictor: new ImpactPredictor(),
            analyzer: fakeAnalyzer,
            gitClient: fakeGit);

        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(0, result.ExitCode);
        string stdout = reporter.Output;

        Assert.Contains("⚠️ Your staged changes touch Types.cs", stdout);
        Assert.Contains("frequently co-change with it", stdout);
        Assert.Contains("gitic impact src/Core/Types.cs", stdout);
    }

    [Fact]
    public async Task ImpactCli_SpecifiedFile_DirectAnalysisWithoutStaged()
    {
        var analysisResult = ImpactTestDataBuilder.CreateStandardAnalysisResult();
        var fakeGit = new FakeGitClientForImpact();
        var fakeAnalyzer = new FakeRepositoryAnalyzerForImpact(analysisResult);
        var reporter = new MockConsoleReporter();

        var command = new ImpactCommand(
            new[] { "src/Core/Types.cs" },
            predictor: new ImpactPredictor(),
            analyzer: fakeAnalyzer,
            gitClient: fakeGit);

        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("src/Core/Scoring.cs", reporter.Output);
    }

    [Fact]
    public async Task ImpactCli_NoFilesAndNoStaged_ReturnsUsageError()
    {
        var fakeGit = new FakeGitClientForImpact();
        var fakeAnalyzer = new FakeRepositoryAnalyzerForImpact();
        var reporter = new MockConsoleReporter();

        var command = new ImpactCommand(
            Array.Empty<string>(),
            predictor: new ImpactPredictor(),
            analyzer: fakeAnalyzer,
            gitClient: fakeGit);

        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("No target files specified", reporter.Error);
    }
}

/// <summary>
/// Gate 3: git pre-commit hook installer generates working hook triggering impact analysis.
/// CHECK: dotnet test --filter "FullyQualifiedName~ImpactHookInstallationTests"
/// </summary>
public class ImpactHookInstallationTests
{
    [Fact]
    public async Task ImpactCli_InstallHook_WritesExecutablePreCommitHook_InGitHooksDir()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "gitic_hook_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string gitDir = Path.Combine(tempDir, ".git");
        Directory.CreateDirectory(gitDir);

        try
        {
            var fakeGit = new FakeGitClientForImpact { RepoRoot = tempDir };
            var reporter = new MockConsoleReporter();

            var command = new ImpactCommand(
                new[] { "--install-hook", "--repo", tempDir },
                predictor: new ImpactPredictor(),
                analyzer: new FakeRepositoryAnalyzerForImpact(),
                gitClient: fakeGit);

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Successfully installed git pre-commit hook", reporter.Output);

            string hookPath = Path.Combine(gitDir, "hooks", "pre-commit");
            Assert.True(File.Exists(hookPath), $"Expected hook file at {hookPath} to exist.");

            string hookContent = await File.ReadAllTextAsync(hookPath);
            Assert.Contains("#!/bin/sh", hookContent);
            Assert.Contains("gitic impact --staged --warn-threshold 0.5", hookContent);

            if (!OperatingSystem.IsWindows())
            {
                var mode = File.GetUnixFileMode(hookPath);
                Assert.True(mode.HasFlag(UnixFileMode.UserExecute), "Hook file should have UserExecute permission.");
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ImpactCli_InstallHook_CreatesHooksDir_WhenMissing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "gitic_hook_missing_hooks_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string gitDir = Path.Combine(tempDir, ".git");
        Directory.CreateDirectory(gitDir);
        // Specifically do NOT create "hooks" dir

        try
        {
            var fakeGit = new FakeGitClientForImpact { RepoRoot = tempDir };
            var reporter = new MockConsoleReporter();

            var command = new ImpactCommand(
                new[] { "--install-hook", "--repo", tempDir },
                gitClient: fakeGit);

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            string hookPath = Path.Combine(gitDir, "hooks", "pre-commit");
            Assert.True(File.Exists(hookPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ImpactCli_InstallHook_PreservesCustomWarnThreshold()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "gitic_hook_custom_threshold_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, ".git"));

        try
        {
            var fakeGit = new FakeGitClientForImpact { RepoRoot = tempDir };
            var reporter = new MockConsoleReporter();

            var command = new ImpactCommand(
                new[] { "--install-hook", "--warn-threshold", "0.75", "--repo", tempDir },
                gitClient: fakeGit);

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            string hookPath = Path.Combine(tempDir, ".git", "hooks", "pre-commit");
            string hookContent = await File.ReadAllTextAsync(hookPath);
            Assert.Contains("--warn-threshold 0.75", hookContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ImpactCli_InstallHook_HookScript_IsValidShellSyntax()
    {
        if (OperatingSystem.IsWindows()) return;

        string tempDir = Path.Combine(Path.GetTempPath(), "gitic_hook_syntax_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, ".git"));

        try
        {
            var fakeGit = new FakeGitClientForImpact { RepoRoot = tempDir };
            var reporter = new MockConsoleReporter();

            var command = new ImpactCommand(
                new[] { "--install-hook", "--repo", tempDir },
                gitClient: fakeGit);

            await command.ExecuteAsync(reporter);

            string hookPath = Path.Combine(tempDir, ".git", "hooks", "pre-commit");

            // Execute `sh -n <hookPath>` to check for shell script syntax validity
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = "sh",
                ArgumentList = { "-n", hookPath },
                RedirectStandardError = true,
                UseShellExecute = false
            };
            proc.Start();
            await proc.WaitForExitAsync();

            Assert.Equal(0, proc.ExitCode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ImpactCli_InstallHook_HandlesGitWorktreeFileReference()
    {
        string worktreeDir = Path.Combine(Path.GetTempPath(), "gitic_worktree_" + Guid.NewGuid().ToString("N"));
        string actualGitDir = Path.Combine(Path.GetTempPath(), "gitic_actual_git_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(worktreeDir);
        Directory.CreateDirectory(actualGitDir);

        try
        {
            // .git in worktree is a file with gitdir: <actualGitDir>
            string gitFile = Path.Combine(worktreeDir, ".git");
            await File.WriteAllTextAsync(gitFile, $"gitdir: {actualGitDir}\n");

            var fakeGit = new FakeGitClientForImpact { RepoRoot = worktreeDir };
            var reporter = new MockConsoleReporter();

            var command = new ImpactCommand(
                new[] { "--install-hook", "--repo", worktreeDir },
                gitClient: fakeGit);

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            string hookPath = Path.Combine(actualGitDir, "hooks", "pre-commit");
            Assert.True(File.Exists(hookPath), "Hook should be written to the actual gitdir referenced by .git file.");
        }
        finally
        {
            if (Directory.Exists(worktreeDir)) Directory.Delete(worktreeDir, true);
            if (Directory.Exists(actualGitDir)) Directory.Delete(actualGitDir, true);
        }
    }
}
