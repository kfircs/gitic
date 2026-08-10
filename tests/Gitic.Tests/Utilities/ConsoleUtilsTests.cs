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
}
