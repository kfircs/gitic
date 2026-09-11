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

public class ContributorTrajectoryAggregationTests
{
    private static BaselineSnapshot CreateSnapshotWithContributors(
        DateTimeOffset timestamp,
        string tag,
        Dictionary<string, ContributorBaselineMetrics> contributors)
    {
        var snapshot = new BaselineSnapshot
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = timestamp,
            Tag = tag,
            CommitHash = "hash_" + tag,
            Summary = new BaselineSummaryMetrics { CommitCount = contributors.Values.Sum(c => c.CommitCount) },
            Contributors = contributors
        };
        return snapshot;
    }

    [Fact]
    public void TestAggregation_MultiBaseline_OrdersPointsChronologically()
    {
        var t1 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var t3 = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var snap1 = CreateSnapshotWithContributors(t1, "sprint-1", new()
        {
            ["Alice"] = new ContributorBaselineMetrics
            {
                Developer = "Alice",
                CommitCount = 20,
                ReworkRate = 0.25,
                ContextSwitchesPerWeek = 8.0,
                PrimaryAreas = new() { "Core", "Cli" }
            }
        });

        var snap2 = CreateSnapshotWithContributors(t2, "sprint-2", new()
        {
            ["Alice"] = new ContributorBaselineMetrics
            {
                Developer = "Alice",
                CommitCount = 25,
                ReworkRate = 0.20,
                ContextSwitchesPerWeek = 6.0,
                PrimaryAreas = new() { "Core" }
            }
        });

        var snap3 = CreateSnapshotWithContributors(t3, "sprint-3", new()
        {
            ["Alice"] = new ContributorBaselineMetrics
            {
                Developer = "Alice",
                CommitCount = 30,
                ReworkRate = 0.15,
                ContextSwitchesPerWeek = 4.0,
                PrimaryAreas = new() { "Core" }
            }
        });

        // Pass snapshots out of order
        var engine = new ContributorTrajectoryEngine();
        var profiles = engine.AnalyzeTrajectories(new[] { snap3, snap1, snap2 });

        Assert.Single(profiles);
        var alice = profiles[0];
        Assert.Equal("Alice", alice.Developer);
        Assert.Equal(3, alice.Timeline.Count);

        // Verify chronological order
        Assert.Equal(t1, alice.Timeline[0].SnapshotDate);
        Assert.Equal(20, alice.Timeline[0].CommitCount);
        Assert.Equal(0.25, alice.Timeline[0].ReworkRate);
        Assert.Equal(8.0, alice.Timeline[0].ContextSwitchesPerWeek);
        Assert.Equal(new[] { "Core", "Cli" }, alice.Timeline[0].PrimaryAreas);
        Assert.Equal("sprint-1", alice.Timeline[0].Tag);

        Assert.Equal(t2, alice.Timeline[1].SnapshotDate);
        Assert.Equal(25, alice.Timeline[1].CommitCount);
        Assert.Equal(0.20, alice.Timeline[1].ReworkRate);

        Assert.Equal(t3, alice.Timeline[2].SnapshotDate);
        Assert.Equal(30, alice.Timeline[2].CommitCount);
        Assert.Equal(0.15, alice.Timeline[2].ReworkRate);
    }

    [Fact]
    public void TestAggregation_MultipleContributors_AggregatesSeparately()
    {
        var t1 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        var snap1 = CreateSnapshotWithContributors(t1, "sprint-1", new()
        {
            ["Alice"] = new ContributorBaselineMetrics
            {
                Developer = "Alice",
                CommitCount = 15,
                ReworkRate = 0.10,
                PrimaryAreas = new() { "Core" }
            },
            ["Bob"] = new ContributorBaselineMetrics
            {
                Developer = "Bob",
                CommitCount = 30,
                ReworkRate = 0.30,
                PrimaryAreas = new() { "Cli" }
            }
        });

        var snap2 = CreateSnapshotWithContributors(t2, "sprint-2", new()
        {
            ["Alice"] = new ContributorBaselineMetrics
            {
                Developer = "Alice",
                CommitCount = 20,
                ReworkRate = 0.08,
                PrimaryAreas = new() { "Core" }
            },
            ["Bob"] = new ContributorBaselineMetrics
            {
                Developer = "Bob",
                CommitCount = 28,
                ReworkRate = 0.22,
                PrimaryAreas = new() { "Cli", "Git" }
            }
        });

        var engine = new ContributorTrajectoryEngine();
        var profiles = engine.AnalyzeTrajectories(new[] { snap1, snap2 });

        Assert.Equal(2, profiles.Count);

        var alice = profiles.FirstOrDefault(p => p.Developer == "Alice");
        Assert.NotNull(alice);
        Assert.Equal(2, alice.Timeline.Count);
        Assert.Equal(15, alice.Timeline[0].CommitCount);
        Assert.Equal(20, alice.Timeline[1].CommitCount);

        var bob = profiles.FirstOrDefault(p => p.Developer == "Bob");
        Assert.NotNull(bob);
        Assert.Equal(2, bob.Timeline.Count);
        Assert.Equal(30, bob.Timeline[0].CommitCount);
        Assert.Equal(28, bob.Timeline[1].CommitCount);
    }

    [Fact]
    public void TestAggregation_LateJoiningContributor_TimelineStartsAtFirstAppearance()
    {
        var t1 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var t3 = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var snap1 = CreateSnapshotWithContributors(t1, "sprint-1", new()
        {
            ["Alice"] = new ContributorBaselineMetrics { Developer = "Alice", CommitCount = 10, PrimaryAreas = new() { "Core" } }
        });

        // Carol joins in sprint-2
        var snap2 = CreateSnapshotWithContributors(t2, "sprint-2", new()
        {
            ["Alice"] = new ContributorBaselineMetrics { Developer = "Alice", CommitCount = 12, PrimaryAreas = new() { "Core" } },
            ["Carol"] = new ContributorBaselineMetrics { Developer = "Carol", CommitCount = 8, PrimaryAreas = new() { "Frontend" } }
        });

        var snap3 = CreateSnapshotWithContributors(t3, "sprint-3", new()
        {
            ["Alice"] = new ContributorBaselineMetrics { Developer = "Alice", CommitCount = 14, PrimaryAreas = new() { "Core" } },
            ["Carol"] = new ContributorBaselineMetrics { Developer = "Carol", CommitCount = 15, PrimaryAreas = new() { "Frontend" } }
        });

        var engine = new ContributorTrajectoryEngine();
        var profiles = engine.AnalyzeTrajectories(new[] { snap1, snap2, snap3 });

        var carol = profiles.FirstOrDefault(p => p.Developer == "Carol");
        Assert.NotNull(carol);
        Assert.Equal(2, carol.Timeline.Count);
        Assert.Equal(t2, carol.Timeline[0].SnapshotDate);
        Assert.Equal(8, carol.Timeline[0].CommitCount);
        Assert.Equal(t3, carol.Timeline[1].SnapshotDate);
        Assert.Equal(15, carol.Timeline[1].CommitCount);
    }

    [Fact]
    public void TestAggregation_EmptyOrNullBaselines_ReturnsEmptyList()
    {
        var engine = new ContributorTrajectoryEngine();

        var nullResult = engine.AnalyzeTrajectories(null!);
        Assert.NotNull(nullResult);
        Assert.Empty(nullResult);

        var emptyResult = engine.AnalyzeTrajectories(new List<BaselineSnapshot>());
        Assert.NotNull(emptyResult);
        Assert.Empty(emptyResult);
    }

    [Fact]
    public void TestAggregation_BaselineEngineIntegration_PopulatesFromAnalysisResult()
    {
        var analysis = new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                RepoRoot = "/repo",
                CommitCount = 10,
                GeneratedAt = "2026-04-01T12:00:00Z"
            },
            Contributors = new List<ContributorMetric>
            {
                new()
                {
                    Name = "Alice",
                    TotalActivity = 10,
                    Areas = new List<ContributorAreaMetric>
                    {
                        new() { Area = "Core", Activity = 8, ActivityShare = 0.8 },
                        new() { Area = "Cli", Activity = 2, ActivityShare = 0.2 }
                    }
                }
            },
            CognitiveProfiles = new List<ContributorCognitiveProfile>
            {
                new()
                {
                    Developer = "Alice",
                    EstimatedSwitchesPerWeek = 3.5,
                    TotalCommits = 10,
                    PrimaryArea = "Core"
                }
            },
            Files = new List<FileMetric>
            {
                new()
                {
                    Path = "src/Core/A.cs",
                    Area = "Core",
                    Touches = 8,
                    ReworkRate = 0.12,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", Activity = 8 }
                    }
                }
            }
        };

        var baselineEngine = new BaselineEngine();
        var snapshot = baselineEngine.CreateSnapshot(analysis, tag: "sprint-5");

        Assert.NotNull(snapshot.Contributors);
        Assert.True(snapshot.Contributors.ContainsKey("Alice"));
        var aliceMetric = snapshot.Contributors["Alice"];
        Assert.Equal(10, aliceMetric.CommitCount);
        Assert.Equal(3.5, aliceMetric.ContextSwitchesPerWeek);
        Assert.Contains("Core", aliceMetric.PrimaryAreas);

        var trajectoryEngine = new ContributorTrajectoryEngine();
        var profiles = trajectoryEngine.AnalyzeTrajectories(new[] { snapshot });

        Assert.Single(profiles);
        Assert.Equal("Alice", profiles[0].Developer);
        Assert.Single(profiles[0].Timeline);
        Assert.Equal(10, profiles[0].Timeline[0].CommitCount);
        Assert.Equal(3.5, profiles[0].Timeline[0].ContextSwitchesPerWeek);
    }

    [Fact]
    public void TestAggregation_EmptyContributorsDictionary_ReturnsEmptyList()
    {
        var snap = new BaselineSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Areas = new()
            {
                ["Core"] = new AreaBaselineMetrics
                {
                    Area = "Core",
                    ReworkRate = 0.18
                }
            }
        };

        var engine = new ContributorTrajectoryEngine();
        var profiles = engine.AnalyzeTrajectories(new[] { snap });
        Assert.Empty(profiles);
    }
}

public class ContributorTrajectoryDriftDetectionTests
{
    private static ContributorTrajectoryPoint Point(
        int year, int month, int day,
        int commits, double rework, double switches,
        params string[] areas)
    {
        return new ContributorTrajectoryPoint
        {
            SnapshotDate = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero),
            CommitCount = commits,
            ReworkRate = rework,
            ContextSwitchesPerWeek = switches,
            PrimaryAreas = areas.ToList()
        };
    }

    [Fact]
    public void TestDriftDetection_Deepening_ConsolidatesIntoFewerAreas()
    {
        // Alice initially works in Core and Cli, then narrows focus strictly to Core
        var timeline = new List<ContributorTrajectoryPoint>
        {
            Point(2026, 1, 1, 30, 0.25, 12.0, "Core", "Cli"),
            Point(2026, 2, 1, 28, 0.20, 8.0, "Core"),
            Point(2026, 3, 1, 25, 0.15, 4.0, "Core")
        };

        var category = ContributorTrajectoryEngine.DetermineDriftCategory(timeline);
        Assert.Equal(SpecializationDriftCategory.Deepening, category);

        var profile = new ContributorTrajectoryProfile
        {
            Developer = "Alice",
            DriftCategory = category,
            Timeline = timeline
        };
        profile.TrajectoryNarrative = ContributorTrajectoryEngine.GenerateNarrative(profile);

        Assert.Contains("deepening specialization into Core", profile.TrajectoryNarrative);
        Assert.Contains("Rework rate improved", profile.TrajectoryNarrative);
        Assert.Contains("Context-switching decreased", profile.TrajectoryNarrative);
    }

    [Fact]
    public void TestDriftDetection_Deepening_DecreasedContextSwitchesWithStableCommits()
    {
        // Bob maintains same primary area "Core", but drops context switches from 10.0 to 2.0/wk
        var timeline = new List<ContributorTrajectoryPoint>
        {
            Point(2026, 1, 1, 25, 0.15, 10.0, "Core"),
            Point(2026, 2, 1, 24, 0.14, 2.0, "Core")
        };

        var category = ContributorTrajectoryEngine.DetermineDriftCategory(timeline);
        Assert.Equal(SpecializationDriftCategory.Deepening, category);
    }

    [Fact]
    public void TestDriftDetection_Expanding_AddsNewSubsystems()
    {
        // Carol initially specializes in Core, then expands into Git and Cli
        var timeline = new List<ContributorTrajectoryPoint>
        {
            Point(2026, 1, 1, 20, 0.10, 3.0, "Core"),
            Point(2026, 2, 1, 26, 0.12, 7.0, "Core", "Git"),
            Point(2026, 3, 1, 32, 0.14, 9.5, "Core", "Git", "Cli")
        };

        var category = ContributorTrajectoryEngine.DetermineDriftCategory(timeline);
        Assert.Equal(SpecializationDriftCategory.Expanding, category);

        var profile = new ContributorTrajectoryProfile
        {
            Developer = "Carol",
            DriftCategory = category,
            Timeline = timeline
        };
        profile.TrajectoryNarrative = ContributorTrajectoryEngine.GenerateNarrative(profile);

        Assert.Contains("expanding domain scope into new areas", profile.TrajectoryNarrative);
        Assert.Contains("Git", profile.TrajectoryNarrative);
    }

    [Fact]
    public void TestDriftDetection_Withdrawing_SignificantCommitDrop()
    {
        // Dave's commits drop by more than 60%
        var timeline = new List<ContributorTrajectoryPoint>
        {
            Point(2026, 1, 1, 40, 0.15, 8.0, "Core"),
            Point(2026, 2, 1, 20, 0.12, 4.0, "Core"),
            Point(2026, 3, 1, 5, 0.05, 1.0, "Core")
        };

        var category = ContributorTrajectoryEngine.DetermineDriftCategory(timeline);
        Assert.Equal(SpecializationDriftCategory.Withdrawing, category);

        var profile = new ContributorTrajectoryProfile
        {
            Developer = "Dave",
            DriftCategory = category,
            Timeline = timeline
        };
        profile.TrajectoryNarrative = ContributorTrajectoryEngine.GenerateNarrative(profile);

        Assert.Contains("withdrawing activity", profile.TrajectoryNarrative);
        Assert.Contains("40 to 5", profile.TrajectoryNarrative);
    }

    [Fact]
    public void TestDriftDetection_Withdrawing_ZeroCommitsOrEmptiedAreas()
    {
        var timelineZero = new List<ContributorTrajectoryPoint>
        {
            Point(2026, 1, 1, 30, 0.20, 6.0, "Core"),
            Point(2026, 2, 1, 0, 0.0, 0.0)
        };

        var catZero = ContributorTrajectoryEngine.DetermineDriftCategory(timelineZero);
        Assert.Equal(SpecializationDriftCategory.Withdrawing, catZero);

        var timelineEmptyAreas = new List<ContributorTrajectoryPoint>
        {
            Point(2026, 1, 1, 20, 0.10, 3.0, "Frontend"),
            Point(2026, 2, 1, 3, 0.05, 0.5)
        };

        var catEmpty = ContributorTrajectoryEngine.DetermineDriftCategory(timelineEmptyAreas);
        Assert.Equal(SpecializationDriftCategory.Withdrawing, catEmpty);
    }

    [Fact]
    public void TestDriftDetection_Stable_SteadyMetricsAndFocus()
    {
        var timeline = new List<ContributorTrajectoryPoint>
        {
            Point(2026, 1, 1, 20, 0.12, 4.0, "Core"),
            Point(2026, 2, 1, 22, 0.13, 4.2, "Core"),
            Point(2026, 3, 1, 21, 0.11, 3.9, "Core")
        };

        var category = ContributorTrajectoryEngine.DetermineDriftCategory(timeline);
        Assert.Equal(SpecializationDriftCategory.Stable, category);

        var profile = new ContributorTrajectoryProfile
        {
            Developer = "Eve",
            DriftCategory = category,
            Timeline = timeline
        };
        profile.TrajectoryNarrative = ContributorTrajectoryEngine.GenerateNarrative(profile);

        Assert.Contains("maintains a stable focus", profile.TrajectoryNarrative);
    }

    [Fact]
    public void TestDriftDetection_SinglePoint_DefaultsToStable()
    {
        var timeline = new List<ContributorTrajectoryPoint>
        {
            Point(2026, 1, 1, 20, 0.12, 4.0, "Core")
        };

        var category = ContributorTrajectoryEngine.DetermineDriftCategory(timeline);
        Assert.Equal(SpecializationDriftCategory.Stable, category);

        var profile = new ContributorTrajectoryProfile
        {
            Developer = "Frank",
            DriftCategory = category,
            Timeline = timeline
        };
        string narrative = ContributorTrajectoryEngine.GenerateNarrative(profile);
        Assert.Contains("single baseline", narrative);
    }
}

public class ContributorTrajectoryCliExecutionTests
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

    private static (string storageDir, BaselineEngine engine) SetupTestBaselines()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "gitic_traj_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var engine = new BaselineEngine();

        var snap1 = new BaselineSnapshot
        {
            Timestamp = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
            Tag = "sprint-10",
            Summary = new BaselineSummaryMetrics { CommitCount = 50 },
            Contributors = new()
            {
                ["Alice"] = new ContributorBaselineMetrics
                {
                    Developer = "Alice",
                    CommitCount = 30,
                    ReworkRate = 0.24,
                    ContextSwitchesPerWeek = 14.2,
                    PrimaryAreas = new() { "Core", "Cli" }
                },
                ["Bob"] = new ContributorBaselineMetrics
                {
                    Developer = "Bob",
                    CommitCount = 20,
                    ReworkRate = 0.10,
                    ContextSwitchesPerWeek = 3.0,
                    PrimaryAreas = new() { "Core" }
                }
            }
        };

        var snap2 = new BaselineSnapshot
        {
            Timestamp = new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero),
            Tag = "sprint-11",
            Summary = new BaselineSummaryMetrics { CommitCount = 60 },
            Contributors = new()
            {
                ["Alice"] = new ContributorBaselineMetrics
                {
                    Developer = "Alice",
                    CommitCount = 22,
                    ReworkRate = 0.16,
                    ContextSwitchesPerWeek = 8.1,
                    PrimaryAreas = new() { "Core" }
                },
                ["Bob"] = new ContributorBaselineMetrics
                {
                    Developer = "Bob",
                    CommitCount = 25,
                    ReworkRate = 0.12,
                    ContextSwitchesPerWeek = 6.5,
                    PrimaryAreas = new() { "Core", "Git" }
                }
            }
        };

        engine.SaveBaseline(tempDir, snap1, "sprint-10");
        engine.SaveBaseline(tempDir, snap2, "sprint-11");

        return (tempDir, engine);
    }

    [Fact]
    public async Task TestCliExecution_PrintsHistoricalGrowthTimeline()
    {
        var (storageDir, engine) = SetupTestBaselines();
        try
        {
            var options = new ContributorTrajectoryCommandOptions
            {
                StorageDir = storageDir,
                Developer = "Alice"
            };

            var command = new ContributorTrajectoryCommand(options, baselineEngine: engine);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Contributor Trajectory: Alice", reporter.Output);
            Assert.Contains("Historical Growth Timeline:", reporter.Output);
            Assert.Contains("Specialization Drift: Deepening", reporter.Output);
            Assert.Contains("2026-01-15", reporter.Output);
            Assert.Contains("2026-02-15", reporter.Output);
            Assert.Contains("Evolution Summary:", reporter.Output);
            Assert.Contains("Commit Volume:", reporter.Output);
            Assert.Contains("Rework Rate:", reporter.Output);
            Assert.Contains("Context Switches:", reporter.Output);
            Assert.Contains("Trajectory Insight:", reporter.Output);
        }
        finally
        {
            if (Directory.Exists(storageDir)) Directory.Delete(storageDir, true);
        }
    }

    [Fact]
    public async Task TestCliExecution_StringArgs_ExecutesSuccessfully()
    {
        var (storageDir, engine) = SetupTestBaselines();
        try
        {
            string[] args = new[]
            {
                "trajectory",
                "--developer", "Bob",
                "--storage", storageDir
            };

            var command = new ContributorTrajectoryCommand(args, baselineEngine: engine);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Contributor Trajectory: Bob", reporter.Output);
            Assert.Contains("Historical Growth Timeline:", reporter.Output);
            Assert.Contains("Specialization Drift: Expanding", reporter.Output);
            Assert.Contains("Git", reporter.Output);
        }
        finally
        {
            if (Directory.Exists(storageDir)) Directory.Delete(storageDir, true);
        }
    }

    [Fact]
    public async Task TestCliExecution_JsonFormat_OutputsValidJson()
    {
        var (storageDir, engine) = SetupTestBaselines();
        try
        {
            var options = new ContributorTrajectoryCommandOptions
            {
                StorageDir = storageDir,
                Json = true
            };

            var command = new ContributorTrajectoryCommand(options, baselineEngine: engine);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            using var doc = JsonDocument.Parse(reporter.Output);
            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.Equal(2, doc.RootElement.GetArrayLength());

            var firstProfile = doc.RootElement[0];
            Assert.True(firstProfile.TryGetProperty("developer", out _));
            Assert.True(firstProfile.TryGetProperty("drift_category", out _));
            Assert.True(firstProfile.TryGetProperty("timeline", out var timeline));
            Assert.Equal(2, timeline.GetArrayLength());
        }
        finally
        {
            if (Directory.Exists(storageDir)) Directory.Delete(storageDir, true);
        }
    }

    [Fact]
    public async Task TestCliExecution_NoBaselines_ReturnsExitCode1()
    {
        string emptyDir = Path.Combine(Path.GetTempPath(), "gitic_empty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDir);
        try
        {
            var options = new ContributorTrajectoryCommandOptions
            {
                StorageDir = emptyDir
            };

            var command = new ContributorTrajectoryCommand(options);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("No baseline snapshots found", reporter.Error);
        }
        finally
        {
            if (Directory.Exists(emptyDir)) Directory.Delete(emptyDir, true);
        }
    }

    [Fact]
    public async Task TestCliExecution_UnknownDeveloper_ReturnsExitCode1()
    {
        var (storageDir, engine) = SetupTestBaselines();
        try
        {
            var options = new ContributorTrajectoryCommandOptions
            {
                StorageDir = storageDir,
                Developer = "UnknownNonExistentDev"
            };

            var command = new ContributorTrajectoryCommand(options, baselineEngine: engine);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("No trajectory profiles found for contributor", reporter.Error);
        }
        finally
        {
            if (Directory.Exists(storageDir)) Directory.Delete(storageDir, true);
        }
    }

    [Fact]
    public void TestFormatSummaryTable_RendersExpectedColumns()
    {
        var engine = new ContributorTrajectoryEngine();
        var profiles = new List<ContributorTrajectoryProfile>
        {
            new()
            {
                Developer = "Alice",
                DriftCategory = SpecializationDriftCategory.Deepening,
                Timeline = new List<ContributorTrajectoryPoint>
                {
                    new() { SnapshotDate = DateTimeOffset.UtcNow.AddDays(-30), CommitCount = 30, ReworkRate = 0.24, ContextSwitchesPerWeek = 14.2 },
                    new() { SnapshotDate = DateTimeOffset.UtcNow, CommitCount = 22, ReworkRate = 0.16, ContextSwitchesPerWeek = 8.1 }
                }
            }
        };

        string table = engine.FormatSummaryTable(profiles);
        Assert.NotNull(table);
        Assert.Contains("Alice", table);
        Assert.Contains("Deepening", table);
        Assert.Contains("30", table);
        Assert.Contains("22", table);
        Assert.Contains("16.0%", table);
    }

    [Fact]
    public void TestCommandLineParser_ParsesTrajectoryCommandAndOptions()
    {
        var parser = new CommandLineParser(new[] { "trajectory", "--developer", "Alice", "--storage", "/data/baselines" });
        var parsed = parser.Parse();

        Assert.Equal("trajectory", parsed.Command);
        Assert.True(parsed.Trajectory);
        Assert.Equal("Alice", parsed.TrajectoryDeveloper);
        Assert.Equal("/data/baselines", parsed.BaselineStorageDir);

        var parserContributorFlag = new CommandLineParser(new[] { "contributor", "Bob", "--trajectory" });
        var parsedContributor = parserContributorFlag.Parse();

        Assert.Equal("trajectory", parsedContributor.Command);
        Assert.True(parsedContributor.Trajectory);
        Assert.Equal("Bob", parsedContributor.TrajectoryDeveloper);
    }
}
