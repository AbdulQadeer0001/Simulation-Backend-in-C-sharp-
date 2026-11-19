using QueueLab.Core.Common;
using QueueLab.Core.DataProcessing;
using QueueLab.Core.Models;
using QueueLab.Core.Queueing;
using QueueLab.Core.Services;
using QueueLab.Core.Simulation;
using QueueLab.Core.Statistics;
using Xunit;

namespace QueueLab.Tests;

/// <summary>Direct tests of the engine (ported from the original pytest suite).</summary>
public class CoreBehaviourTests
{
    private static List<ISimCustomer> Customers(params (string id, double arrival, double service)[] items)
    {
        List<ISimCustomer> list = new List<ISimCustomer>();
        foreach ((string id, double arrival, double service) in items)
        {
            list.Add(new SimulationCustomer(id, arrival, service));
        }

        return list;
    }

    // -------------------------------------------------- analytical models

    [Fact]
    public void Mm1ComputesTheClassicFormulas()
    {
        QueueModelResult result = new MM1Queue(4, 5).Calculate();

        Assert.Equal("M/M/1", result.Model);
        Assert.Equal(0.2, result.Metrics["probability_empty"]);
        Assert.Equal(3.2, result.Metrics["average_number_in_queue"]);
        Assert.Equal(4.0, result.Metrics["average_number_in_system"]);
        Assert.Equal(0.8, result.Metrics["average_waiting_time"]);
        Assert.Equal(1.0, result.Metrics["average_system_time"]);
        Assert.Equal(4.0, result.Metrics["throughput"]);
    }

    [Fact]
    public void MmcMatchesTheErlangCValues()
    {
        QueueModelResult result = new MMCQueue(4, 5, 2).Calculate();

        Assert.Equal(0.428571, result.Metrics["probability_empty"]);
        Assert.Equal(0.228571, result.Metrics["probability_wait"]);
        Assert.Equal(0.152381, result.Metrics["average_number_in_queue"]);
        Assert.Equal(0.952381, result.Metrics["average_number_in_system"]);
        Assert.Equal(0.038095, result.Metrics["average_waiting_time"]);
        Assert.Equal(0.238095, result.Metrics["average_system_time"]);
    }

    [Fact]
    public void UnstableModelsAreRejected()
    {
        Assert.Throws<ValueError>(() => new MM1Queue(5, 4));
        Assert.Throws<ValueError>(() => new MMCQueue(11, 5, 2));
        Assert.Throws<ValueError>(() => new MMCQueue(1, 1, 0));
    }

    // ------------------------------------------------------- simulations

    [Fact]
    public void SingleServerQueuesCustomersInArrivalOrder()
    {
        SimulationResult result = SimulationService.RunSingleServer(Customers(("C3", 10, 4), ("C1", 0, 5), ("C2", 2, 3)));

        Assert.Equal(3, result.CustomersProcessed);
        Assert.Equal(new[] { "C1", "C2", "C3" }, result.CustomerResults.Select(c => c.CustomerId).ToArray());

        CustomerSimulationResult second = result.CustomerResults[1];
        Assert.Equal(5.0, second.ServiceStart);
        Assert.Equal(3.0, second.WaitingTime);
        Assert.Equal(8.0, second.ServiceEnd);

        Assert.Equal(1.0, result.AverageWaitingTime);
        Assert.Equal(2.0, result.TotalIdleTime);
    }

    [Fact]
    public void MultiServerGivesTiesToTheLowestServer()
    {
        SimulationResult result = SimulationService.RunMultiServer(
            Customers(("A", 1, 2), ("B", 1, 2), ("C", 1, 2)),
            2);

        Assert.Equal(1, result.CustomerResults[0].ServerId);
        Assert.Equal(2, result.CustomerResults[1].ServerId);
        Assert.Equal(1, result.CustomerResults[2].ServerId);
        Assert.Equal(2.0, result.CustomerResults[2].WaitingTime);
    }

    [Fact]
    public void NegativeServiceTimesAreRejected()
    {
        Assert.Throws<ValueError>(() => SimulationService.RunSingleServer(Customers(("A", 0, -1))));
        Assert.Throws<ValueError>(() => new MultiServerSimulation(Customers(("A", 0, 1)), 0));
    }

    [Fact]
    public void TimeWeightedStatisticsMatchAHandCalculation()
    {
        // One customer: arrives 0, served 0-4. Queue empty; system holds 1 customer for all 4 minutes.
        SimulationResult result = SimulationService.RunSingleServer(Customers(("A", 0, 4)));

        Assert.Equal(0.0, result.AverageNumberInQueue);
        Assert.Equal(1.0, result.AverageNumberInSystem);
        Assert.Equal(1.0, result.Utilization);
    }

    // ---------------------------------------------------- distributions

    [Fact]
    public void SameSeedGivesTheSameCustomers()
    {
        List<DistributionGenerator.GeneratedCustomer> a = DistributionGenerator.GenerateCustomers(25L, 0.5, 1.0, "exponential", "exponential", 42L);
        List<DistributionGenerator.GeneratedCustomer> b = DistributionGenerator.GenerateCustomers(25L, 0.5, 1.0, "exponential", "exponential", 42L);
        List<DistributionGenerator.GeneratedCustomer> c = DistributionGenerator.GenerateCustomers(25L, 0.5, 1.0, "exponential", "exponential", 43L);

        Assert.Equal(a.Select(x => x.ArrivalTime), b.Select(x => x.ArrivalTime));
        Assert.NotEqual(a.Select(x => x.ArrivalTime), c.Select(x => x.ArrivalTime));
    }

    [Fact]
    public void DeterministicCustomersAreEvenlySpaced()
    {
        List<DistributionGenerator.GeneratedCustomer> customers =
            DistributionGenerator.GenerateCustomers(3L, 2.0, 4.0, "deterministic", "deterministic", null);

        Assert.Equal(new[] { 0.0, 0.5, 1.0 }, customers.Select(c => c.ArrivalTime).ToArray());
        Assert.All(customers, c => Assert.Equal(0.25, c.ServiceTime));
        Assert.Equal("C1", customers[0].CustomerId);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-3L)]
    public void GeneratorRejectsNonPositiveCounts(long count)
    {
        Assert.Throws<ValueError>(() => DistributionGenerator.GenerateCustomers(count, 1.0, 1.0, "exponential", "exponential", null));
    }

    [Fact]
    public void GeneratorRejectsBoolsAndFloatsAsIntegers()
    {
        Assert.Throws<ValueError>(() => DistributionGenerator.GenerateCustomers(true, 1.0, 1.0, "exponential", "exponential", null));
        Assert.Throws<ValueError>(() => DistributionGenerator.GenerateCustomers(5L, 1.0, 1.0, "exponential", "exponential", 1.5));
        Assert.Throws<ValueError>(() => DistributionGenerator.GenerateCustomers(5L, 1.0, 1.0, "weibull", "exponential", null));
    }

    // -------------------------------------------------------- replications

    [Fact]
    public void ReplicationSummaryMatchesPythonValues()
    {
        MetricSummary summary = ReplicationStatistics.Summarize(new[] { 4.0, 5.0, 6.0, 4.0, 6.0 }, 0.95);

        Assert.Equal(5, summary.ReplicationCount);
        Assert.Equal(5.0, summary.Mean);
        Assert.Equal(1.0, summary.StandardDeviation);
        Assert.Equal(3.758336, summary.Lower);
        Assert.Equal(6.241664, summary.Upper);
        Assert.Equal(1.241664, summary.MarginOfError);
    }

    [Fact]
    public void ReplicationSummaryHandlesWiderIntervalsAndSingleRuns()
    {
        MetricSummary wide = ReplicationStatistics.Summarize(
            new[] { 1.0, 2, 3, 4, 5, 6, 7, 8, 9, 10.5 }, 0.99);
        Assert.Equal(5.55, wide.Mean);
        Assert.Equal(3.113144, wide.StandardDeviation);
        Assert.Equal(2.350658, wide.Lower);
        Assert.Equal(8.749342, wide.Upper);

        MetricSummary single = ReplicationStatistics.Summarize(new[] { 10.0 }, 0.9);
        Assert.Null(single.StandardDeviation);
        Assert.False(single.HasConfidenceInterval);
    }

    [Fact]
    public void ReplicationsAreReproducibleWithASeed()
    {
        ReplicationResult a = ReplicationService.Run("single_server", 40, 0.5, 1.0, 4, 1, "exponential", "exponential", 7, 0.95);
        ReplicationResult b = ReplicationService.Run("single_server", 40, 0.5, 1.0, 4, 1, "exponential", "exponential", 7, 0.95);

        Assert.Equal(
            a.Replications.Select(r => r.AverageWaitingTime),
            b.Replications.Select(r => r.AverageWaitingTime));
        Assert.Equal(4, a.Replications.Count);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(1001L)]
    public void ReplicationCountIsLimited(long replications)
    {
        Assert.Throws<ValueError>(() => ReplicationService.RunFromJson(
            "single_server", 10L, 0.5, 1.0, replications, 1L, "exponential", "exponential", null, 0.95));
    }

    // ------------------------------------------------------------- export

    [Fact]
    public void CsvQuotesLikePythonsCsvModule()
    {
        List<Dictionary<string, object?>> rows = new List<Dictionary<string, object?>>();
        Dictionary<string, object?> row = new Dictionary<string, object?>();
        row["text"] = "a,b \"q\"";
        row["number"] = 2.5;
        row["gap"] = null;
        row["flag"] = true;
        rows.Add(row);

        Assert.Equal("text,number,gap,flag\r\n\"a,b \"\"q\"\"\",2.5,,True\r\n", ExportService.ToCsv(rows));
    }

    [Fact]
    public void EmptyExportsAreRejected()
    {
        Assert.Throws<ValueError>(() => ExportService.ToCsv(new List<Dictionary<string, object?>>()));
        Assert.Throws<ValueError>(() => ExportService.ToExcel(new List<Dictionary<string, object?>>()));
    }

    // ------------------------------------------------------------- upload

    private static MemoryStream Csv(string text)
    {
        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
    }

    [Fact]
    public void UploadLoaderReadsAValidCsv()
    {
        List<SimulationCustomer> customers = SimulationUploadLoader.Load(
            Csv("Customer_ID, arrival_time ,SERVICE_TIME\nA,0,1.5\nB,2,3\n"),
            "data.CSV");

        Assert.Equal(2, customers.Count);
        Assert.Equal("A", customers[0].CustomerId);
        Assert.Equal(1.5, customers[0].ServiceTimeMin);
        Assert.Equal(2.0, customers[1].ArrivalMin);
    }

    [Theory]
    [InlineData("arrival_time,service_time\n-1,5\n", "Row 2: arrival_time cannot be negative.")]
    [InlineData("arrival_time,service_time\n1,0\n", "Row 2: service_time must be positive.")]
    [InlineData("arrival_time,service_time\n1,abc\n", "Row 2: arrival_time and service_time must be numeric.")]
    [InlineData("arrival_time,service_time\n1,inf\n", "Row 2: time values must be finite.")]
    [InlineData("arrival_time\n1\n", "Missing required columns: service_time")]
    [InlineData("arrival_time,service_time\n", "The uploaded file contains no customers.")]
    public void UploadLoaderReportsProblemsLikeTheOriginal(string csv, string message)
    {
        ValueError error = Assert.Throws<ValueError>(() => SimulationUploadLoader.Load(Csv(csv), "x.csv"));
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void UploadLoaderRejectsUnknownExtensions()
    {
        ValueError error = Assert.Throws<ValueError>(() => SimulationUploadLoader.Load(Csv("x"), "notes.txt"));
        Assert.Equal("Unsupported file type. Upload CSV, XLSX, or XLS.", error.Message);
    }

    [Fact]
    public void NumericIdsFollowPandasRowUpcasting()
    {
        // All-integer sheet: ids stay "1", "2".
        List<SimulationCustomer> ints = SimulationUploadLoader.Load(Csv("customer_id,arrival_time,service_time\n1,0,5\n2,2,3\n"), "a.csv");
        Assert.Equal(new[] { "1", "2" }, ints.Select(c => c.CustomerId).ToArray());

        // A float column makes pandas turn the whole row into floats: ids become "1.0", "2.0" there.
        List<SimulationCustomer> floats = SimulationUploadLoader.Load(Csv("customer_id,arrival_time,service_time\n1,0,5.5\n2,2,3\n"), "a.csv");
        Assert.Equal(new[] { "1.0", "2.0" }, floats.Select(c => c.CustomerId).ToArray());
    }

    [Fact]
    public void NadraWorkbookLoadsAllHundredCustomers()
    {
        string? directory = AppContext.BaseDirectory;
        string? file = null;
        while (directory != null && file == null)
        {
            string candidate = Path.Combine(directory, "src", "QueueLab.Api", "data", "observed", "nadra_day1_first100.xlsx");
            if (File.Exists(candidate))
            {
                file = candidate;
            }

            directory = Path.GetDirectoryName(directory);
        }

        Assert.NotNull(file);

        List<Customer> customers = NadraDataLoader.LoadFile(file!);
        Assert.Equal(100, customers.Count);
        Assert.All(customers, c => Assert.False(c.Reneged));
        Assert.Equal("F1", customers[0].CustomerId);
        Assert.Equal(19.7, customers[0].ServiceTimeMin);
    }
}
