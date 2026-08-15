using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic;

/// <summary>
/// Provides utility methods for statistical percentile calculations on numeric sequences.
/// </summary>
public static class PercentileCalculator
{
    private const double MaxPercentile = 100.0;

    /// <summary>
    /// Calculates the value at a specified percentile from a sequence of double-precision floating-point numbers
    /// using linear interpolation between the closest ranks.
    /// </summary>
    /// <param name="values">The sequence of values to calculate the percentile from.</param>
    /// <param name="percentile">The percentile value to calculate (must be between 0.0 and 100.0 inclusive).</param>
    /// <returns>The calculated percentile value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="percentile"/> is less than 0.0 or greater than 100.0.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="values"/> sequence is empty.</exception>
    public static double CalculatePercentile(IEnumerable<double> values, double percentile)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (percentile < 0.0 || percentile > MaxPercentile)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), $"Percentile must be between 0.0 and {MaxPercentile} inclusive.");
        }

        var sorted = values.OrderBy(v => v).ToArray();
        int n = sorted.Length;
        if (n == 0)
        {
            throw new ArgumentException("Values sequence cannot be empty.", nameof(values));
        }

        if (n == 1)
        {
            return sorted[0];
        }

        double interpolatedIndex = (percentile / MaxPercentile) * (n - 1);
        int low = (int)Math.Floor(interpolatedIndex);
        int high = (int)Math.Ceiling(interpolatedIndex);

        if (low == high)
        {
            return sorted[low];
        }

        return sorted[low] + (interpolatedIndex - low) * (sorted[high] - sorted[low]);
    }
}
