using QueueLab.Core.Common;
using QueueLab.Core.Models;
using QueueLab.Core.Statistics;

namespace QueueLab.Core.Simulation;

/// <summary>First-come-first-served simulation with one server.</summary>
public sealed class SingleServerSimulation
{
    private readonly IReadOnlyList<ISimCustomer> _customers;

    public SingleServerSimulation(IReadOnlyList<ISimCustomer> customers)
    {
        _customers = customers;
    }

    public SimulationResult Run()
    {
        if (_customers.Count == 0)
        {
            throw new ValueError("No customers available for simulation.");
        }

        // Process customers in arrival order (stable sort; customers without an arrival time go last and are skipped).
        List<ISimCustomer> customers = _customers
            .OrderBy(c => c.ArrivalMin.HasValue ? 0 : 1)
            .ThenBy(c => c.ArrivalMin ?? 0.0)
            .ToList();

        List<CustomerSimulationResult> customerResults = new List<CustomerSimulationResult>();
        double serverAvailableTime = 0.0;
        double totalIdleTime = 0.0;

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

            double idleTime;
            double serviceStart;

            // Determine when service can begin.
            if (arrivalTime > serverAvailableTime)
            {
                idleTime = arrivalTime - serverAvailableTime;
                totalIdleTime += idleTime;
                serviceStart = arrivalTime;
            }
            else
            {
                idleTime = 0.0;
                serviceStart = serverAvailableTime;
            }

            double waitingTime = serviceStart - arrivalTime;
            double serviceEnd = serviceStart + serviceTime;
            double systemTime = serviceEnd - arrivalTime;

            serverAvailableTime = serviceEnd;

            customerResults.Add(new CustomerSimulationResult
            {
                CustomerId = customer.CustomerId,
                ArrivalTime = PyMath.Round(arrivalTime, 3),
                ServiceTime = PyMath.Round(serviceTime, 3),
                ServiceStart = PyMath.Round(serviceStart, 3),
                ServiceEnd = PyMath.Round(serviceEnd, 3),
                WaitingTime = PyMath.Round(waitingTime, 3),
                SystemTime = PyMath.Round(systemTime, 3),
                ServerId = 1,
                ServerIdleTime = PyMath.Round(idleTime, 3),
            });
        }

        return SimulationSummary.Build("single_server", 1, customerResults, totalIdleTime);
    }
}
