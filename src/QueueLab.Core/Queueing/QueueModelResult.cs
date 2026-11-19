namespace QueueLab.Core.Queueing;

/// <summary>Result of an analytical (steady-state) queueing model calculation.</summary>
public sealed class QueueModelResult
{
    /// <summary>"M/M/1" or "M/M/c".</summary>
    public string Model { get; init; } = string.Empty;

    public Dictionary<string, object> Assumptions { get; init; } = new Dictionary<string, object>();

    public Dictionary<string, double> Input { get; init; } = new Dictionary<string, double>();

    public Dictionary<string, double> Metrics { get; init; } = new Dictionary<string, double>();

    /// <summary>JSON object of the "result" field.</summary>
    public Dictionary<string, object?> ToDict()
    {
        Dictionary<string, object?> assumptions = new Dictionary<string, object?>();
        foreach (KeyValuePair<string, object> pair in Assumptions)
        {
            assumptions[pair.Key] = pair.Value;
        }

        Dictionary<string, object?> input = new Dictionary<string, object?>();
        foreach (KeyValuePair<string, double> pair in Input)
        {
            input[pair.Key] = pair.Value;
        }

        Dictionary<string, object?> metrics = new Dictionary<string, object?>();
        foreach (KeyValuePair<string, double> pair in Metrics)
        {
            metrics[pair.Key] = pair.Value;
        }

        Dictionary<string, object?> d = new Dictionary<string, object?>();
        d["model"] = Model;
        d["assumptions"] = assumptions;
        d["input"] = input;
        d["metrics"] = metrics;
        return d;
    }
}
