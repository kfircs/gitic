using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Gitic.Tests;

public class PerspectiveRefactoringTests
{
    [Fact]
    public void CodeRotPerspective_WhenGeneratedAtIsSoonAfterLastTouched_IsNotZombie()
    {
        var fileMetric = new FileMetric
        {
            Path = "src/test.cs",
            Lines = 100,
            LastTouched = "2023-01-01T00:00:00Z"
        };
        var node = new TuiNode
        {
            Name = "test.cs",
            IsDirectory = false,
            FileMetric = fileMetric
        };
        var perspective = new CodeRotPerspective();
        var result = new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                GeneratedAt = "2023-02-01T00:00:00Z"
            },
            CuratedReports = new CuratedReports
            {
                CodeRot = new CodeRotMetric
                {
                    ThresholdDays = 365,
                    ZombieFileCount = 0,
                    ZombieLines = 0
                }
            }
        };

        var lines = perspective.GetRightSidebarLines(node, 80, result);

        var output = string.Join("\n", lines);
        Assert.Contains("Zombie Files in this folder: 0", output);
        Assert.Contains("Zombie Lines in this folder: 0", output);
    }

    [Fact]
    public void CodeRotPerspective_WhenGeneratedAtIsLongAfterLastTouched_IsZombie()
    {
        var fileMetric = new FileMetric
        {
            Path = "src/test.cs",
            Lines = 100,
            LastTouched = "2023-01-01T00:00:00Z"
        };
        var node = new TuiNode
        {
            Name = "test.cs",
            IsDirectory = false,
            FileMetric = fileMetric
        };
        var perspective = new CodeRotPerspective();
        var result = new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                GeneratedAt = "2024-02-01T00:00:00Z"
            },
            CuratedReports = new CuratedReports
            {
                CodeRot = new CodeRotMetric
                {
                    ThresholdDays = 365,
                    ZombieFileCount = 0,
                    ZombieLines = 0
                }
            }
        };

        var lines = perspective.GetRightSidebarLines(node, 80, result);

        var output = string.Join("\n", lines);
        Assert.Contains("Zombie Files in this folder: 1", output);
        Assert.Contains("Zombie Lines in this folder: 100", output);
    }

    [Fact]
    public void LinesStructurePerspective_WhenGeneratedAtIsSameDay_ShowsToday()
    {
        var fileMetric = new FileMetric
        {
            Path = "src/test.cs",
            Lines = 100,
            LastTouched = "2023-01-01T00:00:00Z"
        };
        var node = new TuiNode
        {
            Name = "test.cs",
            IsDirectory = false,
            FileMetric = fileMetric,
            TotalLines = 100
        };
        var perspective = new LinesStructurePerspective();
        var result = new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                GeneratedAt = "2023-01-01T12:00:00Z"
            }
        };

        var lines = perspective.GetRightSidebarLines(node, 80, result);

        Assert.Contains("(today)", string.Join("\n", lines));
    }

    [Fact]
    public void LinesStructurePerspective_WhenGeneratedAtIsOneDayAfter_ShowsOneDayAgo()
    {
        var fileMetric = new FileMetric
        {
            Path = "src/test.cs",
            Lines = 100,
            LastTouched = "2023-01-01T00:00:00Z"
        };
        var node = new TuiNode
        {
            Name = "test.cs",
            IsDirectory = false,
            FileMetric = fileMetric,
            TotalLines = 100
        };
        var perspective = new LinesStructurePerspective();
        var result = new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                GeneratedAt = "2023-01-02T12:00:00Z"
            }
        };

        var lines = perspective.GetRightSidebarLines(node, 80, result);

        Assert.Contains("(1 day ago)", string.Join("\n", lines));
    }

    [Fact]
    public void LinesStructurePerspective_WhenGeneratedAtIsTenDaysAfter_ShowsTenDaysAgo()
    {
        var fileMetric = new FileMetric
        {
            Path = "src/test.cs",
            Lines = 100,
            LastTouched = "2023-01-01T00:00:00Z"
        };
        var node = new TuiNode
        {
            Name = "test.cs",
            IsDirectory = false,
            FileMetric = fileMetric,
            TotalLines = 100
        };
        var perspective = new LinesStructurePerspective();
        var result = new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                GeneratedAt = "2023-01-11T12:00:00Z"
            }
        };

        var lines = perspective.GetRightSidebarLines(node, 80, result);

        Assert.Contains("(10 days ago)", string.Join("\n", lines));
    }
}
