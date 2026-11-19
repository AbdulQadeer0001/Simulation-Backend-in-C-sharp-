using QueueLab.Core.Common;
using QueueLab.Core.Models;

namespace QueueLab.Core.Services;

/// <summary>Converts simulation results into flat rows ready for CSV / Excel export.</summary>
public static class ResultFormatter
{
    /// <summary>One row per customer, followed by the simulation type and server count.</summary>
    public static List<Dictionary<string, object?>> CustomerRows(SimulationResult result)
    {
        if (result.CustomerResults.Count == 0)
        {
            throw new ValueError("Simulation result contains no customer results.");
        }

        List<Dictionary<string, object?>> rows = new List<Dictionary<string, object?>>(result.CustomerResults.Count);

        foreach (CustomerSimulationResult customer in result.CustomerResults)
        {
            Dictionary<string, object?> row = customer.ToDict(includeServerId: true);
            row["simulation_type"] = result.SimulationType;
            row["server_count"] = result.ServerCount;
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>A single row with the summary metrics (customer-level detail is exported separately).</summary>
    public static List<Dictionary<string, object?>> SummaryRows(SimulationResult result)
    {
        Dictionary<string, object?> row = new Dictionary<string, object?>();
        row["simulation_type"] = result.SimulationType;
        row["server_count"] = result.ServerCount;
        row["customers_processed"] = result.CustomersProcessed;
        row["arrival_rate"] = result.ArrivalRate;
        row["service_rate"] = result.ServiceRate;
        row["utilization"] = result.Utilization;
        row["average_waiting_time"] = result.AverageWaitingTime;
        row["average_system_time"] = result.AverageSystemTime;
        row["average_number_in_queue"] = result.AverageNumberInQueue;
        row["average_number_in_system"] = result.AverageNumberInSystem;
        row["throughput"] = result.Throughput;
        row["total_idle_time"] = result.TotalIdleTime;
        row["server_utilization"] = new Dictionary<string, double>(result.ServerUtilization);

        return new List<Dictionary<string, object?>> { row };
    }
}
