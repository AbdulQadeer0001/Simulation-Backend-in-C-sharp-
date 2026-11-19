using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace QueueLab.Core.Common;

/// <summary>
/// Python <c>repr()</c>/<c>str()</c> of simple values. The original CSV export writes nested values with
/// <c>str(value)</c>, e.g. <c>{'1': 0.5}</c>, and this reproduces that text.
/// </summary>
public static class PyRepr
{
    public static string Of(object? value)
    {
        if (value == null)
        {
            return "None";
        }

        if (value is bool b)
        {
            return b ? "True" : "False";
        }

        if (value is string s)
        {
            return StringRepr(s);
        }

        if (value is long l)
        {
            return l.ToString(CultureInfo.InvariantCulture);
        }

        if (value is int i)
        {
            return i.ToString(CultureInfo.InvariantCulture);
        }

        if (value is BigInteger big)
        {
            return big.ToString(CultureInfo.InvariantCulture);
        }

        if (value is double d)
        {
            return PyFloat.Repr(d);
        }

        if (value is IDictionary dict)
        {
            List<string> parts = new List<string>();
            foreach (DictionaryEntry entry in dict)
            {
                parts.Add(Of(entry.Key) + ": " + Of(entry.Value));
            }

            return "{" + string.Join(", ", parts) + "}";
        }

        if (value is IEnumerable sequence)
        {
            List<string> parts = new List<string>();
            foreach (object? item in sequence)
            {
                parts.Add(Of(item));
            }

            return "[" + string.Join(", ", parts) + "]";
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "None";
    }

    /// <summary>Text of <c>str(value)</c>: like <see cref="Of"/>, except strings are returned unquoted.</summary>
    public static string Str(object? value)
    {
        return value is string s ? s : Of(value);
    }

    private static string StringRepr(string s)
    {
        char quote = s.Contains('\'') && !s.Contains('"') ? '"' : '\'';
        StringBuilder sb = new StringBuilder();
        sb.Append(quote);

        foreach (char c in s)
        {
            if (c == quote || c == '\\')
            {
                sb.Append('\\').Append(c);
            }
            else if (c == '\n')
            {
                sb.Append("\\n");
            }
            else if (c == '\r')
            {
                sb.Append("\\r");
            }
            else if (c == '\t')
            {
                sb.Append("\\t");
            }
            else if (c < 0x20 || c == 0x7f)
            {
                sb.Append("\\x").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(c);
            }
        }

        sb.Append(quote);
        return sb.ToString();
    }
}
