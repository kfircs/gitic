using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic;

public static class PercentileCalculator
{
    public static double CalculatePercentile(IEnumerable<double> values, double percentile)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (percentile < 0.0 || percentile > 100.0)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), "Percentile must be between 0.0 and 100.0 inclusive.");
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

        double idx = (percentile / 100.0) * (n - 1);
        int low = (int)Math.Floor(idx);
        int high = (int)Math.Ceiling(idx);

        if (low == high)
        {
            return sorted[low];
        }

        return sorted[low] + (idx - low) * (sorted[high] - sorted[low]);
    }
}
