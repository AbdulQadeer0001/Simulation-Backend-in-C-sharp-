using System.Globalization;

namespace QueueLab.Desktop.Ui;

/// <summary>Number formatting, matching the web UI's fmt() and pct() helpers.</summary>
internal static class Fmt
{
    public static string Num(double? value, int decimals = 3)
    {
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            return "—";
        }

        return value.Value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    public static string Pct(double? value)
    {
        if (!value.HasValue || double.IsNaN(value.Value))
        {
            return "—";
        }

        return (value.Value * 100).ToString("F1", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>"average_waiting_time" becomes "Average Waiting Time".</summary>
    public static string Label(string key)
    {
        string[] words = key.Split('_');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            }
        }

        return string.Join(" ", words);
    }
}
