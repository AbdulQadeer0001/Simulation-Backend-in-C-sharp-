using System.Globalization;
using System.Numerics;

namespace QueueLab.Core.Common;

/// <summary>
/// Helpers for loosely-typed values (parsed JSON). The original Python code validated request fields with
/// <c>isinstance</c> checks, e.g. "an int but not a bool". Values here are:
/// null, bool, string, long/BigInteger (JSON integers), double (JSON decimals), <c>List&lt;object?&gt;</c>
/// and <c>Dictionary&lt;string, object?&gt;</c>.
/// </summary>
public static class PyTypes
{
    public static bool IsBool(object? v)
    {
        return v is bool;
    }

    /// <summary>True for integer values (a bool is NOT an int here, matching the original checks).</summary>
    public static bool IsInt(object? v)
    {
        return v is long || v is int || v is BigInteger;
    }

    public static bool IsFloat(object? v)
    {
        return v is double;
    }

    /// <summary>True for int or float values, never for bool.</summary>
    public static bool IsNumber(object? v)
    {
        return IsInt(v) || IsFloat(v);
    }

    public static BigInteger ToBigInteger(object v)
    {
        if (v is long l)
        {
            return new BigInteger(l);
        }

        if (v is int i)
        {
            return new BigInteger(i);
        }

        return (BigInteger)v;
    }

    /// <summary>Converts an int or float value to double.</summary>
    public static double ToDouble(object v)
    {
        if (v is double d)
        {
            return d;
        }

        if (v is long l)
        {
            return l;
        }

        if (v is int i)
        {
            return i;
        }

        if (v is BigInteger b)
        {
            return (double)b;
        }

        throw new ValueError("Expected a number.");
    }

    /// <summary>Python-style name of a value's type, used in error messages.</summary>
    public static string TypeName(object? v)
    {
        if (v == null)
        {
            return "NoneType";
        }

        if (v is bool)
        {
            return "bool";
        }

        if (v is string)
        {
            return "str";
        }

        if (IsInt(v))
        {
            return "int";
        }

        if (v is double)
        {
            return "float";
        }

        if (v is Dictionary<string, object?>)
        {
            return "dict";
        }

        return "list";
    }

    /// <summary>
    /// Equivalent of Python <c>float(value)</c> including its error messages. Accepts numbers, bools
    /// (True is 1.0) and numeric strings.
    /// </summary>
    public static double ToFloat(object? v)
    {
        if (v is bool b)
        {
            return b ? 1.0 : 0.0;
        }

        if (IsNumber(v))
        {
            return ToDouble(v!);
        }

        if (v is string s)
        {
            double parsed;
            if (PyFloat.TryParse(s, out parsed))
            {
                return parsed;
            }

            throw new ValueError("could not convert string to float: " + PyRepr.Of(s));
        }

        throw new ValueError(
            "float() argument must be a string or a real number, not '" + TypeName(v) + "'");
    }

    /// <summary>Equivalent of <c>dict.get(key, default)</c>: the default applies only when the key is missing.</summary>
    public static object? Get(Dictionary<string, object?> dict, string key, object? defaultValue = null)
    {
        object? value;
        return dict.TryGetValue(key, out value) ? value : defaultValue;
    }

    /// <summary>Reads an integer value as an int, failing if it does not fit.</summary>
    public static bool TryGetInt32(object? v, out int result)
    {
        result = 0;
        if (!IsInt(v))
        {
            return false;
        }

        BigInteger b = ToBigInteger(v!);
        if (b < int.MinValue || b > int.MaxValue)
        {
            return false;
        }

        result = (int)b;
        return true;
    }

    public static string FormatDouble(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }
}
