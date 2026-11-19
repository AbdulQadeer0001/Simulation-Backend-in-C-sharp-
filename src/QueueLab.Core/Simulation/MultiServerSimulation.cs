using QueueLab.Core.Common;
using QueueLab.Core.Models;
using QueueLab.Core.Statistics;

namespace QueueLab.Core.Simulation;

/// <summary>First-come-first-served simulation with several parallel servers sharing one line.</summary>
public sealed class MultiServerSimulation
{
    /// <summary>Safety limit that did not exist in the Python version.</summary>
    public const int MaxServers = 100_000;

    private readonly IReadOnlyList<ISimCustomer> _customers;
    private readonly int _serverCount;

    public MultiServerSimulation(IReadOnlyList<ISimCustomer> customers, int serverCount)
    {
        if (serverCount < 1)
        {
            throw new ValueError("Server count must be at least 1.");
        }

        if (serverCount > MaxServers)
        {
            throw new ValueError("Server count must not exceed " + MaxServers.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + ".");
        }

        _customers = customers;
        _serverCount = serverCount;
    }

    public SimulationResult Run()
    {
        if (_customers.Count == 0)
        {
            throw new ValueError("No customers available for simulation.");
        }

        List<ISimCustomer> customers = _customers
            .OrderBy(c => c.ArrivalMin.HasValue ? 0 : 1)
            .ThenBy(c => c.ArrivalMin ?? 0.0)
            .ToList();

        // Availability of each server.
        double[] serverAvailableTimes = new double[_serverCount];
        double[] serverIdleTimes = new double[_serverCount];

        List<CustomerSimulationResult> customerResults = new List<CustomerSimulationResult>();

        foreach (ISimCustomer customer in customers)
        {
            if (!customer.ArrivalMin.HasValue)
            {
                continue;
            }

            double arrivalTime = customer.ArrivalMin.Value;

            double? serviceTimeOrNull = customer.ServiceTimeMin;
            if (!serviceTimeOrNull.HasValue)
            {
                continue;
            }

            double serviceTime = serviceTimeOrNull.Value;

            if (serviceTime < 0)
            {
                throw new ValueError("Negative service time for customer " + customer.CustomerId + ".");
            }

            // Select the server that becomes available first (lowest index wins ties).
            int serverIndex = 0;
            for (int i = 1; i < _serverCount; i++)
            {
                if (serverAvailableTimes[i] < serverAvailableTimes[serverIndex])
                {
                    serverIndex = i;
                }
            }

            double serverAvailableTime = serverAvailableTimes[serverIndex];

            double idleTime;
            double serviceStart;

            if (arrivalTime > serverAvailableTime)
            {
                idleTime = arrivalTime - serverAvailableTime;
                serviceStart = arrivalTime;
                serverIdleTimes[serverIndex] += idleTime;
            }
            else
            {
                idleTime = 0.0;
                serviceStart = serverAvailableTime;
            }

            double waitingTime = serviceStart - arrivalTime;
            double serviceEnd = serviceStart + serviceTime;
            double systemTime = serviceEnd - arrivalTime;

            serverAvailableTimes[serverIndex] = serviceEnd;

            customerResults.Add(new CustomerSimulationResult
            {
                CustomerId = customer.CustomerId,
                ArrivalTime = PyMath.Round(arrivalTime, 3),
                ServiceTime = PyMath.Round(serviceTime, 3),
                ServiceStart = PyMath.Round(serviceStart, 3),
                ServiceEnd = PyMath.Round(serviceEnd, 3),
                WaitingTime = PyMath.Round(waitingTime, 3),
                SystemTime = PyMath.Round(systemTime, 3),
                ServerId = serverIndex + 1,
                ServerIdleTime = PyMath.Round(idleTime, 3),
            });
        }

        return SimulationSummary.Build("multi_server", _serverCount, customerResults, PyMath.Sum(serverIdleTimes));
    }
}
