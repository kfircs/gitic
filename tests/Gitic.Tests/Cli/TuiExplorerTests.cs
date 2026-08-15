using Kfc.Cli.Tui;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Gitic.Tests;

public class TuiExplorerTests
{
    [Fact]
    public void BuildTree_CorrectlyExcludesNonCodeFiles()
    {
        // Arrange
        var files = new List<FileMetric>
        {
            new() { Path = "src/Core/Analyzer.cs", Lines = 100, Width = 80 },
            new() { Path = "README.md", Lines = 50, Width = 60 },
            new() { Path = "src/Gitic.csproj", Lines = 40, Width = 50 },
            new() { Path = "src/Core/logo.png", Lines = 0, Width = 0 },
            new() { Path = "package-lock.json", Lines = 1000, Width = 120 }
        };

        // Act
        var root = TuiNode.BuildTree(files);

        // Assert
        // The tree should only include "src/Core/Analyzer.cs"
        Assert.NotNull(root);
        Assert.True(root.IsDirectory);
        Assert.Equal(1, root.FileCount);
        Assert.Equal(100, root.TotalLines);

        // Assert that the child is "src" -> "Core" -> "Analyzer.cs"
        Assert.Single(root.Children);
        var srcNode = root.Children[0];
        Assert.Equal("src", srcNode.Name);
        Assert.True(srcNode.IsDirectory);

        Assert.Single(srcNode.Children);
        var coreNode = srcNode.Children[0];
        Assert.Equal("Core", coreNode.Name);
        Assert.True(coreNode.IsDirectory);

        Assert.Single(coreNode.Children);
        var fileNode = coreNode.Children[0];
        Assert.Equal("Analyzer.cs", fileNode.Name);
        Assert.False(fileNode.IsDirectory);
        Assert.Equal(100, fileNode.TotalLines);
        Assert.Equal(80, fileNode.MaxWidth);
    }

    [Fact]
    public void BuildTree_AggregatesLinesAndWidthsBottomUp()
    {
        // Arrange
        var files = new List<FileMetric>
        {
            new() { Path = "src/Core/Analyzer.cs", Lines = 200, Width = 100, Touches = 10, Churn = 50, HeatScore = 5.0, AttentionScore = 12.0 },
            new() { Path = "src/Core/Types.cs", Lines = 50, Width = 40, Touches = 5, Churn = 20, HeatScore = 2.0, AttentionScore = 8.0 },
            new() { Path = "src/Cli/Cli.cs", Lines = 150, Width = 120, Touches = 8, Churn = 40, HeatScore = 4.0, AttentionScore = 15.0 }
        };

        // Act
        var root = TuiNode.BuildTree(files);

        // Assert
        Assert.NotNull(root);
        Assert.Equal(3, root.FileCount);
        Assert.Equal(400, root.TotalLines);
        Assert.Equal(50, root.MinLines);
        Assert.Equal(200, root.MaxLines);

        Assert.Equal(40, root.MinWidth);
        Assert.Equal(120, root.MaxWidth);
        Assert.Equal(260, root.TotalWidth); // 100 + 40 + 120

        // Subfolder aggregation checks
        var src = root.Children.FirstOrDefault(c => c.Name == "src");
        Assert.NotNull(src);
        Assert.Equal(3, src.FileCount);
        Assert.Equal(400, src.TotalLines);
        Assert.Equal(50, src.MinLines);
        Assert.Equal(200, src.MaxLines);

        var core = src.Children.FirstOrDefault(c => c.Name == "Core");
        Assert.NotNull(core);
        Assert.Equal(2, core.FileCount);
        Assert.Equal(250, core.TotalLines);
        Assert.Equal(50, core.MinLines);
        Assert.Equal(200, core.MaxLines);
        Assert.Equal(40, core.MinWidth);
        Assert.Equal(100, core.MaxWidth);

        var cli = src.Children.FirstOrDefault(c => c.Name == "Cli");
        Assert.NotNull(cli);
        Assert.Equal(1, cli.FileCount);
        Assert.Equal(150, cli.TotalLines);
        Assert.Equal(150, cli.MinLines);
        Assert.Equal(150, cli.MaxLines);
        Assert.Equal(120, cli.MinWidth);
        Assert.Equal(120, cli.MaxWidth);
    }

    [Fact]
    public void TuiViewport_Calculate_AppliesSaneBounds()
    {
        // Test standard bounds
        var normal = TuiViewport.Calculate(80, 24);
        Assert.Equal(80, normal.Width);
        Assert.Equal(23, normal.Height); // Height - 1
        Assert.Equal(78, normal.SafeWidth);
        Assert.Equal(15, normal.ContentHeight); // Height - 8 = 23 - 8 = 15

        // Test minimum bounds
        var tooSmall = TuiViewport.Calculate(40, 10);
        Assert.Equal(70, tooSmall.Width); // clamped to 70
        Assert.Equal(15, tooSmall.Height); // clamped to 15
    }

    [Fact]
    public void TuiScrollManager_AdjustScroll_ClampsAndScrollsCorrectly()
    {
        // 0 items
        var (idx0, scroll0) = TuiScrollManager.AdjustScroll(5, 2, 0, 10);
        Assert.Equal(0, idx0);
        Assert.Equal(0, scroll0);

        // Within content height, no scrolling needed
        var (idx1, scroll1) = TuiScrollManager.AdjustScroll(3, 0, 20, 10);
        Assert.Equal(3, idx1);
        Assert.Equal(0, scroll1);

        // Clamping selected index to bounds
        var (idx2, scroll2) = TuiScrollManager.AdjustScroll(25, 0, 20, 10);
        Assert.Equal(19, idx2); // Clamped to itemCount - 1
        Assert.Equal(10, scroll2); // ScrollOffset adjusts to show index 19 (19 - 10 + 1 = 10)

        // Scrolling up
        var (idx3, scroll3) = TuiScrollManager.AdjustScroll(2, 5, 20, 10);
        Assert.Equal(2, idx3);
        Assert.Equal(2, scroll3); // ScrollOffset goes to selected index when scrolling up
    }
}
