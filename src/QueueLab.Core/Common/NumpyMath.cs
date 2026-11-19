namespace QueueLab.Core.Common;

/// <summary>
/// NumPy-compatible reductions. <c>np.mean</c> uses pairwise summation, which differs from a naive loop in the
/// last bits; porting it keeps the dataset summary numerically identical to the Python backend.
/// </summary>
public static class NumpyMath
{
    public static double Mean(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return double.NaN;
        }

        double[] a = new double[values.Count];
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = values[i];
        }

        return PairwiseSum(a, 0, a.Length) / a.Length;
    }

    public static double Min(IReadOnlyList<double> values)
    {
        double result = values[0];
        for (int i = 1; i < values.Count; i++)
        {
            if (values[i] < result)
            {
                result = values[i];
            }
        }

        return result;
    }

    public static double Max(IReadOnlyList<double> values)
    {
        double result = values[0];
        for (int i = 1; i < values.Count; i++)
        {
            if (values[i] > result)
            {
                result = values[i];
            }
        }

        return result;
    }

    private static double PairwiseSum(double[] a, int start, int n)
    {
        if (n < 8)
        {
            double res = 0.0;
            for (int i = 0; i < n; i++)
            {
                res += a[start + i];
            }

            return res;
        }

        if (n <= 128)
        {
            double r0 = a[start];
            double r1 = a[start + 1];
            double r2 = a[start + 2];
            double r3 = a[start + 3];
            double r4 = a[start + 4];
            double r5 = a[start + 5];
            double r6 = a[start + 6];
            double r7 = a[start + 7];

            int i = 8;
            int limit = n - (n % 8);
            for (; i < limit; i += 8)
            {
                r0 += a[start + i];
                r1 += a[start + i + 1];
                r2 += a[start + i + 2];
                r3 += a[start + i + 3];
                r4 += a[start + i + 4];
                r5 += a[start + i + 5];
                r6 += a[start + i + 6];
                r7 += a[start + i + 7];
            }

            double res = ((r0 + r1) + (r2 + r3)) + ((r4 + r5) + (r6 + r7));
            for (; i < n; i++)
            {
                res += a[start + i];
            }

            return res;
        }

        int n2 = n / 2;
        n2 -= n2 % 8;
        return PairwiseSum(a, start, n2) + PairwiseSum(a, start + n2, n - n2);
    }
}
