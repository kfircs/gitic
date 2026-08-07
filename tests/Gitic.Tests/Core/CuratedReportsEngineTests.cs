using System;
using System.Collections.Generic;
using Xunit;

namespace Gitic.Tests;

public class CuratedReportsEngineTests
{
    [Fact]
    public void Calculate_WithVaryingSpan_SetsAdaptiveCodeRotThreshold()
    {
        // Arrange
        var engine = new CuratedReportsEngine();
        
        // Scenario 1: Commit span < 90 days (under 3 months) -> threshold should be 14 days
        var now = DateTimeOffset.UtcNow;
        var commitsShort = new List<GitCommitRecord>
        {
            new() { Timestamp = now.AddDays(-10).ToUnixTimeMilliseconds(), Message = "feat: some feature" },
            new() { Timestamp = now.ToUnixTimeMilliseconds(), Message = "fix: some fix" }
        };
        var files = new List<FileMetric>
        {
            new() { Path = "src/File1.cs", Lines = 10, LastTouched = now.AddDays(-5).ToString("yyyy-MM-dd HH:mm:ss") }
        };

        // Act
        var reportsShort = engine.Calculate(commitsShort, files, null);

        // Assert
        Assert.Equal(14, reportsShort.CodeRot.ThresholdDays);

        // Scenario 2: Commit span 90 to 365 days (under 1 year) -> threshold should be 90 days
        var commitsMed = new List<GitCommitRecord>
        {
            new() { Timestamp = now.AddDays(-120).ToUnixTimeMilliseconds(), Message = "feat: older commit" },
            new() { Timestamp = now.ToUnixTimeMilliseconds(), Message = "fix: newer commit" }
        };

        // Act
        var reportsMed = engine.Calculate(commitsMed, files, null);

        // Assert
        Assert.Equal(90, reportsMed.CodeRot.ThresholdDays);

        // Scenario 3: Commit span >= 365 days -> threshold should be 365 days
        var commitsLong = new List<GitCommitRecord>
        {
            new() { Timestamp = now.AddDays(-400).ToUnixTimeMilliseconds(), Message = "feat: very old commit" },
            new() { Timestamp = now.ToUnixTimeMilliseconds(), Message = "fix: newer commit" }
        };

        // Act
        var reportsLong = engine.Calculate(commitsLong, files, null);

        // Assert
        Assert.Equal(365, reportsLong.CodeRot.ThresholdDays);
    }

    [Fact]
    public void Calculate_PopulatesPathScopedWorkClassificationForFiles()
    {
        // Arrange
        var engine = new CuratedReportsEngine();
        var now = DateTimeOffset.UtcNow;

        var commits = new List<GitCommitRecord>
        {
            new()
            {
                Timestamp = now.ToUnixTimeMilliseconds(),
                Message = "feat: add logging",
                Files = new List<GitFileChange>
                {
                    new() { Path = "src/Logger.cs" }
                }
            },
            new()
            {
                Timestamp = now.ToUnixTimeMilliseconds(),
                Message = "fix: null reference error in parser",
                Files = new List<GitFileChange>
                {
                    new() { Path = "src/Parser.cs" }
                }
            },
            new()
            {
                Timestamp = now.ToUnixTimeMilliseconds(),
                Message = "refactor: simplify parser loop",
                Files = new List<GitFileChange>
                {
                    new() { Path = "src/Parser.cs" }
                }
            }
        };

        var files = new List<FileMetric>
        {
            new() { Path = "src/Logger.cs" },
            new() { Path = "src/Parser.cs" }
        };

        // Act
        var reports = engine.Calculate(commits, files, null);

        // Assert
        var loggerMetric = files.Find(f => f.Path == "src/Logger.cs");
        var parserMetric = files.Find(f => f.Path == "src/Parser.cs");

        Assert.NotNull(loggerMetric);
        Assert.NotNull(parserMetric);

        // Logger should have 1 feature commit
        Assert.Equal(1, loggerMetric.WorkClassification.Features);
        Assert.Equal(0, loggerMetric.WorkClassification.Bugs);

        // Parser should have 1 bugfix and 1 tech debt (refactor) commit
        Assert.Equal(0, parserMetric.WorkClassification.Features);
        Assert.Equal(1, parserMetric.WorkClassification.Bugs);
        Assert.Equal(1, parserMetric.WorkClassification.TechnicalDebt);
    }
}
