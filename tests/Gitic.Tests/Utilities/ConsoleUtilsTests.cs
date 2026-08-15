using System;
using Xunit;

namespace Gitic.Tests;

public class ConsoleUtilsTests
{
    [Fact]
    public void GetBoundedConsoleWidth_WithOverride_ReturnsOverrideValue()
    {
        // Act & Assert
        Assert.Equal(120, ConsoleUtils.GetBoundedConsoleWidth(120));
        Assert.Equal(30, ConsoleUtils.GetBoundedConsoleWidth(30));
        Assert.Equal(250, ConsoleUtils.GetBoundedConsoleWidth(250));
    }

    [Fact]
    public void GetBoundedConsoleWidth_WithoutOverride_ReturnsBoundedValue()
    {
        // Act
        int width = ConsoleUtils.GetBoundedConsoleWidth(null);

        // Assert
        Assert.True(width >= 40);
        Assert.True(width <= 200);
    }

    [Fact]
    public void PadRightAnsi_NormalText_PadsToGreaterWidth()
    {
        // Act
        string result = ConsoleUtils.PadRightAnsi("hello", 10);

        // Assert
        Assert.Equal("hello     ", result);
    }

    [Fact]
    public void PadRightAnsi_NormalText_NoOpWhenWidthIsLessThanOrEqual()
    {
        // Act & Assert
        Assert.Equal("hello", ConsoleUtils.PadRightAnsi("hello", 5));
        Assert.Equal("hello", ConsoleUtils.PadRightAnsi("hello", 3));
    }

    [Fact]
    public void PadRightAnsi_WithAnsiEscapes_CalculatesVisibleLengthCorrectly()
    {
        // Act
        // "\x1b[31mhello\x1b[0m" has visible length 5 ("hello")
        string result = ConsoleUtils.PadRightAnsi("\x1b[31mhello\x1b[0m", 10);

        // Assert
        Assert.Equal("\x1b[31mhello\x1b[0m     ", result);
    }

    [Fact]
    public void PadRightAnsi_EndsWithReset_WithBgAnsi_StripsResetAndPads()
    {
        // Act
        // For text ending in "\x1b[0m" and having bgAnsi set:
        // it strips "\x1b[0m", appends padding, then appends "\x1b[0m"
        string result = ConsoleUtils.PadRightAnsi("hello\x1b[0m", 10, "\x1b[44m");

        // Assert
        // Length visible of "hello" (reset doesn't count).
        // It should strip reset: "hello" + "     " + "\x1b[0m"
        Assert.Equal("hello     \x1b[0m", result);
    }

    [Fact]
    public void PadRightAnsi_WithBgAnsi_NoReset_PrependsBgAnsiAndAppendsReset()
    {
        // Act
        string result = ConsoleUtils.PadRightAnsi("hello", 10, "\x1b[44m");

        // Assert
        Assert.Equal("\x1b[44mhello     \x1b[0m", result);
    }

    [Fact]
    public void PadRightAnsi_ShortOrEmptyWithReset_DoesNotThrow()
    {
        // Act & Assert
        var exEmpty = Record.Exception(() => ConsoleUtils.PadRightAnsi("", 5, "\x1b[44m"));
        Assert.Null(exEmpty);

        var exShort = Record.Exception(() => ConsoleUtils.PadRightAnsi("a", 5, "\x1b[44m"));
        Assert.Null(exShort);
        
        var exResetShort = Record.Exception(() => ConsoleUtils.PadRightAnsi("\x1b[0m", 5, "\x1b[44m"));
        Assert.Null(exResetShort);
    }
}
