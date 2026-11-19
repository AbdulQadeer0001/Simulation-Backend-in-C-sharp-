using QueueLab.Core.Models;
using QueueLab.Core.Simulation;

namespace QueueLab.Core.Services;

/// <summary>Entry points for running the single- and multi-server simulations.</summary>
public static class SimulationService
{
    public static SimulationResult RunSingleServer(IReadOnlyList<ISimCustomer> customers)
    {
        return new SingleServerSimulation(customers).Run();
    }

    public static SimulationResult RunMultiServer(IReadOnlyList<ISimCustomer> customers, int serverCount)
    {
        return new MultiServerSimulation(customers, serverCount).Run();
    }

    /// <summary>Runs either kind of simulation ("single_server" or "multi_server").</summary>
    public static SimulationResult Run(string simulationType, IReadOnlyList<ISimCustomer> customers, int serverCount)
    {
        return simulationType == "single_server"
            ? RunSingleServer(customers)
            : RunMultiServer(customers, serverCount);
    }
}
