namespace QueueLab.Api;

/// <summary>Runtime settings (the C# counterpart of config.py).</summary>
public sealed class AppSettings
{
    public AppSettings(string observedDataFolder)
    {
        ObservedDataFolder = observedDataFolder;
    }

    /// <summary>Folder that contains nadra_day1_first100.xlsx.</summary>
    public string ObservedDataFolder { get; }

    /// <summary>Largest accepted request body (uploads included), 50 MB like the Flask app.</summary>
    public const long MaxRequestBodyBytes = 50L * 1024 * 1024;
}
