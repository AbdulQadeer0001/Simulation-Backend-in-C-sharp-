namespace QueueLab.Core.Models;

/// <summary>Anything the simulation engines can process: an id, an arrival time and a service duration.</summary>
public interface ISimCustomer
{
    string CustomerId { get; }

    /// <summary>Arrival time in minutes; <c>null</c> when unknown (such customers are skipped).</summary>
    double? ArrivalMin { get; }

    /// <summary>Service duration in minutes; <c>null</c> when unknown (such customers are skipped).</summary>
    double? ServiceTimeMin { get; }
}

/// <summary>A customer with a known arrival time and service duration (generated, uploaded or typed in).</summary>
public sealed class SimulationCustomer : ISimCustomer
{
    public SimulationCustomer(string customerId, double arrivalMin, double serviceTimeMin)
    {
        CustomerId = customerId;
        ArrivalMin = arrivalMin;
        ServiceTimeMin = serviceTimeMin;
    }

    public string CustomerId { get; }

    public double? ArrivalMin { get; }

    public double? ServiceTimeMin { get; }
}
