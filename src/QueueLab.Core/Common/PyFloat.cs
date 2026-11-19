using System.Globalization;
using System.Text.RegularExpressions;

namespace QueueLab.Core.Common;

/// <summary>Python-compatible float formatting (<c>repr</c>) and parsing (<c>float()</c>).</summary>
public static class PyFloat
{
    private static readonly Regex NumberPattern = new Regex(
        @"^(?:\d+(?:_\d+)*(?:\.(?:\d+(?:_\d+)*)?)?|\.\d+(?:_\d+)*)(?:[eE][+-]?\d+(?:_\d+)*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Equivalent of Python <c>repr(float)</c>, e.g. 1e-05, 100.0, 1e+22, 0.30000000000000004.</summary>
    public static string Repr(double x)
    {
        if (double.IsNaN(x))
        {
            return "nan";
        }

        if (double.IsPositiveInfinity(x))
        {
            return "inf";
        }

        if (double.IsNegativeInfinity(x))
        {
            return "-inf";
        }

        // .NET Core 3.0+ returns the shortest string that round-trips; only the layout differs from Python.
        string s = x.ToString("R", CultureInfo.InvariantCulture);
        bool negative = s[0] == '-';
        if (negative)
        {
            s = s.Substring(1);
        }

        int exp10 = 0;
        int ePos = s.IndexOfAny(new[] { 'E', 'e' });
        if (ePos >= 0)
        {
            exp10 = int.Parse(s.Substring(ePos + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            s = s.Substring(0, ePos);
        }

        string intPart;
        string fracPart;
        int dot = s.IndexOf('.');
        if (dot >= 0)
        {
            intPart = s.Substring(0, dot);
            fracPart = s.Substring(dot + 1);
        }
        else
        {
            intPart = s;
            fracPart = string.Empty;
        }

        string all = intPart + fracPart;
        int decpt = intPart.Length + exp10;
        int lead = 0;
        while (lead < all.Length && all[lead] == '0')
        {
            lead++;
        }

        decpt -= lead;
        string digits = all.Substring(lead).TrimEnd('0');

        if (digits.Length == 0)
        {
            return negative ? "-0.0" : "0.0";
        }

        int n = digits.Length;
        string body;

        if (decpt > -4 && decpt <= 16)
        {
            if (decpt <= 0)
            {
                body = "0." + new string('0', -decpt) + digits;
            }
            else if (decpt >= n)
            {
                body = digits + new string('0', decpt - n) + ".0";
            }
            else
            {
                body = digits.Substring(0, decpt) + "." + digits.Substring(decpt);
            }
        }
        else
        {
            int e = decpt - 1;
            string mantissa = digits.Substring(0, 1) + (n > 1 ? "." + digits.Substring(1) : string.Empty);
            body = mantissa + "e" + (e >= 0 ? "+" : "-") + Math.Abs(e).ToString("00", CultureInfo.InvariantCulture);
        }

        return negative ? "-" + body : body;
    }

    /// <summary>
    /// Equivalent of Python <c>float(text)</c>: surrounding whitespace is ignored, and
    /// "inf", "infinity" and "nan" (any case, optional sign) as well as digit underscores are accepted.
    /// </summary>
    public static bool TryParse(string? text, out double value)
    {
        value = 0.0;
        if (text == null)
        {
            return false;
        }

        string s = text.Trim();
        if (s.Length == 0)
        {
            return false;
        }

        bool negative = false;
        int pos = 0;
        if (s[0] == '+' || s[0] == '-')
        {
            negative = s[0] == '-';
            pos = 1;
        }

        string rest = s.Substring(pos);
        string lower = rest.ToLowerInvariant();

        if (lower == "inf" || lower == "infinity")
        {
            value = negative ? double.NegativeInfinity : double.PositiveInfinity;
            return true;
        }

        if (lower == "nan")
        {
            value = double.NaN;
            return true;
        }

        if (!NumberPattern.IsMatch(rest))
        {
            return false;
        }

        string cleaned = rest.Replace("_", string.Empty);
        double parsed;
        if (!double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
        {
            return false;
        }

        value = negative ? -parsed : parsed;
        return true;
    }
}
