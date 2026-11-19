namespace QueueLab.Core.Models;

/// <summary>What happened to one customer during a simulation run.</summary>
public sealed class CustomerSimulationResult
{
    public string CustomerId { get; init; } = string.Empty;

    public double ArrivalTime { get; init; }

    public double ServiceTime { get; init; }

    public double ServiceStart { get; init; }

    public double ServiceEnd { get; init; }

    public double WaitingTime { get; init; }

    public double SystemTime { get; init; }

    public int ServerId { get; init; }

    public double ServerIdleTime { get; init; }

    /// <summary>JSON object for this customer, in the field order used by the original API.</summary>
    public Dictionary<string, object?> ToDict(bool includeServerId = true)
    {
        Dictionary<string, object?> d = new Dictionary<string, object?>();
        d["customer_id"] = CustomerId;
        d["arrival_time"] = ArrivalTime;
        d["service_time"] = ServiceTime;
        d["service_start"] = ServiceStart;
        d["service_end"] = ServiceEnd;
        d["waiting_time"] = WaitingTime;
        d["system_time"] = SystemTime;
        if (includeServerId)
        {
            d["server_id"] = ServerId;
        }

        d["server_idle_time"] = ServerIdleTime;
        return d;
    }
}

/// <summary>Summary statistics and per-customer detail of one simulation run.</summary>
public sealed class SimulationResult
{
    /// <summary>"single_server" or "multi_server".</summary>
    public string SimulationType { get; init; } = string.Empty;

    public int ServerCount { get; init; }

    public int CustomersProcessed { get; init; }

    public double? ArrivalRate { get; init; }

    public double? ServiceRate { get; init; }

    public double? Utilization { get; init; }

    public double? AverageWaitingTime { get; init; }

    public double? AverageSystemTime { get; init; }

    public double? AverageNumberInQueue { get; init; }

    public double? AverageNumberInSystem { get; init; }

    public double? Throughput { get; init; }

    public double TotalIdleTime { get; init; }

    /// <summary>Busy-time share per server, keyed "1", "2", ...</summary>
    public Dictionary<string, double> ServerUtilization { get; init; } = new Dictionary<string, double>();

    public List<CustomerSimulationResult> CustomerResults { get; init; } = new List<CustomerSimulationResult>();

    /// <summary>
    /// The "simulation" JSON object. With <paramref name="legacySingleServerNadra"/> the shape of the
    /// original <c>/simulation/single-server/nadra</c> endpoint is produced ("servers", no server_utilization).
    /// </summary>
    public Dictionary<string, object?> ToSimulationDict(bool legacySingleServerNadra = false)
    {
        Dictionary<string, object?> d = new Dictionary<string, object?>();
        d["type"] = SimulationType;
        d[legacySingleServerNadra ? "servers" : "server_count"] = ServerCount;
        d["customers_processed"] = CustomersProcessed;
        d["arrival_rate"] = ArrivalRate;
        d["service_rate"] = ServiceRate;
        d["utilization"] = Utilization;
        d["average_waiting_time"] = AverageWaitingTime;
        d["average_system_time"] = AverageSystemTime;
        d["average_number_in_queue"] = AverageNumberInQueue;
        d["average_number_in_system"] = AverageNumberInSystem;
        d["throughput"] = Throughput;
        d["total_idle_time"] = TotalIdleTime;
        if (!legacySingleServerNadra)
        {
            d["server_utilization"] = ServerUtilization;
        }

        return d;
    }

    public List<Dictionary<string, object?>> CustomersToList(bool includeServerId = true)
    {
        List<Dictionary<string, object?>> list = new List<Dictionary<string, object?>>(CustomerResults.Count);
        foreach (CustomerSimulationResult customer in CustomerResults)
        {
            list.Add(customer.ToDict(includeServerId));
        }

        return list;
    }

    /// <summary>The standard response body used by most simulation endpoints.</summary>
    public Dictionary<string, object?> ToResponseBody()
    {
        Dictionary<string, object?> d = new Dictionary<string, object?>();
        d["status"] = "success";
        d["simulation"] = ToSimulationDict();
        d["customers"] = CustomersToList();
        return d;
    }
}
