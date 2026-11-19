using QueueLab.Core.Common;
using QueueLab.Core.Models;
using QueueLab.Core.Statistics;

namespace QueueLab.Core.Simulation;

/// <summary>Builds the final <see cref="SimulationResult"/> (shared by the single- and multi-server engines).</summary>
internal static class SimulationSummary
{
    public static SimulationResult Build(
        string simulationType,
        int serverCount,
        List<CustomerSimulationResult> customerResults,
        double totalIdleTime)
    {
        if (customerResults.Count == 0)
        {
            throw new ValueError("No valid customers were available for simulation.");
        }

        // Time-weighted statistics from the event times.
        TimeBasedStatistics timeStats = TimeBasedStatistics.Calculate(customerResults, serverCount);

        int totalCustomers = customerResults.Count;

        double totalServiceTime = PyMath.Sum(customerResults.Select(r => r.ServiceTime));
        double totalWaitingTime = PyMath.Sum(customerResults.Select(r => r.WaitingTime));
        double totalSystemTime = PyMath.Sum(customerResults.Select(r => r.SystemTime));

        double firstArrival = customerResults.Min(r => r.ArrivalTime);
        double lastDeparture = customerResults.Max(r => r.ServiceEnd);
        double simulationTime = lastDeparture - firstArrival;

        if (simulationTime <= 0)
        {
            throw new ValueError("Simulation time must be greater than zero.");
        }

        double arrivalRate = totalCustomers / simulationTime;
        double? serviceRate = totalServiceTime > 0 ? totalCustomers / totalServiceTime : (double?)null;
        double averageWaitingTime = totalWaitingTime / totalCustomers;
        double averageSystemTime = totalSystemTime / totalCustomers;
        double throughput = totalCustomers / simulationTime;

        return new SimulationResult
        {
            SimulationType = simulationType,
            ServerCount = serverCount,
            CustomersProcessed = totalCustomers,
            ArrivalRate = PyMath.Round(arrivalRate, 6),
            ServiceRate = PyMath.Round(serviceRate, 6),
            Utilization = timeStats.Utilization,
            AverageWaitingTime = PyMath.Round(averageWaitingTime, 6),
            AverageSystemTime = PyMath.Round(averageSystemTime, 6),
            AverageNumberInQueue = timeStats.AverageNumberInQueue,
            AverageNumberInSystem = timeStats.AverageNumberInSystem,
            Throughput = PyMath.Round(throughput, 6),
            TotalIdleTime = PyMath.Round(totalIdleTime, 6),
            ServerUtilization = timeStats.ServerUtilization,
            CustomerResults = customerResults,
        };
    }
}
