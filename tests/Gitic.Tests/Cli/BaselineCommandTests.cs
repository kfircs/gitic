using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kfc.Cli.Core;
using Xunit;

namespace Gitic.Tests;

public class BaselineCliCommandExecutionTests
{
    private class MockConsoleReporter : IConsoleReporter
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

    private class FakeRepositoryAnalyzerForBaseline : IRepositoryAnalyzer
    {
        public Task<AnalysisResult> AnalyzeAsync(AnalyzeInput input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AnalysisResult
            {
                Analysis = new AnalysisMetadata
                {
                    RepoRoot = input.RepoRoot,
                    CommitCount = 50,
                    GeneratedAt = DateTime.UtcNow.ToString("o")
                },
                Files = new List<FileMetric>
                {
                    new() { Path = "src/File1.cs", Area = "src", Touches = 10, Churn = 500, Lines = 200 }
                },
                Areas = new List<AreaMetric>
                {
                    new() { Area = "src", FileCount = 1, Touches = 10, Churn = 500 }
                }
            });
        }
    }

    [Fact]
    public async Task TestBaselineSave_ExecutesAndExitsZero()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var parsed = new ParsedArgs
            {
                Command = "baseline",
                BaselineSubcommand = "save",
                BaselineTag = "v1.0",
                BaselineStorageDir = tempDir,
                RepoPath = tempDir
            };

            var command = new BaselineCommand(parsed, analyzer: new FakeRepositoryAnalyzerForBaseline());
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Baseline snapshot saved successfully", reporter.Output);
            Assert.Contains("v1.0", reporter.Output);
            Assert.True(Directory.Exists(tempDir));
            Assert.Single(Directory.GetFiles(tempDir, "*.json"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task TestBaselineSave_JsonFormat_OutputsJson()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var parsed = new ParsedArgs
            {
                Command = "baseline",
                BaselineSubcommand = "save",
                BaselineTag = "json-tag",
                BaselineStorageDir = tempDir,
                RepoPath = tempDir,
                Settings = new AnalysisSettings { Json = true, Format = "json" }
            };

            var command = new BaselineCommand(parsed, analyzer: new FakeRepositoryAnalyzerForBaseline());
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            using var doc = JsonDocument.Parse(reporter.Output);
            Assert.Equal("saved", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("json-tag", doc.RootElement.GetProperty("snapshot").GetProperty("tag").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task TestBaselineDiff_WithFromAndTo_ExecutesAndExitsZero()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var engine = new BaselineEngine();
            string pathA = Path.Combine(tempDir, "baseA.json");
            string pathB = Path.Combine(tempDir, "baseB.json");

            engine.SaveBaseline(pathA, new BaselineSnapshot
            {
                Id = "snapA",
                Tag = "v1.0",
                Summary = new BaselineSummaryMetrics { CommitCount = 10, TotalChurn = 500 }
            });

            engine.SaveBaseline(pathB, new BaselineSnapshot
            {
                Id = "snapB",
                Tag = "v2.0",
                Summary = new BaselineSummaryMetrics { CommitCount = 20, TotalChurn = 1200 }
            });

            var parsed = new ParsedArgs
            {
                Command = "baseline",
                BaselineSubcommand = "diff",
                BaselineFrom = pathA,
                BaselineTo = pathB
            };

            var command = new BaselineCommand(parsed, baselineEngine: engine);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Baseline Comparison", reporter.Output);
            Assert.Contains("commit_count", reporter.Output);
            Assert.Contains("total_churn", reporter.Output);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task TestBaselineDiff_JsonFormat_OutputsJson()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var engine = new BaselineEngine();
            string pathA = Path.Combine(tempDir, "baseA.json");
            string pathB = Path.Combine(tempDir, "baseB.json");

            engine.SaveBaseline(pathA, new BaselineSnapshot { Id = "idA", Tag = "tagA" });
            engine.SaveBaseline(pathB, new BaselineSnapshot { Id = "idB", Tag = "tagB" });

            var parsed = new ParsedArgs
            {
                Command = "baseline",
                BaselineSubcommand = "diff",
                BaselineFrom = pathA,
                BaselineTo = pathB,
                Settings = new AnalysisSettings { Json = true, Format = "json" }
            };

            var command = new BaselineCommand(parsed, baselineEngine: engine);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            using var doc = JsonDocument.Parse(reporter.Output);
            Assert.Equal("idA", doc.RootElement.GetProperty("from_baseline_id").GetString());
            Assert.Equal("idB", doc.RootElement.GetProperty("to_baseline_id").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task TestBaselineDiff_MissingBaselines_ReturnsExitCode1()
    {
        string emptyTempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyTempDir);
        try
        {
            var parsed = new ParsedArgs
            {
                Command = "baseline",
                BaselineSubcommand = "diff",
                BaselineStorageDir = emptyTempDir,
                RepoPath = emptyTempDir
            };

            var command = new BaselineCommand(parsed);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("requires two snapshots", reporter.Error);
        }
        finally
        {
            if (Directory.Exists(emptyTempDir)) Directory.Delete(emptyTempDir, true);
        }
    }

    [Fact]
    public async Task TestBaselineTrend_WithMetricAndArea_ExecutesAndExitsZero()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var engine = new BaselineEngine();

            var snap1 = new BaselineSnapshot { Timestamp = DateTimeOffset.UtcNow.AddDays(-2), Tag = "v1" };
            snap1.Areas["Core"] = new AreaBaselineMetrics { Area = "Core", Churn = 100 };
            engine.SaveBaseline(tempDir, snap1);

            var snap2 = new BaselineSnapshot { Timestamp = DateTimeOffset.UtcNow, Tag = "v2" };
            snap2.Areas["Core"] = new AreaBaselineMetrics { Area = "Core", Churn = 300 };
            engine.SaveBaseline(tempDir, snap2);

            var parsed = new ParsedArgs
            {
                Command = "baseline",
                BaselineSubcommand = "trend",
                BaselineMetric = "churn",
                BaselineArea = "Core",
                BaselineStorageDir = tempDir,
                RepoPath = tempDir
            };

            var command = new BaselineCommand(parsed, baselineEngine: engine);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Historical Trend: churn", reporter.Output);
            Assert.Contains("Area: Core", reporter.Output);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task TestBaselineTrend_JsonFormat_OutputsJson()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var engine = new BaselineEngine();
            engine.SaveBaseline(tempDir, new BaselineSnapshot { Summary = new() { TotalChurn = 500 } });

            var parsed = new ParsedArgs
            {
                Command = "baseline",
                BaselineSubcommand = "trend",
                BaselineMetric = "churn",
                BaselineStorageDir = tempDir,
                RepoPath = tempDir,
                Settings = new AnalysisSettings { Json = true, Format = "json" }
            };

            var command = new BaselineCommand(parsed, baselineEngine: engine);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            using var doc = JsonDocument.Parse(reporter.Output);
            Assert.Equal("churn", doc.RootElement.GetProperty("metric_name").GetString());
            Assert.True(doc.RootElement.GetProperty("data_points").GetArrayLength() > 0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task TestBaselineTrend_NoSnapshots_ReturnsExitCode1()
    {
        string emptyTempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyTempDir);
        try
        {
            var parsed = new ParsedArgs
            {
                Command = "baseline",
                BaselineSubcommand = "trend",
                BaselineStorageDir = emptyTempDir,
                RepoPath = emptyTempDir
            };

            var command = new BaselineCommand(parsed);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("No baseline snapshots found", reporter.Error);
        }
        finally
        {
            if (Directory.Exists(emptyTempDir)) Directory.Delete(emptyTempDir, true);
        }
    }

    [Fact]
    public async Task TestBaselineCommand_MissingSubcommand_ReturnsExitCode2()
    {
        var parsed = new ParsedArgs
        {
            Command = "baseline",
            BaselineSubcommand = null
        };

        var command = new BaselineCommand(parsed);
        var reporter = new MockConsoleReporter();

        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("baseline requires a subcommand", reporter.Error);
    }

    [Fact]
    public async Task TestBaselineCommand_UnknownSubcommand_ReturnsExitCode2()
    {
        var parsed = new ParsedArgs
        {
            Command = "baseline",
            BaselineSubcommand = "invalid_action"
        };

        var command = new BaselineCommand(parsed);
        var reporter = new MockConsoleReporter();

        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown baseline subcommand: invalid_action", reporter.Error);
    }

    [Fact]
    public void TestCommandLineParser_ParsesBaselineCommandAndSubcommands()
    {
        var parserSave = new CommandLineParser(new[] { "baseline", "save", "--tag", "v1.5", "--storage", "/var/baselines" });
        var parsedSave = parserSave.Parse();
        Assert.Equal("baseline", parsedSave.Command);
        Assert.Equal("save", parsedSave.BaselineSubcommand);
        Assert.Equal("v1.5", parsedSave.BaselineTag);
        Assert.Equal("/var/baselines", parsedSave.BaselineStorageDir);

        var parserDiff = new CommandLineParser(new[] { "baseline", "diff", "--from", "b1.json", "--to", "b2.json", "--json" });
        var parsedDiff = parserDiff.Parse();
        Assert.Equal("baseline", parsedDiff.Command);
        Assert.Equal("diff", parsedDiff.BaselineSubcommand);
        Assert.Equal("b1.json", parsedDiff.BaselineFrom);
        Assert.Equal("b2.json", parsedDiff.BaselineTo);
        Assert.True(parsedDiff.Settings.Json);

        var parserTrend = new CommandLineParser(new[] { "baseline", "trend", "--metric", "attention_score", "--area", "Frontend" });
        var parsedTrend = parserTrend.Parse();
        Assert.Equal("baseline", parsedTrend.Command);
        Assert.Equal("trend", parsedTrend.BaselineSubcommand);
        Assert.Equal("attention_score", parsedTrend.BaselineMetric);
        Assert.Equal("Frontend", parsedTrend.BaselineArea);
    }

    [Fact]
    public async Task TestCli_RunCliAsync_WithBaselineCommand()
    {
        var reporter = new MockConsoleReporter();
        var result = await Cli.RunCliAsync(new[] { "baseline" }, reporter);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("baseline requires a subcommand", reporter.Error);
    }
}
