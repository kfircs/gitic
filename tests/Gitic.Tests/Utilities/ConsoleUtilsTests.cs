using System;
using Xunit;

namespace Gitic.Tests;

public class ConsoleUtilsTests
{
    [Theory]
    [InlineData(120, 120)]
    [InlineData(30, 30)]
    [InlineData(250, 250)]
    public void GetBoundedConsoleWidth_WithOverride_ShouldReturnOverrideValue(int? widthOverride, int expected)
    {
        // Arrange
        int? consoleWidthOverride = widthOverride;

        // Act
        int result = ConsoleUtils.GetBoundedConsoleWidth(consoleWidthOverride);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetBoundedConsoleWidth_WithoutOverride_ShouldReturnBoundedValue()
    {
        // Arrange
        int? consoleWidthOverride = null;

        // Act
        int width = ConsoleUtils.GetBoundedConsoleWidth(consoleWidthOverride);

        // Assert
        Assert.True(width >= 40);
        Assert.True(width <= 200);
    }

    [Fact]
    public void PadRightAnsi_WithNormalText_ShouldPadToGreaterWidth()
    {
        // Arrange
        string input = "hello";
        int width = 10;

        // Act
        string result = ConsoleUtils.PadRightAnsi(input, width);

        // Assert
        Assert.Equal("hello     ", result);
    }

    [Theory]
    [InlineData("hello", 5, "hello")]
    [InlineData("hello", 3, "hello")]
    public void PadRightAnsi_WithNormalText_ShouldBeNoOpWhenWidthIsLessThanOrEqual(string input, int width, string expected)
    {
        // Arrange
        string text = input;
        int targetWidth = width;

        // Act
        string result = ConsoleUtils.PadRightAnsi(text, targetWidth);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PadRightAnsi_WithAnsiEscapes_ShouldCalculateVisibleLengthCorrectly()
    {
        // Arrange
        string input = "\x1b[31mhello\x1b[0m";
        int width = 10;

        // Act
        string result = ConsoleUtils.PadRightAnsi(input, width);

        // Assert
        Assert.Equal("\x1b[31mhello\x1b[0m     ", result);
    }

    [Fact]
    public void PadRightAnsi_WithTextEndingInResetAndBgAnsi_ShouldStripResetAndPad()
    {
        // Arrange
        string input = "hello\x1b[0m";
        int width = 10;
        string bgAnsi = "\x1b[44m";

        // Act
        string result = ConsoleUtils.PadRightAnsi(input, width, bgAnsi);

        // Assert
        Assert.Equal("hello     \x1b[0m", result);
    }

    [Fact]
    public void PadRightAnsi_WithBgAnsiAndNoReset_ShouldPrependBgAnsiAndAppendReset()
    {
        // Arrange
        string input = "hello";
        int width = 10;
        string bgAnsi = "\x1b[44m";

        // Act
        string result = ConsoleUtils.PadRightAnsi(input, width, bgAnsi);

        // Assert
        Assert.Equal("\x1b[44mhello     \x1b[0m", result);
    }

    [Fact]
    public void PadRightAnsi_WithEmptyStringAndBgAnsi_ShouldNotThrow()
    {
        // Arrange
        string input = "";
        int width = 5;
        string bgAnsi = "\x1b[44m";

        // Act
        var exception = Record.Exception(() => ConsoleUtils.PadRightAnsi(input, width, bgAnsi));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void PadRightAnsi_WithShortStringAndBgAnsi_ShouldNotThrow()
    {
        // Arrange
        string input = "a";
        int width = 5;
        string bgAnsi = "\x1b[44m";

        // Act
        var exception = Record.Exception(() => ConsoleUtils.PadRightAnsi(input, width, bgAnsi));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void PadRightAnsi_WithResetStringAndBgAnsi_ShouldNotThrow()
    {
        // Arrange
        string input = "\x1b[0m";
        int width = 5;
        string bgAnsi = "\x1b[44m";

        // Act
        var exception = Record.Exception(() => ConsoleUtils.PadRightAnsi(input, width, bgAnsi));

        // Assert
        Assert.Null(exception);
    }
}
