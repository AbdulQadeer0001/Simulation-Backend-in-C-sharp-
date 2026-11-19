using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QueueLab.Api;

/// <summary>JSON settings shared by every response.</summary>
internal static class ApiJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new JsonSerializerOptions
        {
            // Like Python's json module, never fail on NaN / Infinity.
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };
        options.Converters.Add(new BigIntegerConverter());
        return options;
    }

    private sealed class BigIntegerConverter : JsonConverter<BigInteger>
    {
        public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Responses are only ever written; request bodies are parsed by JsonTree instead.
            throw new NotSupportedException();
        }

        public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
        {
            writer.WriteRawValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}

/// <summary>A JSON response with an explicit status code.</summary>
internal sealed class JsonBodyResult : IResult
{
    private readonly object? _body;
    private readonly int _statusCode;

    public JsonBodyResult(object? body, int statusCode = 200)
    {
        _body = body;
        _statusCode = statusCode;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = _statusCode;
        httpContext.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(httpContext.Response.Body, _body, ApiJson.Options, httpContext.RequestAborted);
    }
}

/// <summary>A downloadable file (CSV text or Excel bytes).</summary>
internal sealed class FileBodyResult : IResult
{
    private readonly byte[] _content;
    private readonly string _contentType;
    private readonly string _fileName;

    public FileBodyResult(byte[] content, string contentType, string fileName)
    {
        _content = content;
        _contentType = contentType;
        _fileName = fileName;
    }

    public static FileBodyResult FromText(string text, string contentType, string fileName)
    {
        return new FileBodyResult(new UTF8Encoding(false).GetBytes(text), contentType, fileName);
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = 200;
        httpContext.Response.ContentType = _contentType;
        httpContext.Response.Headers["Content-Disposition"] = "attachment; filename=\"" + _fileName + "\"";
        httpContext.Response.ContentLength = _content.Length;
        await httpContext.Response.Body.WriteAsync(_content, 0, _content.Length, httpContext.RequestAborted);
    }
}

/// <summary>The two error shapes used by the original API.</summary>
internal static class Errors
{
    /// <summary>{"status": "error", "message": ...}</summary>
    public static IResult StatusMessage(int statusCode, string message)
    {
        Dictionary<string, object?> body = new Dictionary<string, object?>();
        body["status"] = "error";
        body["message"] = message;
        return new JsonBodyResult(body, statusCode);
    }

    /// <summary>{"error": ...}</summary>
    public static IResult ErrorOnly(int statusCode, string message)
    {
        Dictionary<string, object?> body = new Dictionary<string, object?>();
        body["error"] = message;
        return new JsonBodyResult(body, statusCode);
    }
}
