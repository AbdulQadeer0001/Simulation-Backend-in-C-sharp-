using System.Globalization;
using System.Numerics;
using QueueLab.Core.Common;
using QueueLab.Core.Simulation;

namespace QueueLab.Api;

/// <summary>Reading and validating request bodies the way the original Flask routes did.</summary>
internal static class RequestParsing
{
    /// <summary>
    /// Equivalent of Flask's <c>request.get_json(silent=True)</c>: returns null when the body is not JSON
    /// (wrong content type or invalid syntax).
    /// </summary>
    public static async Task<object?> ReadJsonBody(HttpRequest request)
    {
        string? contentType = request.ContentType;
        if (!IsJsonContentType(contentType))
        {
            return null;
        }

        (bool ok, object? value) = await JsonTree.TryParseAsync(request.Body, request.HttpContext.RequestAborted);
        return ok ? value : null;
    }

    private static bool IsJsonContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        string mediaType = contentType.Split(';')[0].Trim().ToLowerInvariant();
        return mediaType == "application/json"
            || (mediaType.StartsWith("application/", StringComparison.Ordinal)
                && mediaType.EndsWith("+json", StringComparison.Ordinal));
    }

    public static bool IsSimulationType(object? value)
    {
        string? text = value as string;
        return text == "single_server" || text == "multi_server";
    }

    /// <summary>Validates a server count and converts it to int (the C# version also caps it).</summary>
    public static int ToServerCount(object value)
    {
        BigInteger count = PyTypes.ToBigInteger(value);
        if (count > MultiServerSimulation.MaxServers)
        {
            throw new ValueError(
                "Server count must not exceed "
                + MultiServerSimulation.MaxServers.ToString("N0", CultureInfo.InvariantCulture)
                + ".");
        }

        return (int)count;
    }

    /// <summary>
    /// Python's <c>float(value)</c>, then "finite, whole number >= 1" (used for server_count in
    /// <c>/simulation/run</c> and the upload form).
    /// </summary>
    public static int ToServerCountFromNumber(double numeric)
    {
        if (!double.IsFinite(numeric) || numeric != Math.Floor(numeric) || numeric < 1)
        {
            throw new ValueError("server_count must be a positive integer.");
        }

        if (numeric > MultiServerSimulation.MaxServers)
        {
            throw new ValueError(
                "Server count must not exceed "
                + MultiServerSimulation.MaxServers.ToString("N0", CultureInfo.InvariantCulture)
                + ".");
        }

        return (int)numeric;
    }
}
