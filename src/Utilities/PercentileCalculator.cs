using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic;

public static class PercentileCalculator
{
    private const double MaxPercentile = 100.0;

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
