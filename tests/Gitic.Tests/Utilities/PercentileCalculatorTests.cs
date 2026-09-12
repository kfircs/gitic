using System;
using System.Collections.Generic;
using Xunit;

namespace Gitic.Tests;

public class PercentileCalculatorTests
{
    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(25.0, 3.25)]
    [InlineData(50.0, 5.5)]
    [InlineData(75.0, 7.75)]
    [InlineData(90.0, 9.1)]
    [InlineData(100.0, 10.0)]
    public void CalculatePercentile_WithUniformArray1To10_ReturnsCorrectValues(double percentile, double expected)
    {
        // Arrange
        var values = new double[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0 };

        // Act
        double actual = PercentileCalculator.CalculatePercentile(values, percentile);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CalculatePercentile_WithUniformArray1To100_ReturnsCorrectP50()
    {
        // Arrange
        var values = new List<double>();
        for (int i = 1; i <= 100; i++)
        {
            values.Add(i);
        }

        // Act
        double p50 = PercentileCalculator.CalculatePercentile(values, 50.0);

        // Assert
        Assert.Equal(50.5, p50);
    }

    [Theory]
    [InlineData(25.0, 17.5)]
    [InlineData(50.0, 60.0)]
    [InlineData(75.0, 325.0)]
    public void CalculatePercentile_WithSkewedArray_ReturnsCorrectValues(double percentile, double expected)
    {
        // Arrange
        var values = new double[] { 10.0, 20.0, 100.0, 1000.0 };

        // Act
        double actual = PercentileCalculator.CalculatePercentile(values, percentile);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CalculatePercentile_WithSingleItemArray_ReturnsThatItemForAnyPercentile()
    {
        // Arrange
        var values = new double[] { 42.0 };

        // Act & Assert
        Assert.Equal(42.0, PercentileCalculator.CalculatePercentile(values, 0.0));
        Assert.Equal(42.0, PercentileCalculator.CalculatePercentile(values, 50.0));
        Assert.Equal(42.0, PercentileCalculator.CalculatePercentile(values, 90.0));
        Assert.Equal(42.0, PercentileCalculator.CalculatePercentile(values, 100.0));
    }

    [Fact]
    public void CalculatePercentile_WithNullValues_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => PercentileCalculator.CalculatePercentile(null!, 50.0));
    }

    [Fact]
    public void CalculatePercentile_WithEmptyValues_ThrowsArgumentException()
    {
        // Arrange
        var values = Array.Empty<double>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => PercentileCalculator.CalculatePercentile(values, 50.0));
        Assert.Contains("Values sequence cannot be empty", ex.Message);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(-10.0)]
    [InlineData(100.1)]
    [InlineData(150.0)]
    public void CalculatePercentile_WithOutOfBoundsPercentile_ThrowsArgumentOutOfRangeException(double percentile)
    {
        // Arrange
        var values = new double[] { 1.0, 2.0, 3.0 };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => PercentileCalculator.CalculatePercentile(values, percentile));
    }
}
