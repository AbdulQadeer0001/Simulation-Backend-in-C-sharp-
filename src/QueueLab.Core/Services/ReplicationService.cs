using System.Numerics;
using QueueLab.Core.Common;
using QueueLab.Core.Models;
using QueueLab.Core.Simulation;
using QueueLab.Core.Statistics;

namespace QueueLab.Core.Services;

/// <summary>Metrics of one replication.</summary>
public sealed class ReplicationRun
{
    public int Replication { get; init; }

    public int CustomersProcessed { get; init; }

    public double? AverageWaitingTime { get; init; }

    public double? AverageSystemTime { get; init; }

    public double? AverageNumberInQueue { get; init; }

    public double? AverageNumberInSystem { get; init; }

    public double? Utilization { get; init; }

    public double? Throughput { get; init; }

    public Dictionary<string, object?> ToDict()
    {
        Dictionary<string, object?> d = new Dictionary<string, object?>();
        d["replication"] = Replication;
        d["customers_processed"] = CustomersProcessed;
        d["average_waiting_time"] = AverageWaitingTime;
        d["average_system_time"] = AverageSystemTime;
        d["average_number_in_queue"] = AverageNumberInQueue;
        d["average_number_in_system"] = AverageNumberInSystem;
        d["utilization"] = Utilization;
        d["throughput"] = Throughput;
        return d;
    }
}

/// <summary>Result of running many independent replications of one configuration.</summary>
public sealed class ReplicationResult
{
    public string SimulationType { get; init; } = string.Empty;

    public int ServerCount { get; init; }

    public int CustomerCountPerReplication { get; init; }

    public object? ArrivalRate { get; init; }

    public object? ServiceRate { get; init; }

    public object? ArrivalDistribution { get; init; }

    public object? ServiceDistribution { get; init; }

    public object? BaseSeed { get; init; }

    public ReplicationSummary Summary { get; init; } = new ReplicationSummary();

    public List<ReplicationRun> Replications { get; init; } = new List<ReplicationRun>();

    public Dictionary<string, object?> ToDict()
    {
        List<object?> runs = new List<object?>();
        foreach (ReplicationRun run in Replications)
        {
            runs.Add(run.ToDict());
        }

        Dictionary<string, object?> d = new Dictionary<string, object?>();
        d["simulation_type"] = SimulationType;
        d["server_count"] = ServerCount;
        d["customer_count_per_replication"] = CustomerCountPerReplication;
        d["arrival_rate"] = ArrivalRate;
        d["service_rate"] = ServiceRate;
        d["arrival_distribution"] = ArrivalDistribution;
        d["service_distribution"] = ServiceDistribution;
        d["base_seed"] = BaseSeed;
        d["summary"] = Summary.ToDict();
        d["replications"] = runs;
        return d;
    }
}

/// <summary>Runs independent stochastic replications of a queueing simulation.</summary>
public static class ReplicationService
{
    public const int MaxReplications = 1000;

    /// <summary>Safety limit that did not exist in the Python version (customers x replications).</summary>
    public const long MaxTotalCustomers = 50_000_000;

    private static bool IsFinitePositiveNumber(object? value)
    {
        return PyTypes.IsNumber(value)
            && double.IsFinite(PyTypes.ToDouble(value!))
            && PyTypes.ToDouble(value!) > 0;
    }

    /// <summary>Typed convenience overload (used by the desktop app).</summary>
    public static ReplicationResult Run(
        string simulationType,
        int customerCount,
        double arrivalRate,
        double serviceRate,
        int replicationCount,
        int serverCount,
        string arrivalDistribution,
        string serviceDistribution,
        long? seed,
        double confidenceLevel)
    {
        return RunFromJson(
            simulationType,
            (long)customerCount,
            arrivalRate,
            serviceRate,
            (long)replicationCount,
            (long)serverCount,
            arrivalDistribution,
            serviceDistribution,
            seed.HasValue ? (object)seed.Value : null,
            confidenceLevel);
    }

    /// <summary>
    /// Runs the replications. Arguments are loosely typed (as parsed from JSON) and validated in the same
    /// order, with the same messages, as the original Python implementation.
    /// </summary>
    public static ReplicationResult RunFromJson(
        object? simulationType,
        object? customerCount,
        object? arrivalRate,
        object? serviceRate,
        object? replicationCount,
        object? serverCount,
        object? arrivalDistribution,
        object? serviceDistribution,
        object? seed,
        object? confidenceLevel)
    {
        // Validate the confidence level before running anything.
        if (!PyTypes.IsNumber(confidenceLevel)
            || !double.IsFinite(PyTypes.ToDouble(confidenceLevel!))
            || !(PyTypes.ToDouble(confidenceLevel!) > 0 && PyTypes.ToDouble(confidenceLevel!) < 1))
        {
            throw new ValueError("confidence_level must be a number between 0 and 1.");
        }

        double confidence = PyTypes.ToDouble(confidenceLevel!);

        string? type = simulationType as string;
        if (type != "single_server" && type != "multi_server")
        {
            throw new ValueError("simulation_type must be 'single_server' or 'multi_server'.");
        }

        if (!PyTypes.IsInt(customerCount) || PyTypes.ToBigInteger(customerCount!) < 1)
        {
            throw new ValueError("customer_count must be a positive integer.");
        }

        if (!PyTypes.IsInt(replicationCount)
            || PyTypes.ToBigInteger(replicationCount!) < 2
            || PyTypes.ToBigInteger(replicationCount!) > MaxReplications)
        {
            throw new ValueError("replication_count must be an integer between 2 and 1000.");
        }

        if (!IsFinitePositiveNumber(arrivalRate))
        {
            throw new ValueError("arrival_rate must be a finite positive number.");
        }

        if (!IsFinitePositiveNumber(serviceRate))
        {
            throw new ValueError("service_rate must be a finite positive number.");
        }

        int servers;
        if (type == "single_server")
        {
            servers = 1;
        }
        else
        {
            if (!PyTypes.IsInt(serverCount) || PyTypes.ToBigInteger(serverCount!) < 1)
            {
                throw new ValueError("server_count must be a positive integer.");
            }

            if (PyTypes.ToBigInteger(serverCount!) > MultiServerSimulation.MaxServers)
            {
                throw new ValueError(
                    "Server count must not exceed "
                    + MultiServerSimulation.MaxServers.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                    + ".");
            }

            servers = (int)PyTypes.ToBigInteger(serverCount!);
        }

        if (seed != null && !PyTypes.IsInt(seed))
        {
            throw new ValueError("seed must be an integer or null.");
        }

        BigInteger customers = PyTypes.ToBigInteger(customerCount!);
        int replications = (int)PyTypes.ToBigInteger(replicationCount!);

        if (customers * replications > MaxTotalCustomers)
        {
            throw new ValueError(
                "customer_count x replication_count must not exceed "
                + MaxTotalCustomers.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                + ".");
        }

        BigInteger? baseSeed = seed == null ? (BigInteger?)null : PyTypes.ToBigInteger(seed);
        List<SimulationResult> results = new List<SimulationResult>(replications);

        for (int replicationIndex = 0; replicationIndex < replications; replicationIndex++)
        {
            // Each replication gets its own seed so that it sees different random data, while the same
            // base seed reproduces the whole sequence of replications.
            object? replicationSeed = baseSeed.HasValue
                ? (object)(baseSeed.Value + replicationIndex)
                : null;

            List<DistributionGenerator.GeneratedCustomer> generated = DistributionGenerator.GenerateCustomers(
                customerCount,
                arrivalRate,
                serviceRate,
                arrivalDistribution,
                serviceDistribution,
                replicationSeed);

            List<ISimCustomer> simulationCustomers = new List<ISimCustomer>(generated.Count);
            foreach (DistributionGenerator.GeneratedCustomer item in generated)
            {
                simulationCustomers.Add(item.ToSimulationCustomer());
            }

            SimulationResult result = SimulationService.Run(type!, simulationCustomers, servers);
            results.Add(result);
        }

        ReplicationSummary summary = ReplicationStatistics.SummarizeResults(results, confidence);

        List<ReplicationRun> runs = new List<ReplicationRun>(results.Count);
        for (int index = 0; index < results.Count; index++)
        {
            SimulationResult r = results[index];
            runs.Add(new ReplicationRun
            {
                Replication = index + 1,
                CustomersProcessed = r.CustomersProcessed,
                AverageWaitingTime = r.AverageWaitingTime,
                AverageSystemTime = r.AverageSystemTime,
                AverageNumberInQueue = r.AverageNumberInQueue,
                AverageNumberInSystem = r.AverageNumberInSystem,
                Utilization = r.Utilization,
                Throughput = r.Throughput,
            });
        }

        return new ReplicationResult
        {
            SimulationType = type!,
            ServerCount = servers,
            CustomerCountPerReplication = (int)customers,
            ArrivalRate = arrivalRate,
            ServiceRate = serviceRate,
            ArrivalDistribution = arrivalDistribution,
            ServiceDistribution = serviceDistribution,
            BaseSeed = seed,
            Summary = summary,
            Replications = runs,
        };
    }
}
