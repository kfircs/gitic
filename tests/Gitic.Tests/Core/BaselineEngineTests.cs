using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Gitic.Tests;

public class BaselineEngineSnapshotStorageTests
{
    [Fact]
    public void TestCreateSnapshot_FromAnalysisResult_PopulatesAllMetrics()
    {
        var result = new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                RepoRoot = "/test/repo",
                CommitCount = 42,
                GeneratedAt = "2026-09-01T10:00:00Z"
            },
            Files = new List<FileMetric>
            {
                new() { Path = "src/Core.cs", Area = "Core", Touches = 10, Churn = 500, Lines = 200, HeatScore = 80.0, AttentionScore = 75.0, ReworkRate = 0.1 },
                new() { Path = "src/Cli.cs", Area = "Cli", Touches = 5, Churn = 150, Lines = 100, HeatScore = 40.0, AttentionScore = 35.0, ReworkRate = 0.05 }
            },
            Areas = new List<AreaMetric>
            {
                new() { Area = "Core", FileCount = 1, Touches = 10, Churn = 500, HeatScore = 80.0, AttentionScore = 75.0, ReworkRate = 0.1 },
                new() { Area = "Cli", FileCount = 1, Touches = 5, Churn = 150, HeatScore = 40.0, AttentionScore = 35.0, ReworkRate = 0.05 }
            },
            LeadTimes = new LeadTimesInfo
            {
                AverageLeadTimeHours = 18.5
            },
            CuratedReports = new CuratedReports
            {
                CodeRot = new CodeRotMetric { ZombieFileCount = 2, ZombieLines = 120 },
                WorkClassification = new WorkClassificationMetrics { Features = 8, Bugs = 3, TechnicalDebt = 4, Chores = 1 },
                ReviewCollaboration = new ReviewCollaborationMetric { ReviewerSilos = 1 },
                AiCodeStrain = new AiCodeStrainMetric { HighVolumeCommits = 2 }
            }
        };

        var engine = new BaselineEngine();
        var snapshot = engine.CreateSnapshot(result, tag: "v1.0", commitHash: "abc1234");

        Assert.NotNull(snapshot);
        Assert.Equal("v1.0", snapshot.Tag);
        Assert.Equal("abc1234", snapshot.CommitHash);
        Assert.Equal("/test/repo", snapshot.RepoRoot);
        Assert.Equal(42, snapshot.Summary.CommitCount);
        Assert.Equal(2, snapshot.Summary.FileCount);
        Assert.Equal(650, snapshot.Summary.TotalChurn);
        Assert.Equal(15, snapshot.Summary.TotalTouches);
        Assert.Equal(18.5, snapshot.Summary.AverageLeadTimeHours);
        Assert.Equal(2, snapshot.Summary.ZombieFiles);
        Assert.Equal(120, snapshot.Summary.ZombieLines);
        Assert.Equal(8, snapshot.Summary.Features);
        Assert.Equal(3, snapshot.Summary.Bugs);
        Assert.Equal(4, snapshot.Summary.TechnicalDebt);

        Assert.Equal(2, snapshot.Areas.Count);
        Assert.True(snapshot.Areas.ContainsKey("Core"));
        Assert.Equal(500, snapshot.Areas["Core"].Churn);
        Assert.Equal(80.0, snapshot.Areas["Core"].HeatScore);

        Assert.Equal(2, snapshot.Files.Count);
        Assert.True(snapshot.Files.ContainsKey("src/Core.cs"));
        Assert.Equal(200, snapshot.Files["src/Core.cs"].Lines);

        Assert.Equal(42, snapshot.Metrics["commits"]);
        Assert.Equal(650, snapshot.Metrics["churn"]);
        Assert.Equal(18.5, snapshot.Metrics["lead_time"]);
    }

    [Fact]
    public void TestSaveBaseline_CreatesDirectoryAndFile_AndLoadsSnapshot()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var engine = new BaselineEngine();
            var snapshot = new BaselineSnapshot
            {
                Id = "snap123",
                Tag = "release-1.0",
                CommitHash = "c0ffee",
                Summary = new BaselineSummaryMetrics
                {
                    CommitCount = 100,
                    FileCount = 25,
                    TotalChurn = 12000,
                    TotalTouches = 450,
                    AverageLeadTimeHours = 24.0
                }
            };
            snapshot.Metrics["churn"] = 12000;

            string savedPath = engine.SaveBaseline(tempDir, snapshot);

            Assert.True(File.Exists(savedPath));
            Assert.Contains(tempDir, savedPath);

            var loaded = engine.LoadBaseline(savedPath);

            Assert.NotNull(loaded);
            Assert.Equal("snap123", loaded.Id);
            Assert.Equal("release-1.0", loaded.Tag);
            Assert.Equal("c0ffee", loaded.CommitHash);
            Assert.Equal(100, loaded.Summary.CommitCount);
            Assert.Equal(25, loaded.Summary.FileCount);
            Assert.Equal(12000, loaded.Summary.TotalChurn);
            Assert.Equal(24.0, loaded.Summary.AverageLeadTimeHours);
            Assert.Equal(12000, loaded.Metrics["churn"]);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TestSaveBaseline_WithCustomTag_GeneratesTaggedFilename()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var engine = new BaselineEngine();
            var snapshot = new BaselineSnapshot
            {
                Summary = new BaselineSummaryMetrics { CommitCount = 5 }
            };

            string savedPath = engine.SaveBaseline(tempDir, snapshot, tag: "v2.0-beta");

            Assert.True(File.Exists(savedPath));
            Assert.Contains("v2.0-beta", Path.GetFileName(savedPath));

            var loaded = engine.LoadBaseline(savedPath);
            Assert.Equal("v2.0-beta", loaded.Tag);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TestSaveBaseline_DirectFilePath_WritesDirectlyToFile()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string directFilePath = Path.Combine(tempDir, "custom-baseline.json");
        try
        {
            var engine = new BaselineEngine();
            var snapshot = new BaselineSnapshot
            {
                Id = "direct123",
                Summary = new BaselineSummaryMetrics { CommitCount = 10 }
            };

            string savedPath = engine.SaveBaseline(directFilePath, snapshot);

            Assert.Equal(directFilePath, savedPath);
            Assert.True(File.Exists(directFilePath));

            var loaded = engine.LoadBaseline(directFilePath);
            Assert.Equal("direct123", loaded.Id);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TestLoadBaseline_DirectoryPath_LoadsLatestSnapshot()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var engine = new BaselineEngine();

            var snap1 = new BaselineSnapshot
            {
                Id = "snap1",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10),
                Summary = new BaselineSummaryMetrics { CommitCount = 10 }
            };
            var snap2 = new BaselineSnapshot
            {
                Id = "snap2",
                Timestamp = DateTimeOffset.UtcNow,
                Summary = new BaselineSummaryMetrics { CommitCount = 20 }
            };

            engine.SaveBaseline(tempDir, snap1);
            engine.SaveBaseline(tempDir, snap2);

            var latest = engine.LoadBaseline(tempDir);

            Assert.NotNull(latest);
            Assert.Equal("snap2", latest.Id);
            Assert.Equal(20, latest.Summary.CommitCount);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TestLoadBaseline_NonExistentFile_ThrowsFileNotFoundException()
    {
        var engine = new BaselineEngine();
        Assert.Throws<FileNotFoundException>(() => engine.LoadBaseline("/non/existent/file.json"));
    }

    [Fact]
    public void TestLoadBaseline_CorruptedJson_ThrowsException()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "{ invalid json content !!!");
            var engine = new BaselineEngine();
            Assert.ThrowsAny<Exception>(() => engine.LoadBaseline(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void TestListBaselines_OrdersByTimestampAscending()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var engine = new BaselineEngine();

            var snap1 = new BaselineSnapshot { Id = "s1", Timestamp = DateTimeOffset.UtcNow.AddHours(-2) };
            var snap2 = new BaselineSnapshot { Id = "s2", Timestamp = DateTimeOffset.UtcNow.AddHours(-1) };
            var snap3 = new BaselineSnapshot { Id = "s3", Timestamp = DateTimeOffset.UtcNow };

            // Save out of order
            engine.SaveBaseline(tempDir, snap2);
            engine.SaveBaseline(tempDir, snap1);
            engine.SaveBaseline(tempDir, snap3);

            var list = engine.ListBaselines(tempDir);

            Assert.Equal(3, list.Count);
            Assert.Equal("s1", list[0].Id);
            Assert.Equal("s2", list[1].Id);
            Assert.Equal("s3", list[2].Id);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TestListBaselines_NonExistentDirectory_ReturnsEmpty()
    {
        var engine = new BaselineEngine();
        var list = engine.ListBaselines("/non/existent/path/for/baselines");
        Assert.Empty(list);
    }
}

public class BaselineEngineDeltaCalculationTests
{
    [Fact]
    public void TestDiffBaselines_SummaryDeltas_AccurateCalculations()
    {
        var baselineA = new BaselineSnapshot
        {
            Id = "baseA",
            Tag = "v1.0",
            Timestamp = DateTimeOffset.UtcNow.AddDays(-7),
            Summary = new BaselineSummaryMetrics
            {
                CommitCount = 100,
                FileCount = 50,
                TotalChurn = 5000,
                TotalTouches = 200,
                AverageLeadTimeHours = 10.0,
                ZombieFiles = 4,
                ZombieLines = 200,
                Features = 10,
                Bugs = 6,
                TechnicalDebt = 8
            }
        };

        var baselineB = new BaselineSnapshot
        {
            Id = "baseB",
            Tag = "v2.0",
            Timestamp = DateTimeOffset.UtcNow,
            Summary = new BaselineSummaryMetrics
            {
                CommitCount = 150,
                FileCount = 55,
                TotalChurn = 7500,
                TotalTouches = 300,
                AverageLeadTimeHours = 8.0,
                ZombieFiles = 2,
                ZombieLines = 100,
                Features = 15,
                Bugs = 4,
                TechnicalDebt = 6
            }
        };

        var engine = new BaselineEngine();
        var diff = engine.DiffBaselines(baselineA, baselineB);

        Assert.NotNull(diff);
        Assert.Equal("baseA", diff.FromBaselineId);
        Assert.Equal("baseB", diff.ToBaselineId);

        // Commit Count delta: +50 (+50.0%)
        var commitDelta = diff.GetSummaryDelta("commit_count");
        Assert.NotNull(commitDelta);
        Assert.Equal(100, commitDelta.FromValue);
        Assert.Equal(150, commitDelta.ToValue);
        Assert.Equal(50, commitDelta.AbsoluteDelta);
        Assert.Equal(50.0, commitDelta.PercentageDelta, precision: 1);

        // Churn delta: +2500 (+50.0%)
        var churnDelta = diff.GetSummaryDelta("total_churn");
        Assert.NotNull(churnDelta);
        Assert.Equal(5000, churnDelta.FromValue);
        Assert.Equal(7500, churnDelta.ToValue);
        Assert.Equal(2500, churnDelta.AbsoluteDelta);
        Assert.Equal(50.0, churnDelta.PercentageDelta, precision: 1);

        // Lead time delta: -2.0 (-20.0%)
        var leadTimeDelta = diff.GetSummaryDelta("average_lead_time_hours");
        Assert.NotNull(leadTimeDelta);
        Assert.Equal(10.0, leadTimeDelta.FromValue);
        Assert.Equal(8.0, leadTimeDelta.ToValue);
        Assert.Equal(-2.0, leadTimeDelta.AbsoluteDelta);
        Assert.Equal(-20.0, leadTimeDelta.PercentageDelta, precision: 1);

        // Zombie files delta: -2 (-50.0%)
        var zombieDelta = diff.GetSummaryDelta("zombie_files");
        Assert.NotNull(zombieDelta);
        Assert.Equal(4, zombieDelta.FromValue);
        Assert.Equal(2, zombieDelta.ToValue);
        Assert.Equal(-2, zombieDelta.AbsoluteDelta);
        Assert.Equal(-50.0, zombieDelta.PercentageDelta, precision: 1);
    }

    [Fact]
    public void TestDiffBaselines_ZeroBaseline_DivisionByZeroHandledSafely()
    {
        var baselineA = new BaselineSnapshot
        {
            Summary = new BaselineSummaryMetrics { Bugs = 0, ZombieFiles = 0 }
        };
        var baselineB = new BaselineSnapshot
        {
            Summary = new BaselineSummaryMetrics { Bugs = 5, ZombieFiles = 0 }
        };

        var engine = new BaselineEngine();
        var diff = engine.DiffBaselines(baselineA, baselineB);

        var bugsDelta = diff.GetSummaryDelta("bugs");
        Assert.NotNull(bugsDelta);
        Assert.Equal(0, bugsDelta.FromValue);
        Assert.Equal(5, bugsDelta.ToValue);
        Assert.Equal(5, bugsDelta.AbsoluteDelta);
        Assert.Equal(100.0, bugsDelta.PercentageDelta);

        var zombieDelta = diff.GetSummaryDelta("zombie_files");
        Assert.NotNull(zombieDelta);
        Assert.Equal(0, zombieDelta.FromValue);
        Assert.Equal(0, zombieDelta.ToValue);
        Assert.Equal(0, zombieDelta.AbsoluteDelta);
        Assert.Equal(0.0, zombieDelta.PercentageDelta);
    }

    [Fact]
    public void TestDiffBaselines_AreaDeltas_TracksAddedRemovedAndModified()
    {
        var baselineA = new BaselineSnapshot();
        baselineA.Areas["Core"] = new AreaBaselineMetrics { Area = "Core", FileCount = 10, Churn = 1000, HeatScore = 50.0 };
        baselineA.Areas["Legacy"] = new AreaBaselineMetrics { Area = "Legacy", FileCount = 5, Churn = 500, HeatScore = 20.0 };

        var baselineB = new BaselineSnapshot();
        baselineB.Areas["Core"] = new AreaBaselineMetrics { Area = "Core", FileCount = 12, Churn = 1500, HeatScore = 60.0 };
        baselineB.Areas["Modern"] = new AreaBaselineMetrics { Area = "Modern", FileCount = 8, Churn = 800, HeatScore = 30.0 };

        var engine = new BaselineEngine();
        var diff = engine.DiffBaselines(baselineA, baselineB);

        Assert.Contains("Modern", diff.AddedAreas);
        Assert.Contains("Legacy", diff.RemovedAreas);

        var coreChurnDelta = diff.GetAreaDelta("Core", "churn");
        Assert.NotNull(coreChurnDelta);
        Assert.Equal(1000, coreChurnDelta.FromValue);
        Assert.Equal(1500, coreChurnDelta.ToValue);
        Assert.Equal(500, coreChurnDelta.AbsoluteDelta);
        Assert.Equal(50.0, coreChurnDelta.PercentageDelta, precision: 1);
    }

    [Fact]
    public void TestDiffBaselines_FileDeltas_TracksAddedRemovedAndModified()
    {
        var baselineA = new BaselineSnapshot();
        baselineA.Files["src/A.cs"] = new FileBaselineMetrics { Path = "src/A.cs", Touches = 5, Churn = 100, Lines = 50 };
        baselineA.Files["src/Old.cs"] = new FileBaselineMetrics { Path = "src/Old.cs", Touches = 2, Churn = 20, Lines = 30 };

        var baselineB = new BaselineSnapshot();
        baselineB.Files["src/A.cs"] = new FileBaselineMetrics { Path = "src/A.cs", Touches = 10, Churn = 250, Lines = 80 };
        baselineB.Files["src/New.cs"] = new FileBaselineMetrics { Path = "src/New.cs", Touches = 1, Churn = 50, Lines = 40 };

        var engine = new BaselineEngine();
        var diff = engine.DiffBaselines(baselineA, baselineB);

        Assert.Contains("src/New.cs", diff.AddedFiles);
        Assert.Contains("src/Old.cs", diff.RemovedFiles);

        var fileADelta = diff.GetFileDelta("src/A.cs", "churn");
        Assert.NotNull(fileADelta);
        Assert.Equal(100, fileADelta.FromValue);
        Assert.Equal(250, fileADelta.ToValue);
        Assert.Equal(150, fileADelta.AbsoluteDelta);
        Assert.Equal(150.0, fileADelta.PercentageDelta, precision: 1);
    }

    [Fact]
    public void TestCalculateTrend_MultipleSnapshots_AccurateTrendMetrics()
    {
        var snapshots = new List<BaselineSnapshot>
        {
            new() { Timestamp = DateTimeOffset.UtcNow.AddDays(-3), Summary = new BaselineSummaryMetrics { TotalChurn = 1000 }, Metrics = new() { ["churn"] = 1000 } },
            new() { Timestamp = DateTimeOffset.UtcNow.AddDays(-2), Summary = new BaselineSummaryMetrics { TotalChurn = 1500 }, Metrics = new() { ["churn"] = 1500 } },
            new() { Timestamp = DateTimeOffset.UtcNow.AddDays(-1), Summary = new BaselineSummaryMetrics { TotalChurn = 2000 }, Metrics = new() { ["churn"] = 2000 } },
            new() { Timestamp = DateTimeOffset.UtcNow, Summary = new BaselineSummaryMetrics { TotalChurn = 2500 }, Metrics = new() { ["churn"] = 2500 } }
        };

        var engine = new BaselineEngine();
        var trend = engine.CalculateTrend(snapshots, "churn");

        Assert.NotNull(trend);
        Assert.Equal("churn", trend.MetricName);
        Assert.Equal(4, trend.DataPoints.Count);
        Assert.Equal(1000, trend.StartValue);
        Assert.Equal(2500, trend.EndValue);
        Assert.Equal(1500, trend.AbsoluteChange);
        Assert.Equal(150.0, trend.PercentageChange, precision: 1);
        Assert.Equal(TrendDirection.Increasing, trend.Direction);
        Assert.Equal(1000, trend.MinValue);
        Assert.Equal(2500, trend.MaxValue);
        Assert.Equal(1750, trend.AverageValue);
    }

    [Fact]
    public void TestCalculateTrend_DecreasingAndStableDirections()
    {
        var engine = new BaselineEngine();

        var decreasingSnapshots = new List<BaselineSnapshot>
        {
            new() { Timestamp = DateTimeOffset.UtcNow.AddDays(-1), Summary = new BaselineSummaryMetrics { ZombieFiles = 10 }, Metrics = new() { ["zombie_files"] = 10 } },
            new() { Timestamp = DateTimeOffset.UtcNow, Summary = new BaselineSummaryMetrics { ZombieFiles = 5 }, Metrics = new() { ["zombie_files"] = 5 } }
        };

        var decTrend = engine.CalculateTrend(decreasingSnapshots, "zombie_files");
        Assert.Equal(TrendDirection.Decreasing, decTrend.Direction);
        Assert.Equal(-5, decTrend.AbsoluteChange);
        Assert.Equal(-50.0, decTrend.PercentageChange, precision: 1);

        var stableSnapshots = new List<BaselineSnapshot>
        {
            new() { Timestamp = DateTimeOffset.UtcNow.AddDays(-1), Summary = new BaselineSummaryMetrics { Features = 8 }, Metrics = new() { ["features"] = 8 } },
            new() { Timestamp = DateTimeOffset.UtcNow, Summary = new BaselineSummaryMetrics { Features = 8 }, Metrics = new() { ["features"] = 8 } }
        };

        var stableTrend = engine.CalculateTrend(stableSnapshots, "features");
        Assert.Equal(TrendDirection.Stable, stableTrend.Direction);
        Assert.Equal(0, stableTrend.AbsoluteChange);
        Assert.Equal(0.0, stableTrend.PercentageChange, precision: 1);
    }

    [Fact]
    public void TestCalculateTrend_WithAreaFilter()
    {
        var engine = new BaselineEngine();

        var snap1 = new BaselineSnapshot { Timestamp = DateTimeOffset.UtcNow.AddDays(-1) };
        snap1.Areas["src/Core"] = new AreaBaselineMetrics { Area = "src/Core", Churn = 500 };
        snap1.Areas["Frontend"] = new AreaBaselineMetrics { Area = "Frontend", Churn = 100 };

        var snap2 = new BaselineSnapshot { Timestamp = DateTimeOffset.UtcNow };
        snap2.Areas["src/Core"] = new AreaBaselineMetrics { Area = "src/Core", Churn = 1200 };
        snap2.Areas["Frontend"] = new AreaBaselineMetrics { Area = "Frontend", Churn = 100 };

        var coreTrend = engine.CalculateTrend(new[] { snap1, snap2 }, "churn", area: "src/Core");
        Assert.Equal("src/Core", coreTrend.Area);
        Assert.Equal(500, coreTrend.StartValue);
        Assert.Equal(1200, coreTrend.EndValue);
        Assert.Equal(700, coreTrend.AbsoluteChange);
        Assert.Equal(140.0, coreTrend.PercentageChange, precision: 1);
        Assert.Equal(TrendDirection.Increasing, coreTrend.Direction);

        var frontendTrend = engine.CalculateTrend(new[] { snap1, snap2 }, "churn", area: "Frontend");
        Assert.Equal(TrendDirection.Stable, frontendTrend.Direction);
        Assert.Equal(0, frontendTrend.AbsoluteChange);
    }
}
