using QueueLab.Core.DataProcessing;
using QueueLab.Core.Models;
using QueueLab.Core.Simulation;
using QueueLab.Core.Statistics;

namespace QueueLab.Api;

/// <summary>Endpoints that work on the observed NADRA dataset.</summary>
internal static class NadraEndpoints
{
    public const string FileName = "nadra_day1_first100.xlsx";

    private static string FilePath(AppSettings settings)
    {
        return Path.Combine(settings.ObservedDataFolder, FileName);
    }

    /// <summary>GET /api/dataset/nadra</summary>
    public static IResult Dataset(AppSettings settings)
    {
        try
        {
            List<Customer> customers = NadraDataLoader.LoadFile(FilePath(settings));
            Dictionary<string, object?> summary = DescriptiveStatistics.Summary(customers);

            List<object?> preview = new List<object?>();
            foreach (Customer customer in customers.Take(10))
            {
                Dictionary<string, object?> item = new Dictionary<string, object?>();
                item["customer_id"] = customer.CustomerId;
                item["gender"] = customer.Gender;
                item["arrival_min"] = customer.ArrivalMin;
                item["admitted_min"] = customer.AdmittedMin;
                item["token_start_min"] = customer.TokenStartMin;
                item["token_end_min"] = customer.TokenEndMin;
                item["service_start_min"] = customer.ServiceStartMin;
                item["service_end_min"] = customer.ServiceEndMin;
                item["reneged"] = customer.Reneged;
                item["outside_wait_min"] = customer.OutsideWaitMin;
                item["main_wait_min"] = customer.MainWaitMin;
                item["total_time_min"] = customer.TotalTimeMin;
                item["service_time_min"] = customer.ServiceTimeMin;
                preview.Add(item);
            }

            Dictionary<string, object?> body = new Dictionary<string, object?>();
            body["status"] = "success";
            body["dataset"] = "NADRA Day 1 First 100";
            body["customer_count"] = customers.Count;
            body["summary"] = summary;
            body["preview"] = preview;
            return new JsonBodyResult(body);
        }
        catch (Exception error)
        {
            return Errors.StatusMessage(500, error.Message);
        }
    }

    /// <summary>GET /api/simulation/single-server/nadra</summary>
    public static IResult SingleServer(AppSettings settings)
    {
        try
        {
            List<Customer> customers = NadraDataLoader.LoadFile(FilePath(settings));
            SimulationResult result = new SingleServerSimulation(customers).Run();

            Dictionary<string, object?> body = new Dictionary<string, object?>();
            body["status"] = "success";
            body["simulation"] = result.ToSimulationDict(legacySingleServerNadra: true);
            body["customers"] = result.CustomersToList(includeServerId: false);
            return new JsonBodyResult(body);
        }
        catch (Exception error)
        {
            return Errors.StatusMessage(500, error.Message);
        }
    }

    /// <summary>GET /api/simulation/multi-server/nadra/{serverCount}</summary>
    public static IResult MultiServer(AppSettings settings, int serverCount)
    {
        try
        {
            List<Customer> customers = NadraDataLoader.LoadFile(FilePath(settings));
            SimulationResult result = new MultiServerSimulation(customers, serverCount).Run();
            return new JsonBodyResult(result.ToResponseBody());
        }
        catch (Exception error)
        {
            return Errors.StatusMessage(500, error.Message);
        }
    }
}
