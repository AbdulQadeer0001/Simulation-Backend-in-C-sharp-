using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace QueueLab.Core.Common;

/// <summary>Equivalent of Python <c>json.dumps(value, ensure_ascii=False)</c> (default separators ", " and ": ").</summary>
public static class PyJson
{
    public static string Dumps(object? value)
    {
        StringBuilder sb = new StringBuilder();
        Write(sb, value);
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, object? value)
    {
        if (value == null)
        {
            sb.Append("null");
        }
        else if (value is bool b)
        {
            sb.Append(b ? "true" : "false");
        }
        else if (value is string s)
        {
            WriteString(sb, s);
        }
        else if (value is long l)
        {
            sb.Append(l.ToString(CultureInfo.InvariantCulture));
        }
        else if (value is int i)
        {
            sb.Append(i.ToString(CultureInfo.InvariantCulture));
        }
        else if (value is BigInteger big)
        {
            sb.Append(big.ToString(CultureInfo.InvariantCulture));
        }
        else if (value is double d)
        {
            if (double.IsNaN(d))
            {
                sb.Append("NaN");
            }
            else if (double.IsPositiveInfinity(d))
            {
                sb.Append("Infinity");
            }
            else if (double.IsNegativeInfinity(d))
            {
                sb.Append("-Infinity");
            }
            else
            {
                sb.Append(PyFloat.Repr(d));
            }
        }
        else if (value is IDictionary dict)
        {
            sb.Append('{');
            bool first = true;
            foreach (DictionaryEntry entry in dict)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                first = false;
                WriteString(sb, Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty);
                sb.Append(": ");
                Write(sb, entry.Value);
            }

            sb.Append('}');
        }
        else if (value is IEnumerable sequence)
        {
            sb.Append('[');
            bool first = true;
            foreach (object? item in sequence)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                first = false;
                Write(sb, item);
            }

            sb.Append(']');
        }
        else
        {
            WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
        }
    }

    private static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append('"');
    }
}
