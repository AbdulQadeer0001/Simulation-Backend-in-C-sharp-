using Microsoft.AspNetCore.Http.Features;
using QueueLab.Api;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Same address as `flask run`, so the React UI works without changes. Override with --urls or ASPNETCORE_URLS.
if (string.IsNullOrEmpty(builder.Configuration["urls"]) && string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]))
{
    builder.WebHost.UseUrls("http://127.0.0.1:5000");
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = AppSettings.MaxRequestBodyBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = AppSettings.MaxRequestBodyBytes;
});

// flask-cors default: any origin, any method, any header.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader().WithExposedHeaders("Content-Disposition");
    });
});

// Resolved lazily so that settings supplied by the host (or the test server) are honoured.
builder.Services.AddSingleton(serviceProvider => new AppSettings(
    serviceProvider.GetRequiredService<IConfiguration>()["ObservedDataFolder"]
    ?? Path.Combine(AppContext.BaseDirectory, "data", "observed")));

WebApplication app = builder.Build();

app.UseCors();

// Unexpected failures become a JSON 500 instead of an HTML error page.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception error) when (!context.Response.HasStarted)
    {
        app.Logger.LogError(error, "Unhandled exception");
        await new JsonBodyResult(
            new Dictionary<string, object?> { ["status"] = "error", ["message"] = error.Message },
            500).ExecuteAsync(context);
    }
});

RouteGroupBuilder api = app.MapGroup("/api");

api.MapGet("/health", () => new JsonBodyResult(new Dictionary<string, object?>
{
    ["status"] = "ok",
    ["application"] = "QueueLab",
    ["version"] = "0.1.0",
    ["message"] = "QueueLab backend is running",
}));

api.MapGet("/dataset/nadra", (AppSettings settings) => NadraEndpoints.Dataset(settings));
api.MapGet("/simulation/single-server/nadra", (AppSettings settings) => NadraEndpoints.SingleServer(settings));
api.MapGet(
    "/simulation/multi-server/nadra/{serverCount:int:min(0)}",
    (AppSettings settings, int serverCount) => NadraEndpoints.MultiServer(settings, serverCount));

api.MapPost("/queueing/mm1", QueueingEndpoints.Mm1);
api.MapPost("/queueing/mmc", QueueingEndpoints.Mmc);

api.MapPost("/simulation/run", SimulationEndpoints.Run);
api.MapPost("/simulation/generate-and-run", SimulationEndpoints.GenerateAndRun);
api.MapPost("/simulation/upload-and-run", SimulationEndpoints.UploadAndRun);
api.MapPost("/statistics/describe-upload", SimulationEndpoints.DescribeUpload);
api.MapPost("/simulation/replicate", SimulationEndpoints.Replicate);
api.MapPost("/simulation/compare", SimulationEndpoints.Compare);
api.MapPost("/simulation/export/csv", SimulationEndpoints.ExportCsv);
api.MapPost("/simulation/export/excel", SimulationEndpoints.ExportExcel);
api.MapPost("/simulation/run-and-export/csv", SimulationEndpoints.RunAndExportCsv);
api.MapPost("/simulation/run-and-export/excel", SimulationEndpoints.RunAndExportExcel);

app.Run();

/// <summary>Lets the integration tests start the app in memory.</summary>
public partial class Program
{
}
