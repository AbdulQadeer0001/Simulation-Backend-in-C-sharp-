using MathNet.Numerics.Distributions;
using QueueLab.Core.Common;
using QueueLab.Core.Models;

namespace QueueLab.Core.Statistics;

/// <summary>Mean, standard deviation and confidence interval of one metric across replications.</summary>
public sealed class MetricSummary
{
    public int ReplicationCount { get; init; }

    public double Mean { get; init; }

    /// <summary>Sample standard deviation; <c>null</c> with a single replication.</summary>
    public double? StandardDeviation { get; init; }

    public double ConfidenceLevel { get; init; }

    public double? Lower { get; init; }

    public double? Upper { get; init; }

    public double? MarginOfError { get; init; }

    public bool HasConfidenceInterval
    {
        get { return Lower.HasValue && Upper.HasValue && MarginOfError.HasValue; }
    }

    public Dictionary<string, object?> ToDict()
    {
        Dictionary<string, object?> d = new Dictionary<string, object?>();
        d["replication_count"] = ReplicationCount;
        d["mean"] = Mean;
        d["standard_deviation"] = StandardDeviation;
        d["confidence_level"] = ConfidenceLevel;

        if (HasConfidenceInterval)
        {
            Dictionary<string, object?> ci = new Dictionary<string, object?>();
            ci["lower"] = Lower;
            ci["upper"] = Upper;
            ci["margin_of_error"] = MarginOfError;
            d["confidence_interval"] = ci;
        }
        else
        {
            d["confidence_interval"] = null;
        }

        return d;
    }
}

/// <summary>Summary of all tracked metrics across the replications of one experiment.</summary>
public sealed class ReplicationSummary
{
    public int ReplicationCount { get; init; }

    public double ConfidenceLevel { get; init; }

    /// <summary>Metric name to summary (<c>null</c> if the metric was unavailable in every run).</summary>
    public Dictionary<string, MetricSummary?> Metrics { get; init; } = new Dictionary<string, MetricSummary?>();

    public Dictionary<string, object?> ToDict()
    {
        Dictionary<string, object?> metrics = new Dictionary<string, object?>();
        foreach (KeyValuePair<string, MetricSummary?> pair in Metrics)
        {
            metrics[pair.Key] = pair.Value == null ? null : pair.Value.ToDict();
        }

        Dictionary<string, object?> d = new Dictionary<string, object?>();
        d["replication_count"] = ReplicationCount;
        d["confidence_level"] = ConfidenceLevel;
        d["metrics"] = metrics;
        return d;
    }
}

public static class ReplicationStatistics
{
    public static readonly string[] Metrics =
    {
        "average_waiting_time",
        "average_system_time",
        "average_number_in_queue",
        "average_number_in_system",
        "utilization",
        "throughput",
    };

    /// <summary>Summarizes a numeric metric across independent replications.</summary>
    public static MetricSummary Summarize(IReadOnlyList<double> values, double confidenceLevel = 0.95)
    {
        if (!(confidenceLevel > 0 && confidenceLevel < 1))
        {
            throw new ValueError("Confidence level must be a number between 0 and 1.");
        }

        if (values.Count == 0)
        {
            throw new ValueError("At least one replication is required.");
        }

        foreach (double value in values)
        {
            if (!double.IsFinite(value))
            {
                throw new ValueError("Replication values must be finite.");
            }
        }

        int count = values.Count;
        double average = PyStats.Mean(values);

        if (count == 1)
        {
            return new MetricSummary
            {
                ReplicationCount = 1,
                Mean = PyMath.Round(average, 6),
                StandardDeviation = null,
                ConfidenceLevel = confidenceLevel,
            };
        }

        double standardDeviation = PyStats.SampleStdDev(values);
        double standardError = standardDeviation / Math.Sqrt(count);

        // Two-sided Student-t critical value (replaces scipy.stats.t.ppf).
        double criticalValue = StudentT.InvCDF(0.0, 1.0, count - 1, (1 + confidenceLevel) / 2);
        double marginOfError = criticalValue * standardError;

        return new MetricSummary
        {
            ReplicationCount = count,
            Mean = PyMath.Round(average, 6),
            StandardDeviation = PyMath.Round(standardDeviation, 6),
            ConfidenceLevel = confidenceLevel,
            Lower = PyMath.Round(average - marginOfError, 6),
            Upper = PyMath.Round(average + marginOfError, 6),
            MarginOfError = PyMath.Round(marginOfError, 6),
        };
    }

    private static double? MetricOf(SimulationResult result, string metric)
    {
        switch (metric)
        {
            case "average_waiting_time":
                return result.AverageWaitingTime;
            case "average_system_time":
                return result.AverageSystemTime;
            case "average_number_in_queue":
                return result.AverageNumberInQueue;
            case "average_number_in_system":
                return result.AverageNumberInSystem;
            case "utilization":
                return result.Utilization;
            case "throughput":
                return result.Throughput;
            default:
                throw new ValueError("Unknown metric '" + metric + "'.");
        }
    }

    /// <summary>Summarizes every supported metric across a list of simulation results.</summary>
    public static ReplicationSummary SummarizeResults(IReadOnlyList<SimulationResult> results, double confidenceLevel = 0.95)
    {
        if (results.Count == 0)
        {
            throw new ValueError("At least one simulation result is required.");
        }

        Dictionary<string, MetricSummary?> summaries = new Dictionary<string, MetricSummary?>();

        foreach (string metric in Metrics)
        {
            List<double?> values = new List<double?>(results.Count);
            foreach (SimulationResult result in results)
            {
                values.Add(MetricOf(result, metric));
            }

            // Some metrics may be unavailable for every run.
            if (values.All(v => !v.HasValue))
            {
                summaries[metric] = null;
                continue;
            }

            if (values.Any(v => !v.HasValue))
            {
                throw new ValueError("Metric '" + metric + "' is missing from some replications.");
            }

            summaries[metric] = Summarize(values.Select(v => v!.Value).ToList(), confidenceLevel);
        }

        return new ReplicationSummary
        {
            ReplicationCount = results.Count,
            ConfidenceLevel = confidenceLevel,
            Metrics = summaries,
        };
    }
}
