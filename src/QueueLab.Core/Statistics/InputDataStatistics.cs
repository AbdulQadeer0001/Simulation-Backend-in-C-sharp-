using QueueLab.Core.Common;
using QueueLab.Core.Models;

namespace QueueLab.Core.Statistics;

/// <summary>Descriptive statistics for uploaded arrival / service data (the "describe-upload" endpoint).</summary>
public static class InputDataStatistics
{
    /// <summary>Statistics of one numeric series: count, mean, median, std-dev, min, max and quartiles.</summary>
    public sealed class SeriesSummary
    {
        public int Count { get; init; }

        public double? Mean { get; init; }

        public double? Median { get; init; }

        public double? StandardDeviation { get; init; }

        public double? Minimum { get; init; }

        public double? Maximum { get; init; }

        public double? Percentile25 { get; init; }

        public double? Percentile50 { get; init; }

        public double? Percentile75 { get; init; }

        public Dictionary<string, object?> ToDict()
        {
            Dictionary<string, object?> d = new Dictionary<string, object?>();
            d["count"] = Count;
            d["mean"] = Mean;
            d["median"] = Median;
            d["standard_deviation"] = StandardDeviation;
            d["minimum"] = Minimum;
            d["maximum"] = Maximum;
            d["percentile_25"] = Percentile25;
            d["percentile_50"] = Percentile50;
            d["percentile_75"] = Percentile75;
            return d;
        }
    }

    public sealed class Summary
    {
        public int CustomerCount { get; init; }

        public string TimeUnit { get; init; } = "minutes";

        public SeriesSummary ArrivalTimes { get; init; } = new SeriesSummary();

        public SeriesSummary InterarrivalTimes { get; init; } = new SeriesSummary();

        public SeriesSummary ServiceTimes { get; init; } = new SeriesSummary();

        public double? EstimatedArrivalRate { get; init; }

        public double? EstimatedServiceRate { get; init; }

        public double ArrivalObservationDuration { get; init; }

        public Dictionary<string, object?> ToDict()
        {
            Dictionary<string, object?> d = new Dictionary<string, object?>();
            d["customer_count"] = CustomerCount;
            d["time_unit"] = TimeUnit;
            d["arrival_times"] = ArrivalTimes.ToDict();
            d["interarrival_times"] = InterarrivalTimes.ToDict();
            d["service_times"] = ServiceTimes.ToDict();
            d["estimated_arrival_rate"] = EstimatedArrivalRate;
            d["estimated_service_rate"] = EstimatedServiceRate;
            d["arrival_observation_duration"] = ArrivalObservationDuration;
            return d;
        }
    }

    public static SeriesSummary Describe(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return new SeriesSummary { Count = 0 };
        }

        double minimum = values[0];
        double maximum = values[0];
        foreach (double v in values)
        {
            if (v < minimum)
            {
                minimum = v;
            }

            if (v > maximum)
            {
                maximum = v;
            }
        }

        return new SeriesSummary
        {
            Count = values.Count,
            Mean = PyMath.Round(PyStats.Mean(values), 6),
            Median = PyMath.Round(PyStats.Median(values), 6),
            StandardDeviation = PyMath.Round(PyStats.PopulationStdDev(values), 6),
            Minimum = PyMath.Round(minimum, 6),
            Maximum = PyMath.Round(maximum, 6),
            Percentile25 = PyMath.Round(PyStats.Percentile(values, 0.25), 6),
            Percentile50 = PyMath.Round(PyStats.Percentile(values, 0.50), 6),
            Percentile75 = PyMath.Round(PyStats.Percentile(values, 0.75), 6),
        };
    }

    /// <summary>Summarizes arrival and service data. Every customer needs an arrival time and a service time.</summary>
    public static Summary SummarizeCustomers(IReadOnlyList<ISimCustomer> customers)
    {
        if (customers.Count == 0)
        {
            throw new ValueError("At least one customer is required.");
        }

        List<double> arrivalTimes = new List<double>(customers.Count);
        List<double> serviceTimes = new List<double>(customers.Count);

        foreach (ISimCustomer customer in customers)
        {
            arrivalTimes.Add(customer.ArrivalMin ?? double.NaN);
            serviceTimes.Add(customer.ServiceTimeMin ?? double.NaN);
        }

        arrivalTimes.Sort();

        List<double> interarrivalTimes = new List<double>();
        for (int i = 1; i < arrivalTimes.Count; i++)
        {
            interarrivalTimes.Add(arrivalTimes[i] - arrivalTimes[i - 1]);
        }

        double arrivalDuration = arrivalTimes[arrivalTimes.Count - 1] - arrivalTimes[0];

        double? arrivalRate = null;
        if (arrivalDuration > 0)
        {
            arrivalRate = interarrivalTimes.Count / arrivalDuration;
        }

        double averageServiceTime = PyStats.Mean(serviceTimes);
        double? serviceRate = averageServiceTime > 0 ? 1 / averageServiceTime : (double?)null;

        return new Summary
        {
            CustomerCount = customers.Count,
            TimeUnit = "minutes",
            ArrivalTimes = Describe(arrivalTimes),
            InterarrivalTimes = Describe(interarrivalTimes),
            ServiceTimes = Describe(serviceTimes),
            EstimatedArrivalRate = PyMath.Round(arrivalRate, 6),
            EstimatedServiceRate = PyMath.Round(serviceRate, 6),
            ArrivalObservationDuration = PyMath.Round(arrivalDuration, 6),
        };
    }
}
