using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kfc.Cli.Core;
using Xunit;

namespace Gitic.Tests.Reporting;

public class MockConsoleReporter : IConsoleReporter
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

public class SprintCardFormattingTests
{
    private static (BaselineSnapshot Previous, BaselineSnapshot Current) CreateSampleSnapshots()
    {
        var prev = new BaselineSnapshot
        {
            Id = "snap-prev",
            Tag = "sprint-41",
            Timestamp = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero),
            Summary = new BaselineSummaryMetrics
            {
                AverageLeadTimeHours = 18.0,
                ZombieFiles = 34,
                Features = 5,
                Bugs = 2
            },
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["dx_index"] = 67.0,
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
                    ReworkRate = 0.22,
                    Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["hotspot_density"] = 0.35,
                        ["rework_rate"] = 0.22
                    }
                }
            }
        };

        var curr = new BaselineSnapshot
        {
            Id = "snap-curr",
            Tag = "sprint-42",
            Timestamp = new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero),
            Summary = new BaselineSummaryMetrics
            {
                AverageLeadTimeHours = 12.0,
                ZombieFiles = 28,
                Features = 9,
                Bugs = 1
            },
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["dx_index"] = 71.0,
                ["p50_lead_time"] = 12.0,
                ["rework_rate"] = 0.18,
                ["hotspot_density"] = 0.42,
                ["zombie_files"] = 28.0
            },
            Areas = new Dictionary<string, AreaBaselineMetrics>(StringComparer.OrdinalIgnoreCase)
            {
                ["Core/"] = new AreaBaselineMetrics
                {
                    Area = "Core/",
                    ReworkRate = 0.18,
                    Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["hotspot_density"] = 0.42,
                        ["rework_rate"] = 0.18
                    }
                }
            }
        };

        return (prev, curr);
    }

    [Fact]
    public void TestRenderTerminalBox_ContainsBordersAndHeader()
    {
        var (prev, curr) = CreateSampleSnapshots();
        var renderer = new SprintCardRenderer();
        var card = renderer.RenderCard(prev, curr, "Sprint 42 Health Card");

        string box = renderer.RenderTerminalBox(card, enableAnsi: false);

        Assert.NotNull(box);
        Assert.Contains("╔", box);
        Assert.Contains("═", box);
        Assert.Contains("║", box);
        Assert.Contains("╚", box);
        Assert.Contains("╝", box);
        Assert.Contains("Sprint 42 Health Card", box);
        Assert.Contains("Period: Aug 26 – Sep 8, 2026", box);
    }

    [Fact]
    public void TestRenderTerminalBox_FormatsDeltaTableWithStatusIndicators()
    {
        var (prev, curr) = CreateSampleSnapshots();
        var renderer = new SprintCardRenderer();
        var card = renderer.RenderCard(prev, curr);

        string box = renderer.RenderTerminalBox(card, enableAnsi: false);

        // DX Index and status indicator
        Assert.Contains("DX Index: 67 → 71", box);
        Assert.Contains("[UP]", box);

        // Improvements table / list
        Assert.Contains("IMPROVEMENTS:", box);
        Assert.Contains("P50 Lead Time:  18h → 12h", box);
        Assert.Contains("(-33%)", box);

        // Regressions table / list
        Assert.Contains("REGRESSIONS:", box);
        Assert.Contains("Hotspot Density (Core/):  35.0% → 42.0%", box);
        Assert.Contains("[WARN]", box);

        // Key Actions
        Assert.Contains("KEY ACTIONS", box);
        Assert.True(card.KeyActions.Count > 0);
    }

    [Fact]
    public void TestRenderTerminalBox_WithAnsiFormatting_IncludesColorSequences()
    {
        var (prev, curr) = CreateSampleSnapshots();
        var renderer = new SprintCardRenderer();
        var card = renderer.RenderCard(prev, curr);

        string boxAnsi = renderer.RenderTerminalBox(card, enableAnsi: true);

        // Green ANSI for [UP]
        Assert.Contains("\u001b[1;32m[UP]\u001b[0m", boxAnsi);
        // Yellow ANSI for [WARN]
        Assert.Contains("\u001b[1;33m[WARN]\u001b[0m", boxAnsi);
    }

    [Fact]
    public void TestRenderMarkdown_FormatsComparativeTableWithStatusIndicators()
    {
        var (prev, curr) = CreateSampleSnapshots();
        var renderer = new SprintCardRenderer();
        var card = renderer.RenderCard(prev, curr, "Sprint 42 Health Card");

        string md = renderer.RenderMarkdown(card);

        Assert.NotNull(md);
        Assert.Contains("# 🏥 Sprint 42 Health Card", md);
        Assert.Contains("## DX Index Overview", md);
        Assert.Contains("## Comparative Metrics Delta", md);

        // Markdown table syntax
        Assert.Contains("| Status | Metric | Previous | Current | Absolute Delta | % Change |", md);
        Assert.Contains("| :---: | :--- | :--- | :--- | :--- | :--- |", md);
        Assert.Contains("| [UP] | P50 Lead Time | 18h | 12h | -6.0h | -33% |", md);
        Assert.Contains("| [WARN] | Hotspot Density (Core/) | 35.0% | 42.0% | +7.0pp | +20% |", md);

        // Key Actions
        Assert.Contains("## Key Actions", md);
        Assert.Contains("1. Pair on Core/ hotspots", md);
    }

    [Fact]
    public void TestRenderHtml_FormatsStandaloneDocumentWithBadges()
    {
        var (prev, curr) = CreateSampleSnapshots();
        var renderer = new SprintCardRenderer();
        var card = renderer.RenderCard(prev, curr, "Sprint 42 Health Card");

        string html = renderer.RenderHtml(card);

        Assert.NotNull(html);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("<html", html);
        Assert.Contains("<title>Sprint 42 Health Card</title>", html);
        Assert.Contains("<table", html);
        Assert.Contains("<th>Status</th>", html);
        Assert.Contains("<th>Metric</th>", html);
        Assert.Contains("<span class=\"badge badge-up\">[UP]</span>", html);
        Assert.Contains("<span class=\"badge badge-warn\">[WARN]</span>", html);
        Assert.Contains("🎯 Key Actions", html);
    }
}

public class SprintCardHighlightsTests
{
    [Fact]
    public void TestHighlightsTopImprovements_LeadTimeReworkAndZombieFiles()
    {
        var prev = new BaselineSnapshot
        {
            Summary = new BaselineSummaryMetrics { AverageLeadTimeHours = 24.0, ZombieFiles = 40 },
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["dx_index"] = 65.0,
                ["p50_lead_time"] = 24.0,
                ["rework_rate"] = 0.25,
                ["zombie_files"] = 40.0
            }
        };

        var curr = new BaselineSnapshot
        {
            Summary = new BaselineSummaryMetrics { AverageLeadTimeHours = 12.0, ZombieFiles = 25 },
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["dx_index"] = 75.0,
                ["p50_lead_time"] = 12.0,
                ["rework_rate"] = 0.15,
                ["zombie_files"] = 25.0
            }
        };

        var renderer = new SprintCardRenderer();
        var card = renderer.RenderCard(prev, curr);

        Assert.NotNull(card.Improvements);
        Assert.True(card.Improvements.Count >= 3);

        var leadTime = card.Improvements.FirstOrDefault(i => i.MetricName.Contains("Lead Time"));
        Assert.NotNull(leadTime);
        Assert.Equal(24.0, leadTime.FromValue);
        Assert.Equal(12.0, leadTime.ToValue);
        Assert.Equal(-12.0, leadTime.AbsoluteDelta);
        Assert.Equal(-50.0, leadTime.PercentageDelta);

        var rework = card.Improvements.FirstOrDefault(i => i.MetricName.Contains("Rework"));
        Assert.NotNull(rework);
        Assert.Equal(0.25, rework.FromValue);
        Assert.Equal(0.15, rework.ToValue);
        Assert.True(rework.AbsoluteDelta < 0);

        var zombies = card.Improvements.FirstOrDefault(i => i.MetricName.Contains("Zombie"));
        Assert.NotNull(zombies);
        Assert.Equal(40.0, zombies.FromValue);
        Assert.Equal(25.0, zombies.ToValue);
        Assert.Equal(-15.0, zombies.AbsoluteDelta);
    }

    [Fact]
    public void TestHighlightsEmergingRisks_HotspotConcentrationAndKnowledgeSilos()
    {
        var prev = new BaselineSnapshot
        {
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["dx_index"] = 72.0
            },
            Areas = new Dictionary<string, AreaBaselineMetrics>(StringComparer.OrdinalIgnoreCase)
            {
                ["Core/"] = new AreaBaselineMetrics
                {
                    Area = "Core/",
                    Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["hotspot_density"] = 0.30
                    }
                }
            },
            Files = new Dictionary<string, FileBaselineMetrics>(StringComparer.OrdinalIgnoreCase)
            {
                ["src/Git/Gitgraph.cs"] = new FileBaselineMetrics
                {
                    Path = "src/Git/Gitgraph.cs",
                    AttentionScore = 30.0
                }
            }
        };

        var curr = new BaselineSnapshot
        {
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["dx_index"] = 68.0
            },
            Areas = new Dictionary<string, AreaBaselineMetrics>(StringComparer.OrdinalIgnoreCase)
            {
                ["Core/"] = new AreaBaselineMetrics
                {
                    Area = "Core/",
                    Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["hotspot_density"] = 0.45
                    }
                }
            },
            Files = new Dictionary<string, FileBaselineMetrics>(StringComparer.OrdinalIgnoreCase)
            {
                ["src/Git/Gitgraph.cs"] = new FileBaselineMetrics
                {
                    Path = "src/Git/Gitgraph.cs",
                    AttentionScore = 85.0
                }
            }
        };

        var renderer = new SprintCardRenderer();
        var card = renderer.RenderCard(prev, curr);

        Assert.NotNull(card.Regressions);
        Assert.True(card.Regressions.Count >= 2);

        var hotspot = card.Regressions.FirstOrDefault(r => r.MetricName.Contains("Hotspot"));
        Assert.NotNull(hotspot);
        Assert.Equal(0.30, hotspot.FromValue, precision: 2);
        Assert.Equal(0.45, hotspot.ToValue, precision: 2);
        Assert.True(hotspot.AbsoluteDelta > 0);

        var silo = card.Regressions.FirstOrDefault(r => r.MetricName.Contains("Gitgraph.cs"));
        Assert.NotNull(silo);
        Assert.True(silo.ToValue >= 80.0);
    }

    [Fact]
    public void TestDxIndexTrajectory_CalculatesDeltaAndAssignsIndicators()
    {
        var renderer = new SprintCardRenderer();

        // 1. Positive trajectory -> [UP]
        var p1 = new BaselineSnapshot { Metrics = new() { ["dx_index"] = 67.0 } };
        var c1 = new BaselineSnapshot { Metrics = new() { ["dx_index"] = 71.0 } };
        var card1 = renderer.RenderCard(p1, c1);
        Assert.Equal(67.0, card1.PreviousDxIndex);
        Assert.Equal(71.0, card1.CurrentDxIndex);
        Assert.Equal(4.0, card1.DeltaDxIndex);
        Assert.Equal("[UP]", card1.StatusIndicator);

        // 2. Minor negative trajectory -> [WARN]
        var p2 = new BaselineSnapshot { Metrics = new() { ["dx_index"] = 72.0 } };
        var c2 = new BaselineSnapshot { Metrics = new() { ["dx_index"] = 70.0 } };
        var card2 = renderer.RenderCard(p2, c2);
        Assert.Equal(-2.0, card2.DeltaDxIndex);
        Assert.Equal("[WARN]", card2.StatusIndicator);

        // 3. Severe negative trajectory -> [CRIT]
        var p3 = new BaselineSnapshot { Metrics = new() { ["dx_index"] = 75.0 } };
        var c3 = new BaselineSnapshot { Metrics = new() { ["dx_index"] = 62.0 } };
        var card3 = renderer.RenderCard(p3, c3);
        Assert.Equal(-13.0, card3.DeltaDxIndex);
        Assert.Equal("[CRIT]", card3.StatusIndicator);
    }

    [Fact]
    public void TestPrioritizedKeyActions_GeneratedFromRegressions()
    {
        var prev = new BaselineSnapshot
        {
            Summary = new BaselineSummaryMetrics { ZombieFiles = 10 },
            Areas = new Dictionary<string, AreaBaselineMetrics>
            {
                ["Core/"] = new AreaBaselineMetrics
                {
                    Area = "Core/",
                    Metrics = new() { ["hotspot_density"] = 0.20 }
                }
            }
        };

        var curr = new BaselineSnapshot
        {
            Summary = new BaselineSummaryMetrics { ZombieFiles = 15 },
            Areas = new Dictionary<string, AreaBaselineMetrics>
            {
                ["Core/"] = new AreaBaselineMetrics
                {
                    Area = "Core/",
                    Metrics = new() { ["hotspot_density"] = 0.35 }
                }
            }
        };

        var renderer = new SprintCardRenderer();
        var card = renderer.RenderCard(prev, curr);

        Assert.NotEmpty(card.KeyActions);
        Assert.Contains(card.KeyActions, a => a.Contains("Core/") && a.Contains("hotspot", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(card.KeyActions, a => a.Contains("cleanup", StringComparison.OrdinalIgnoreCase) || a.Contains("zombie", StringComparison.OrdinalIgnoreCase));
    }
}

public class SprintCardCliExecutionTests
{
    private (string StorageDir, string PrevPath, string CurrPath) CreateTestBaselines()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "gitic_sprint_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var engine = new BaselineEngine();

        var prevSnapshot = new BaselineSnapshot
        {
            Id = "b-prev-123",
            Tag = "sprint-41",
            Timestamp = DateTimeOffset.UtcNow.AddDays(-14),
            Summary = new BaselineSummaryMetrics
            {
                AverageLeadTimeHours = 20.0,
                ZombieFiles = 30,
                Features = 4,
                Bugs = 3
            },
            Metrics = new()
            {
                ["dx_index"] = 66.0,
                ["p50_lead_time"] = 20.0,
                ["rework_rate"] = 0.20
            }
        };

        var currSnapshot = new BaselineSnapshot
        {
            Id = "b-curr-456",
            Tag = "sprint-42",
            Timestamp = DateTimeOffset.UtcNow,
            Summary = new BaselineSummaryMetrics
            {
                AverageLeadTimeHours = 14.0,
                ZombieFiles = 22,
                Features = 8,
                Bugs = 1
            },
            Metrics = new()
            {
                ["dx_index"] = 73.0,
                ["p50_lead_time"] = 14.0,
                ["rework_rate"] = 0.16
            }
        };

        string prevPath = engine.SaveBaseline(tempDir, prevSnapshot, "sprint-41");
        string currPath = engine.SaveBaseline(tempDir, currSnapshot, "sprint-42");

        return (tempDir, prevPath, currPath);
    }

    [Fact]
    public async Task TestCli_RenderStandaloneMarkdown_CreatesValidMarkdownFile()
    {
        var (storageDir, _, _) = CreateTestBaselines();
        string outputMd = Path.Combine(storageDir, "output-card.md");

        try
        {
            var options = new SprintReportCommandOptions
            {
                Baseline = "sprint-41",
                Current = "sprint-42",
                StorageDir = storageDir,
                MdPath = outputMd
            };

            var command = new SprintReportCommand(options);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(outputMd));

            string mdContent = await File.ReadAllTextAsync(outputMd);
            Assert.Contains("# 🏥 Sprint 42 Health Card", mdContent);
            Assert.Contains("## DX Index Overview", mdContent);
            Assert.Contains("| Status | Metric |", mdContent);
            Assert.Contains("[UP]", mdContent);
            Assert.Contains("Lead Time", mdContent);
        }
        finally
        {
            if (Directory.Exists(storageDir)) Directory.Delete(storageDir, true);
        }
    }

    [Fact]
    public async Task TestCli_RenderStandaloneHtml_CreatesValidHtmlFile()
    {
        var (storageDir, _, _) = CreateTestBaselines();
        string outputHtml = Path.Combine(storageDir, "output-card.html");

        try
        {
            var options = new SprintReportCommandOptions
            {
                Baseline = "sprint-41",
                Current = "sprint-42",
                StorageDir = storageDir,
                HtmlPath = outputHtml
            };

            var command = new SprintReportCommand(options);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(outputHtml));

            string htmlContent = await File.ReadAllTextAsync(outputHtml);
            Assert.Contains("<!DOCTYPE html>", htmlContent);
            Assert.Contains("<title>Sprint 42 Health Card</title>", htmlContent);
            Assert.Contains("<table", htmlContent);
            Assert.Contains("badge-up", htmlContent);
            Assert.Contains("[UP]", htmlContent);
        }
        finally
        {
            if (Directory.Exists(storageDir)) Directory.Delete(storageDir, true);
        }
    }

    [Fact]
    public async Task TestCli_RenderBothMarkdownAndHtml_CreatesBothFiles()
    {
        var (storageDir, _, _) = CreateTestBaselines();
        string outputMd = Path.Combine(storageDir, "card.md");
        string outputHtml = Path.Combine(storageDir, "card.html");

        try
        {
            var options = new SprintReportCommandOptions
            {
                Baseline = "sprint-41",
                Current = "sprint-42",
                StorageDir = storageDir,
                MdPath = outputMd,
                HtmlPath = outputHtml
            };

            var command = new SprintReportCommand(options);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(outputMd));
            Assert.True(File.Exists(outputHtml));
        }
        finally
        {
            if (Directory.Exists(storageDir)) Directory.Delete(storageDir, true);
        }
    }

    [Fact]
    public async Task TestCli_AutoDiscoversLatestBaselines_WhenOmitted()
    {
        var (storageDir, _, _) = CreateTestBaselines();
        string outputMd = Path.Combine(storageDir, "auto.md");

        try
        {
            var options = new SprintReportCommandOptions
            {
                StorageDir = storageDir,
                MdPath = outputMd
            };

            var command = new SprintReportCommand(options);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(outputMd));

            string content = await File.ReadAllTextAsync(outputMd);
            Assert.Contains("DX Index Overview", content);
        }
        finally
        {
            if (Directory.Exists(storageDir)) Directory.Delete(storageDir, true);
        }
    }

    [Fact]
    public async Task TestCli_MissingBaseline_ReturnsErrorExitCode()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "gitic_err_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = new SprintReportCommandOptions
            {
                Baseline = "non-existent-tag",
                StorageDir = tempDir
            };

            var command = new SprintReportCommand(options);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("not found", reporter.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task TestCli_ExecutionWithStringArrayArgs_Succeeds()
    {
        var (storageDir, _, _) = CreateTestBaselines();
        string outputMd = Path.Combine(storageDir, "from_args.md");

        try
        {
            string[] args = new[]
            {
                "sprint-report",
                "--baseline", "sprint-41",
                "--current", "sprint-42",
                "--storage-dir", storageDir,
                "--md", outputMd
            };

            var command = new SprintReportCommand(args);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(outputMd));
        }
        finally
        {
            if (Directory.Exists(storageDir)) Directory.Delete(storageDir, true);
        }
    }
}
