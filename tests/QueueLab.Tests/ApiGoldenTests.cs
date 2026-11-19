using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace QueueLab.Tests;

/// <summary>
/// Sends the same requests the original Flask backend was tested with and checks that the C# API answers with
/// the same status code and the same JSON (or CSV text). The expected answers in Golden/api_golden.json were
/// recorded by running the original Python code.
/// </summary>
public class ApiGoldenTests : IClassFixture<ApiGoldenTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("ObservedDataFolder", FindObservedFolder());
        }

        private static string FindObservedFolder()
        {
            string? directory = AppContext.BaseDirectory;
            while (directory != null)
            {
                string candidate = Path.Combine(directory, "src", "QueueLab.Api", "data", "observed");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = Path.GetDirectoryName(directory);
            }

            return Path.Combine(AppContext.BaseDirectory, "data", "observed");
        }
    }

    private static readonly Dictionary<string, JsonElement> Cases = LoadCases();

    private readonly Factory _factory;

    public ApiGoldenTests(Factory factory)
    {
        _factory = factory;
    }

    private static Dictionary<string, JsonElement> LoadCases()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Golden", "api_golden.json");
        JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        Dictionary<string, JsonElement> cases = new Dictionary<string, JsonElement>();
        foreach (JsonElement item in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            cases[item.GetProperty("name").GetString()!] = item;
        }

        return cases;
    }

    public static IEnumerable<object[]> CaseNames()
    {
        foreach (string name in Cases.Keys)
        {
            yield return new object[] { name };
        }
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task MatchesPythonBackend(string name)
    {
        JsonElement testCase = Cases[name];
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await Send(client, testCase);

        int expectedStatus = testCase.GetProperty("status").GetInt32();
        string text = await response.Content.ReadAsStringAsync();

        Assert.True(
            expectedStatus == (int)response.StatusCode,
            "Status: expected " + expectedStatus + " but got " + (int)response.StatusCode + ". Body: " + Truncate(text));

        JsonElement expectedJson;
        if (testCase.TryGetProperty("expected", out expectedJson))
        {
            Assert.Contains("application/json", response.Content.Headers.ContentType?.MediaType ?? string.Empty);
            using (JsonDocument actual = JsonDocument.Parse(text))
            {
                List<string> problems = JsonCompare.Diff(expectedJson, actual.RootElement);
                Assert.True(problems.Count == 0, "Differences from the Python backend:\n" + JsonCompare.Report(problems));
            }

            return;
        }

        JsonElement expectedText;
        if (testCase.TryGetProperty("expected_text", out expectedText))
        {
            Assert.Equal(expectedText.GetString(), text);
            Assert.StartsWith("text/csv", response.Content.Headers.ContentType?.MediaType ?? string.Empty);
            return;
        }

        // Excel downloads: the file must be a readable workbook with a header row and data.
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        using (MemoryStream stream = new MemoryStream(bytes))
        using (XLWorkbook workbook = new XLWorkbook(stream))
        {
            IXLWorksheet sheet = workbook.Worksheets.First();
            Assert.True(sheet.LastRowUsed()!.RowNumber() >= 2, "Excel export should contain a header and at least one data row.");
            Assert.False(sheet.Cell(1, 1).IsEmpty());
        }
    }

    private static string Truncate(string text)
    {
        return text.Length > 300 ? text.Substring(0, 300) + "..." : text;
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, JsonElement testCase)
    {
        string method = testCase.GetProperty("method").GetString()!;
        string path = testCase.GetProperty("path").GetString()!;

        if (method == "GET")
        {
            return await client.GetAsync(path);
        }

        if (method == "UPLOAD")
        {
            using (MultipartFormDataContent form = new MultipartFormDataContent())
            {
                foreach (JsonProperty field in testCase.GetProperty("form").EnumerateObject())
                {
                    form.Add(new StringContent(field.Value.GetString() ?? string.Empty), field.Name);
                }

                JsonElement file;
                if (testCase.TryGetProperty("file", out file))
                {
                    byte[] content = Convert.FromBase64String(file.GetProperty("content_base64").GetString()!);
                    ByteArrayContent fileContent = new ByteArrayContent(content);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    form.Add(fileContent, "file", file.GetProperty("name").GetString()!);
                }

                return await client.PostAsync(path, form);
            }
        }

        JsonElement raw;
        if (testCase.TryGetProperty("raw_body", out raw))
        {
            string contentType = testCase.GetProperty("content_type").GetString()!;
            return await client.PostAsync(path, new StringContent(raw.GetString()!, Encoding.UTF8, contentType));
        }

        string json = testCase.GetProperty("body").GetRawText();
        return await client.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));
    }
}
