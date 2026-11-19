using System.Globalization;
using System.Numerics;
using QueueLab.Core.Common;

namespace QueueLab.Desktop.Pages;

/// <summary>Parsing and validating what the user typed into the form fields.</summary>
internal static class Inputs
{
    public const int MaxCustomers = 1_000_000;

    public const int MaxServers = 100_000;

    public static string NadraFile
    {
        get { return Path.Combine(AppContext.BaseDirectory, "data", "observed", "nadra_day1_first100.xlsx"); }
    }

    public static double PositiveNumber(string? text, string name)
    {
        double value;
        if (!PyFloat.TryParse(text, out value) || !double.IsFinite(value) || value <= 0)
        {
            throw new ValueError(name + " must be a positive number.");
        }

        return value;
    }

    public static int PositiveInt(string? text, string name, int max)
    {
        double value;
        if (!PyFloat.TryParse(text, out value) || !double.IsFinite(value) || value != Math.Floor(value) || value < 1)
        {
            throw new ValueError(name + " must be a whole number of at least 1.");
        }

        if (value > max)
        {
            throw new ValueError(name + " must not exceed " + max.ToString("N0", CultureInfo.InvariantCulture) + ".");
        }

        return (int)value;
    }

    /// <summary>A whole-number seed; an empty field means "random".</summary>
    public static BigInteger? OptionalSeed(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        BigInteger seed;
        if (!BigInteger.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out seed))
        {
            throw new ValueError("Seed must be a whole number (or empty for a random seed).");
        }

        return seed;
    }

    public static long? OptionalLongSeed(string? text)
    {
        BigInteger? seed = OptionalSeed(text);
        if (!seed.HasValue)
        {
            return null;
        }

        if (seed.Value < long.MinValue || seed.Value > long.MaxValue)
        {
            throw new ValueError("Seed is too large.");
        }

        return (long)seed.Value;
    }

    public static double ConfidenceLevel(string? text)
    {
        double value;
        if (!PyFloat.TryParse(text, out value) || !(value > 0 && value < 1))
        {
            throw new ValueError("Confidence level must be a number between 0 and 1 (for example 0.95).");
        }

        return value;
    }
}
