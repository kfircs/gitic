using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Gitic.Tests;

public class PerspectiveRefactoringTests
{
    [Fact]
    public void CodeRotPerspective_UsesGeneratedAtAsReferenceDate()
    {
        // Arrange
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

        // Case A: GeneratedAt is soon after LastTouched (not a zombie)
        var resultNotZombie = new AnalysisResult
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

        // Act
        var linesNotZombie = perspective.GetRightSidebarLines(node, 80, resultNotZombie);

        // Assert
        Assert.Contains("Zombie Files in this folder: 0", string.Join("\n", linesNotZombie));
        Assert.Contains("Zombie Lines in this folder: 0", string.Join("\n", linesNotZombie));

        // Case B: GeneratedAt is long after LastTouched (is a zombie)
        var resultZombie = new AnalysisResult
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

        // Act
        var linesZombie = perspective.GetRightSidebarLines(node, 80, resultZombie);

        // Assert
        Assert.Contains("Zombie Files in this folder: 1", string.Join("\n", linesZombie));
        Assert.Contains("Zombie Lines in this folder: 100", string.Join("\n", linesZombie));
    }

    [Fact]
    public void LinesStructurePerspective_UsesGeneratedAtAsReferenceDate()
    {
        // Arrange
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

        // Case A: GeneratedAt is the same day -> "today"
        var resultSameDay = new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                GeneratedAt = "2023-01-01T12:00:00Z"
            }
        };

        // Act
        var linesSameDay = perspective.GetRightSidebarLines(node, 80, resultSameDay);

        // Assert
        Assert.Contains("(today)", string.Join("\n", linesSameDay));

        // Case B: GeneratedAt is 1 day after -> "1 day ago"
        var resultOneDay = new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                GeneratedAt = "2023-01-02T12:00:00Z"
            }
        };

        // Act
        var linesOneDay = perspective.GetRightSidebarLines(node, 80, resultOneDay);

        // Assert
        Assert.Contains("(1 day ago)", string.Join("\n", linesOneDay));

        // Case C: GeneratedAt is 10 days after -> "10 days ago"
        var resultTenDays = new AnalysisResult
        {
            Analysis = new AnalysisMetadata
            {
                GeneratedAt = "2023-01-11T12:00:00Z"
            }
        };

        // Act
        var linesTenDays = perspective.GetRightSidebarLines(node, 80, resultTenDays);

        // Assert
        Assert.Contains("(10 days ago)", string.Join("\n", linesTenDays));
    }
}
