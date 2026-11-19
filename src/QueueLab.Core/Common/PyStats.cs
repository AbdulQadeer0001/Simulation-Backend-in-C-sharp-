namespace QueueLab.Core.Common;

/// <summary>Equivalents of the helpers in Python's <c>statistics</c> module used by the backend.</summary>
public static class PyStats
{
    public static double Mean(IReadOnlyList<double> values)
    {
        return PyMath.Sum(values) / values.Count;
    }

    public static double Median(IReadOnlyList<double> values)
    {
        double[] sorted = values.ToArray();
        Array.Sort(sorted);
        int n = sorted.Length;
        int mid = n / 2;

        if (n % 2 == 1)
        {
            return sorted[mid];
        }

        return (sorted[mid - 1] + sorted[mid]) / 2;
    }

    /// <summary>Population standard deviation (<c>statistics.pstdev</c>).</summary>
    public static double PopulationStdDev(IReadOnlyList<double> values)
    {
        return Math.Sqrt(SumOfSquares(values) / values.Count);
    }

    /// <summary>Sample standard deviation (<c>statistics.stdev</c>); needs at least two values.</summary>
    public static double SampleStdDev(IReadOnlyList<double> values)
    {
        return Math.Sqrt(SumOfSquares(values) / (values.Count - 1));
    }

    private static double SumOfSquares(IReadOnlyList<double> values)
    {
        double mean = Mean(values);
        double[] squares = new double[values.Count];
        for (int i = 0; i < squares.Length; i++)
        {
            double d = values[i] - mean;
            squares[i] = d * d;
        }

        return PyMath.Sum(squares);
    }

    /// <summary>Percentile with linear interpolation (the <c>_percentile</c> helper of the original code).</summary>
    public static double? Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return null;
        }

        double[] ordered = values.ToArray();
        Array.Sort(ordered);

        if (ordered.Length == 1)
        {
            return ordered[0];
        }

        double position = (ordered.Length - 1) * percentile;
        int lowerIndex = (int)position;
        int upperIndex = Math.Min(lowerIndex + 1, ordered.Length - 1);
        double fraction = position - lowerIndex;

        return ordered[lowerIndex] * (1 - fraction) + ordered[upperIndex] * fraction;
    }
}
