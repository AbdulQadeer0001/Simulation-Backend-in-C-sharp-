using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace QueueLab.Core.Common;

/// <summary>
/// Parses JSON into plain .NET values while preserving Python's int/float distinction
/// (<c>3</c> is an integer, <c>3.0</c> and <c>3e0</c> are floats). The result consists of
/// null, bool, string, long/BigInteger, double, <c>List&lt;object?&gt;</c> and
/// <c>Dictionary&lt;string, object?&gt;</c> (object keys keep their order; duplicate keys: last value wins).
/// </summary>
public static class JsonTree
{
    /// <summary>Parses text; returns false (instead of throwing) when it is not valid JSON.</summary>
    public static bool TryParse(string text, out object? result)
    {
        result = null;
        try
        {
            using (JsonDocument doc = JsonDocument.Parse(text))
            {
                result = FromElement(doc.RootElement);
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static async Task<(bool ok, object? value)> TryParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            using (JsonDocument doc = await JsonDocument.ParseAsync(stream, default, cancellationToken))
            {
                return (true, FromElement(doc.RootElement));
            }
        }
        catch (JsonException)
        {
            return (false, null);
        }
    }

    public static object? FromElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                Dictionary<string, object?> dict = new Dictionary<string, object?>();
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    dict[property.Name] = FromElement(property.Value);
                }

                return dict;

            case JsonValueKind.Array:
                List<object?> list = new List<object?>();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    list.Add(FromElement(item));
                }

                return list;

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                return ConvertNumber(element.GetRawText());

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            default:
                return null;
        }
    }

    private static object ConvertNumber(string raw)
    {
        bool isFloat = raw.IndexOf('.') >= 0 || raw.IndexOf('e') >= 0 || raw.IndexOf('E') >= 0;

        if (isFloat)
        {
            return double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        long l;
        if (long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out l))
        {
            return l;
        }

        return BigInteger.Parse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }
}
