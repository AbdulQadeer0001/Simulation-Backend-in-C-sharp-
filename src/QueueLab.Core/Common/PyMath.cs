using System.Globalization;
using System.Numerics;

namespace QueueLab.Core.Common;

/// <summary>
/// Numeric helpers that reproduce Python semantics exactly. The original backend rounds almost every
/// value with <c>round(x, n)</c> and adds floats with <c>sum()</c>; C#'s <c>Math.Round</c> and plain
/// loops give slightly different results in edge cases, so the behaviour is ported precisely here.
/// </summary>
public static class PyMath
{
    private static readonly double[] Pow10Table =
    {
        1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11,
        1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22,
    };

    private static readonly double NegativeZero = BitConverter.Int64BitsToDouble(long.MinValue);

    /// <summary>
    /// Python's <c>round(x, ndigits)</c> for floats: the decimal rounding of the exact binary value,
    /// with ties going to the even digit (so round(0.125, 2) == 0.12).
    /// </summary>
    public static double Round(double x, int ndigits)
    {
        if (double.IsNaN(x) || double.IsInfinity(x) || x == 0.0)
        {
            return x;
        }

        if (ndigits >= 0 && ndigits < Pow10Table.Length)
        {
            double p10 = Pow10Table[ndigits];
            double y = Math.Abs(x) * p10;

            // Fast path: far from a rounding tie, ordinary floating point is provably correct.
            if (y < 1e9)
            {
                double f = Math.Floor(y);
                double frac = y - f;
                if (Math.Abs(frac - 0.5) > 1e-6)
                {
                    double q = frac > 0.5 ? f + 1.0 : f;
                    double r = q / p10;
                    return x < 0 ? -r : r;
                }
            }
        }

        return RoundExact(x, ndigits);
    }

    public static double? Round(double? x, int ndigits)
    {
        return x.HasValue ? Round(x.Value, ndigits) : (double?)null;
    }

    private static double RoundExact(double x, int ndigits)
    {
        long bits = BitConverter.DoubleToInt64Bits(x);
        bool negative = bits < 0;
        int exponent = (int)((bits >> 52) & 0x7FF);
        long fraction = bits & 0xFFFFFFFFFFFFFL;

        if (exponent == 0)
        {
            exponent = 1; // subnormal
        }
        else
        {
            fraction |= 1L << 52;
        }

        int e = exponent - 1075; // value = fraction * 2^e
        BigInteger mantissa = new BigInteger(fraction);
        BigInteger p10 = BigInteger.Pow(new BigInteger(10), ndigits);
        BigInteger q;

        if (e >= 0)
        {
            q = (mantissa << e) * p10;
        }
        else
        {
            BigInteger numerator = mantissa * p10;
            BigInteger denominator = BigInteger.One << (-e);
            BigInteger remainder;
            q = BigInteger.DivRem(numerator, denominator, out remainder);
            int cmp = (remainder * 2).CompareTo(denominator);
            if (cmp > 0 || (cmp == 0 && !q.IsEven))
            {
                q += BigInteger.One;
            }
        }

        if (q.IsZero)
        {
            return negative ? NegativeZero : 0.0;
        }

        string digits = q.ToString(CultureInfo.InvariantCulture).PadLeft(ndigits + 1, '0');
        string text = ndigits > 0
            ? digits.Substring(0, digits.Length - ndigits) + "." + digits.Substring(digits.Length - ndigits)
            : digits;

        double value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        return negative ? -value : value;
    }

    /// <summary>
    /// Python 3.12+ <c>sum()</c> over floats (Neumaier compensated summation).
    /// </summary>
    public static double Sum(IEnumerable<double> values)
    {
        double result = 0.0;
        double c = 0.0;

        foreach (double x in values)
        {
            double t = result + x;
            if (Math.Abs(result) >= Math.Abs(x))
            {
                c += (result - t) + x;
            }
            else
            {
                c += (x - t) + result;
            }

            result = t;
        }

        if (c != 0.0 && double.IsFinite(c))
        {
            result += c;
        }

        return result;
    }
}
