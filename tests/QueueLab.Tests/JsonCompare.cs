using System.Text;
using System.Text.Json;

namespace QueueLab.Tests;

/// <summary>Deep JSON comparison: numbers match within a tiny tolerance, everything else exactly.</summary>
internal static class JsonCompare
{
    public static List<string> Diff(JsonElement expected, JsonElement actual, string path = "$")
    {
        List<string> problems = new List<string>();
        Compare(expected, actual, path, problems);
        return problems;
    }

    private static void Compare(JsonElement expected, JsonElement actual, string path, List<string> problems)
    {
        if (expected.ValueKind == JsonValueKind.True || expected.ValueKind == JsonValueKind.False)
        {
            if (expected.ValueKind != actual.ValueKind)
            {
                problems.Add(path + ": expected " + expected.ValueKind + " but got " + actual.ValueKind);
            }

            return;
        }

        if (expected.ValueKind != actual.ValueKind)
        {
            problems.Add(path + ": expected " + Short(expected) + " but got " + Short(actual));
            return;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                HashSet<string> expectedKeys = new HashSet<string>();
                foreach (JsonProperty property in expected.EnumerateObject())
                {
                    expectedKeys.Add(property.Name);
                    JsonElement other;
                    if (!actual.TryGetProperty(property.Name, out other))
                    {
                        problems.Add(path + "." + property.Name + ": missing in actual response");
                        continue;
                    }

                    Compare(property.Value, other, path + "." + property.Name, problems);
                }

                foreach (JsonProperty property in actual.EnumerateObject())
                {
                    if (!expectedKeys.Contains(property.Name))
                    {
                        problems.Add(path + "." + property.Name + ": unexpected extra key");
                    }
                }

                break;

            case JsonValueKind.Array:
                int expectedCount = expected.GetArrayLength();
                int actualCount = actual.GetArrayLength();
                if (expectedCount != actualCount)
                {
                    problems.Add(path + ": expected " + expectedCount + " items but got " + actualCount);
                    return;
                }

                for (int i = 0; i < expectedCount; i++)
                {
                    Compare(expected[i], actual[i], path + "[" + i + "]", problems);
                }

                break;

            case JsonValueKind.Number:
                double e = expected.GetDouble();
                double a = actual.GetDouble();
                double tolerance = 1e-9 * Math.Max(1.0, Math.Abs(e));
                if (!(Math.Abs(e - a) <= tolerance))
                {
                    problems.Add(path + ": expected " + e.ToString("R") + " but got " + a.ToString("R"));
                }

                break;

            case JsonValueKind.String:
                if (expected.GetString() != actual.GetString())
                {
                    problems.Add(path + ": expected \"" + expected.GetString() + "\" but got \"" + actual.GetString() + "\"");
                }

                break;
        }
    }

    private static string Short(JsonElement element)
    {
        string text = element.GetRawText();
        return text.Length > 80 ? text.Substring(0, 80) + "..." : text;
    }

    public static string Report(List<string> problems)
    {
        StringBuilder sb = new StringBuilder();
        foreach (string problem in problems.Take(15))
        {
            sb.AppendLine(problem);
        }

        if (problems.Count > 15)
        {
            sb.AppendLine("... and " + (problems.Count - 15) + " more");
        }

        return sb.ToString();
    }
}
