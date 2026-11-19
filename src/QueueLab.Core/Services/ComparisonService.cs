using QueueLab.Core.Common;
using QueueLab.Core.Statistics;

namespace QueueLab.Core.Services;

/// <summary>Replicated results of one scenario in a comparison.</summary>
public sealed class ScenarioResult
{
    public int ScenarioId { get; init; }

    public string ScenarioName { get; init; } = string.Empty;

    public string SimulationType { get; init; } = string.Empty;

    public int ServerCount { get; init; }

    public int CustomerCountPerReplication { get; init; }

    public object? ArrivalRate { get; init; }

    public object? ServiceRate { get; init; }

    public object? ArrivalDistribution { get; init; }

    public object? ServiceDistribution { get; init; }

    public int ReplicationCount { get; init; }

    public Dictionary<string, MetricSummary?> Metrics { get; init; } = new Dictionary<string, MetricSummary?>();

    public Dictionary<string, object?> ToDict()
    {
        Dictionary<string, object?> metrics = new Dictionary<string, object?>();
        foreach (KeyValuePair<string, MetricSummary?> pair in Metrics)
        {
            metrics[pair.Key] = pair.Value == null ? null : pair.Value.ToDict();
        }

        Dictionary<string, object?> d = new Dictionary<string, object?>();
        d["scenario_id"] = ScenarioId;
        d["scenario_name"] = ScenarioName;
        d["simulation_type"] = SimulationType;
        d["server_count"] = ServerCount;
        d["customer_count_per_replication"] = CustomerCountPerReplication;
        d["arrival_rate"] = ArrivalRate;
        d["service_rate"] = ServiceRate;
        d["arrival_distribution"] = ArrivalDistribution;
        d["service_distribution"] = ServiceDistribution;
        d["replication_count"] = ReplicationCount;
        d["metrics"] = metrics;
        return d;
    }
}

public sealed class ComparisonResult
{
    public List<ScenarioResult> Scenarios { get; init; } = new List<ScenarioResult>();

    public Dictionary<string, object?> ToDict()
    {
        List<object?> scenarios = new List<object?>();
        foreach (ScenarioResult scenario in Scenarios)
        {
            scenarios.Add(scenario.ToDict());
        }

        Dictionary<string, object?> d = new Dictionary<string, object?>();
        d["scenario_count"] = Scenarios.Count;
        d["scenarios"] = scenarios;
        return d;
    }
}

/// <summary>Compares several queueing configurations by replicating each one.</summary>
public static class ComparisonService
{
    public const int MaxScenarios = 20;

    /// <summary>
    /// Compares scenarios. <paramref name="scenarios"/> is a list of scenario objects
    /// (<c>Dictionary&lt;string, object?&gt;</c>) with the same fields as the JSON API.
    /// </summary>
    public static ComparisonResult CompareScenarios(object? scenarios)
    {
        List<object?>? list = scenarios as List<object?>;
        if (list == null || list.Count == 0)
        {
            throw new ValueError("scenarios must be a non-empty list.");
        }

        if (list.Count > MaxScenarios)
        {
            throw new ValueError("A maximum of 20 scenarios is allowed.");
        }

        List<ScenarioResult> results = new List<ScenarioResult>();

        for (int index = 0; index < list.Count; index++)
        {
            Dictionary<string, object?>? scenario = list[index] as Dictionary<string, object?>;
            if (scenario == null)
            {
                throw new ValueError("Scenario " + (index + 1) + " must be an object.");
            }

            object? scenarioName = PyTypes.Get(scenario, "name", "Scenario " + (index + 1));
            string? nameText = scenarioName as string;
            if (nameText == null || nameText.Trim().Length == 0)
            {
                throw new ValueError("Scenario " + (index + 1) + " name must be a non-empty string.");
            }

            ReplicationResult result = ReplicationService.RunFromJson(
                PyTypes.Get(scenario, "simulation_type", "single_server"),
                PyTypes.Get(scenario, "customer_count", 100L),
                PyTypes.Get(scenario, "arrival_rate", 0.5),
                PyTypes.Get(scenario, "service_rate", 1.0),
                PyTypes.Get(scenario, "replication_count", 30L),
                PyTypes.Get(scenario, "server_count", 1L),
                PyTypes.Get(scenario, "arrival_distribution", "exponential"),
                PyTypes.Get(scenario, "service_distribution", "exponential"),
                PyTypes.Get(scenario, "seed", 42L),
                PyTypes.Get(scenario, "confidence_level", 0.95));

            results.Add(new ScenarioResult
            {
                ScenarioId = index + 1,
                ScenarioName = nameText,
                SimulationType = result.SimulationType,
                ServerCount = result.ServerCount,
                CustomerCountPerReplication = result.CustomerCountPerReplication,
                ArrivalRate = result.ArrivalRate,
                ServiceRate = result.ServiceRate,
                ArrivalDistribution = result.ArrivalDistribution,
                ServiceDistribution = result.ServiceDistribution,
                ReplicationCount = result.Summary.ReplicationCount,
                Metrics = result.Summary.Metrics,
            });
        }

        return new ComparisonResult { Scenarios = results };
    }
}
