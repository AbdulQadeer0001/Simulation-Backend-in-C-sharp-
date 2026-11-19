using System.Globalization;
using System.Numerics;
using QueueLab.Core.Common;
using QueueLab.Core.DataProcessing;
using QueueLab.Core.Models;
using QueueLab.Core.Services;
using QueueLab.Core.Simulation;
using QueueLab.Core.Statistics;

namespace QueueLab.Api;

/// <summary>The /api/simulation/* and /api/statistics/* endpoints.</summary>
internal static class SimulationEndpoints
{
    private const string SimulationTypeMessage = "simulation_type must be 'single_server' or 'multi_server'.";

    private static async Task<Dictionary<string, object?>?> ReadObject(HttpRequest request)
    {
        return await RequestParsing.ReadJsonBody(request) as Dictionary<string, object?>;
    }

    private static List<ISimCustomer> AsCustomers(IEnumerable<SimulationCustomer> items)
    {
        List<ISimCustomer> list = new List<ISimCustomer>();
        foreach (SimulationCustomer item in items)
        {
            list.Add(item);
        }

        return list;
    }

    private static List<ISimCustomer> AsCustomers(IEnumerable<DistributionGenerator.GeneratedCustomer> items)
    {
        List<ISimCustomer> list = new List<ISimCustomer>();
        foreach (DistributionGenerator.GeneratedCustomer item in items)
        {
            list.Add(item.ToSimulationCustomer());
        }

        return list;
    }

    // ----------------------------------------------------------------- run

    /// <summary>POST /api/simulation/run</summary>
    public static async Task<IResult> Run(HttpRequest request)
    {
        Dictionary<string, object?>? data = await ReadObject(request);
        if (data == null)
        {
            return Errors.StatusMessage(400, "Provide a valid JSON request body.");
        }

        try
        {
            object? simulationType = PyTypes.Get(data, "simulation_type");
            object? customersData = PyTypes.Get(data, "customers");

            if (!RequestParsing.IsSimulationType(simulationType))
            {
                return Errors.StatusMessage(400, SimulationTypeMessage);
            }

            List<object?>? customerList = customersData as List<object?>;
            if (customerList == null || customerList.Count == 0)
            {
                return Errors.StatusMessage(400, "customers must be a non-empty list.");
            }

            string type = (string)simulationType!;
            int serverCount = 1;

            if (type == "multi_server")
            {
                object? rawCount = PyTypes.Get(data, "server_count");
                if (rawCount is bool || rawCount == null)
                {
                    throw new ValueError("server_count must be a positive integer.");
                }

                serverCount = RequestParsing.ToServerCountFromNumber(PyTypes.ToFloat(rawCount));
            }

            List<SimulationCustomer> customers = new List<SimulationCustomer>();
            HashSet<string> customerIds = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 1; index <= customerList.Count; index++)
            {
                Dictionary<string, object?>? item = customerList[index - 1] as Dictionary<string, object?>;
                if (item == null)
                {
                    throw new ValueError("Customer at index " + (index - 1).ToString(CultureInfo.InvariantCulture) + " must be an object.");
                }

                object? rawId = PyTypes.Get(item, "customer_id", "Customer-" + index.ToString("D3", CultureInfo.InvariantCulture));
                string? idText = rawId as string;
                if (idText == null || idText.Trim().Length == 0)
                {
                    throw new ValueError(
                        "Customer at index " + (index - 1).ToString(CultureInfo.InvariantCulture)
                        + " must have a valid customer_id.");
                }

                string customerId = idText.Trim();
                if (!customerIds.Add(customerId))
                {
                    throw new ValueError("Duplicate customer_id: " + customerId + ".");
                }

                object? rawArrival = PyTypes.Get(item, "arrival_time");
                object? rawService = PyTypes.Get(item, "service_time");

                if (rawArrival == null || rawService == null)
                {
                    throw new ValueError("Customer " + customerId + " must include arrival_time and service_time.");
                }

                if (rawArrival is bool || rawService is bool)
                {
                    throw new ValueError("Customer " + customerId + " has invalid time values.");
                }

                double arrivalTime = PyTypes.ToFloat(rawArrival);
                double serviceTime = PyTypes.ToFloat(rawService);

                if (!double.IsFinite(arrivalTime) || !double.IsFinite(serviceTime))
                {
                    throw new ValueError("Customer " + customerId + " has non-finite time values.");
                }

                if (arrivalTime < 0)
                {
                    throw new ValueError("Arrival time for " + customerId + " cannot be negative.");
                }

                if (serviceTime <= 0)
                {
                    throw new ValueError("Service time for " + customerId + " must be greater than zero.");
                }

                customers.Add(new SimulationCustomer(customerId, arrivalTime, serviceTime));
            }

            SimulationResult result = SimulationService.Run(type, AsCustomers(customers), serverCount);
            return new JsonBodyResult(result.ToResponseBody());
        }
        catch (ValueError error)
        {
            return Errors.StatusMessage(400, error.Message);
        }
    }

    // ------------------------------------------------------ generate-and-run

    private static void RequireRate(object? value, string name)
    {
        if (value is bool || value == null)
        {
            throw new ValueError(name + " must be a positive number.");
        }

        if (!PyTypes.IsNumber(value))
        {
            throw new ValueError(name + " must be a positive number.");
        }

        double number = PyTypes.ToDouble(value);
        if (!double.IsFinite(number) || number <= 0)
        {
            throw new ValueError(name + " must be a finite positive number.");
        }
    }

    /// <summary>POST /api/simulation/generate-and-run</summary>
    public static async Task<IResult> GenerateAndRun(HttpRequest request)
    {
        Dictionary<string, object?>? data = await ReadObject(request);
        if (data == null)
        {
            return Errors.StatusMessage(400, "Provide a valid JSON request body.");
        }

        try
        {
            object? simulationType = PyTypes.Get(data, "simulation_type", "single_server");
            object? customerCount = PyTypes.Get(data, "customer_count");
            object? arrivalRate = PyTypes.Get(data, "arrival_rate");
            object? serviceRate = PyTypes.Get(data, "service_rate");
            object? arrivalDistribution = PyTypes.Get(data, "arrival_distribution", "exponential");
            object? serviceDistribution = PyTypes.Get(data, "service_distribution", "exponential");
            object? seed = PyTypes.Get(data, "seed");
            object? rawServerCount = PyTypes.Get(data, "server_count", 1L);

            if (!RequestParsing.IsSimulationType(simulationType))
            {
                throw new ValueError(SimulationTypeMessage);
            }

            if (!PyTypes.IsInt(customerCount) || PyTypes.ToBigInteger(customerCount!) < 1)
            {
                throw new ValueError("customer_count must be a positive integer.");
            }

            RequireRate(arrivalRate, "arrival_rate");
            RequireRate(serviceRate, "service_rate");

            string type = (string)simulationType!;
            int serverCount;
            if (type == "single_server")
            {
                serverCount = 1;
            }
            else
            {
                if (!PyTypes.IsInt(rawServerCount) || PyTypes.ToBigInteger(rawServerCount!) < 1)
                {
                    throw new ValueError("server_count must be a positive integer.");
                }

                serverCount = RequestParsing.ToServerCount(rawServerCount!);
            }

            if (seed != null && !PyTypes.IsInt(seed))
            {
                throw new ValueError("seed must be an integer or null.");
            }

            List<DistributionGenerator.GeneratedCustomer> generated = DistributionGenerator.GenerateCustomers(
                customerCount,
                arrivalRate,
                serviceRate,
                arrivalDistribution,
                serviceDistribution,
                seed);

            SimulationResult result = SimulationService.Run(type, AsCustomers(generated), serverCount);

            Dictionary<string, object?> configuration = new Dictionary<string, object?>();
            configuration["simulation_type"] = type;
            configuration["customer_count"] = customerCount;
            configuration["arrival_rate"] = arrivalRate;
            configuration["service_rate"] = serviceRate;
            configuration["arrival_distribution"] = arrivalDistribution;
            configuration["service_distribution"] = serviceDistribution;
            configuration["server_count"] = serverCount;
            configuration["seed"] = seed;
            configuration["time_unit"] = "minutes";

            Dictionary<string, object?> body = new Dictionary<string, object?>();
            body["status"] = "success";
            body["configuration"] = configuration;
            body["simulation"] = result.ToSimulationDict();
            body["customers"] = result.CustomersToList();
            return new JsonBodyResult(body);
        }
        catch (ValueError error)
        {
            return Errors.StatusMessage(400, error.Message);
        }
    }

    // ---------------------------------------------------- upload and describe

    private static async Task<MemoryStream> CopyUpload(IFormFile file)
    {
        MemoryStream buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        buffer.Position = 0;
        return buffer;
    }

    private static async Task<IFormFile?> ReadUploadedFile(HttpRequest request)
    {
        if (!request.HasFormContentType)
        {
            return null;
        }

        try
        {
            IFormCollection form = await request.ReadFormAsync(request.HttpContext.RequestAborted);
            return form.Files.GetFile("file");
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string FormValue(IFormCollection? form, string key, string defaultValue)
    {
        if (form == null || !form.ContainsKey(key))
        {
            return defaultValue;
        }

        string? first = form[key].FirstOrDefault();
        return first ?? defaultValue;
    }

    /// <summary>POST /api/simulation/upload-and-run</summary>
    public static async Task<IResult> UploadAndRun(HttpRequest request)
    {
        IFormFile? uploadedFile = await ReadUploadedFile(request);
        if (uploadedFile == null)
        {
            return Errors.StatusMessage(400, "Upload a file using the 'file' field.");
        }

        // The form was read successfully by ReadUploadedFile, so it is cached on the request.
        IFormCollection form = await request.ReadFormAsync();

        string simulationType = FormValue(form, "simulation_type", "single_server").Trim().ToLowerInvariant();
        if (simulationType != "single_server" && simulationType != "multi_server")
        {
            return Errors.StatusMessage(400, SimulationTypeMessage);
        }

        try
        {
            int serverCount = 1;
            if (simulationType == "multi_server")
            {
                string? rawCount = form.ContainsKey("server_count") ? form["server_count"].FirstOrDefault() : null;
                if (rawCount == null || rawCount.Trim().Length == 0)
                {
                    throw new ValueError("server_count is required for multi-server simulation.");
                }

                double numericCount;
                if (!PyFloat.TryParse(rawCount, out numericCount))
                {
                    throw new ValueError("server_count must be a positive integer.");
                }

                serverCount = RequestParsing.ToServerCountFromNumber(numericCount);
            }

            List<SimulationCustomer> customers;
            using (MemoryStream stream = await CopyUpload(uploadedFile))
            {
                customers = SimulationUploadLoader.Load(stream, uploadedFile.FileName);
            }

            SimulationResult result = SimulationService.Run(simulationType, AsCustomers(customers), serverCount);

            Dictionary<string, object?> body = new Dictionary<string, object?>();
            body["status"] = "success";
            body["source"] = "uploaded_file";
            body["filename"] = uploadedFile.FileName;
            body["time_unit"] = "minutes";
            body["simulation"] = result.ToSimulationDict();
            body["customers"] = result.CustomersToList();
            return new JsonBodyResult(body);
        }
        catch (ValueError error)
        {
            return Errors.StatusMessage(400, error.Message);
        }
    }

    /// <summary>POST /api/statistics/describe-upload</summary>
    public static async Task<IResult> DescribeUpload(HttpRequest request)
    {
        IFormFile? uploadedFile = await ReadUploadedFile(request);

        try
        {
            List<SimulationCustomer> customers;
            if (uploadedFile == null)
            {
                customers = SimulationUploadLoader.Load(null, null);
            }
            else
            {
                using (MemoryStream stream = await CopyUpload(uploadedFile))
                {
                    customers = SimulationUploadLoader.Load(stream, uploadedFile.FileName);
                }
            }

            InputDataStatistics.Summary summary = InputDataStatistics.SummarizeCustomers(customers);

            Dictionary<string, object?> body = new Dictionary<string, object?>();
            body["status"] = "success";
            body["source"] = "uploaded_file";
            body["filename"] = uploadedFile!.FileName;
            body["statistics"] = summary.ToDict();
            return new JsonBodyResult(body);
        }
        catch (ValueError error)
        {
            return Errors.StatusMessage(400, error.Message);
        }
    }

    // ------------------------------------------------- replicate and compare

    /// <summary>POST /api/simulation/replicate</summary>
    public static async Task<IResult> Replicate(HttpRequest request)
    {
        Dictionary<string, object?>? data = await ReadObject(request);
        if (data == null)
        {
            return Errors.ErrorOnly(400, "Request body must be a JSON object.");
        }

        try
        {
            ReplicationResult result = ReplicationService.RunFromJson(
                PyTypes.Get(data, "simulation_type", "single_server"),
                PyTypes.Get(data, "customer_count", 100L),
                PyTypes.Get(data, "arrival_rate", 0.5),
                PyTypes.Get(data, "service_rate", 1.0),
                PyTypes.Get(data, "replication_count", 30L),
                PyTypes.Get(data, "server_count", 1L),
                PyTypes.Get(data, "arrival_distribution", "exponential"),
                PyTypes.Get(data, "service_distribution", "exponential"),
                PyTypes.Get(data, "seed"),
                PyTypes.Get(data, "confidence_level", 0.95));

            return new JsonBodyResult(result.ToDict());
        }
        catch (ValueError error)
        {
            return Errors.ErrorOnly(400, error.Message);
        }
    }

    /// <summary>POST /api/simulation/compare</summary>
    public static async Task<IResult> Compare(HttpRequest request)
    {
        Dictionary<string, object?>? data = await ReadObject(request);
        if (data == null)
        {
            return Errors.ErrorOnly(400, "Request body must be a JSON object.");
        }

        try
        {
            ComparisonResult result = ComparisonService.CompareScenarios(PyTypes.Get(data, "scenarios"));
            return new JsonBodyResult(result.ToDict());
        }
        catch (ValueError error)
        {
            return Errors.ErrorOnly(400, error.Message);
        }
    }

    // ----------------------------------------------------------------- export

    /// <summary>POST /api/simulation/export/csv</summary>
    public static async Task<IResult> ExportCsv(HttpRequest request)
    {
        Dictionary<string, object?>? data = await ReadObject(request);
        if (data == null)
        {
            return Errors.ErrorOnly(400, "Request body must be a JSON object.");
        }

        try
        {
            string csv = ExportService.ToCsvFromJson(PyTypes.Get(data, "rows"));
            return FileBodyResult.FromText(csv, "text/csv; charset=utf-8", "queuelab_results.csv");
        }
        catch (ValueError error)
        {
            return Errors.ErrorOnly(400, error.Message);
        }
    }

    /// <summary>POST /api/simulation/export/excel</summary>
    public static async Task<IResult> ExportExcel(HttpRequest request)
    {
        Dictionary<string, object?>? data = await ReadObject(request);
        if (data == null)
        {
            return Errors.ErrorOnly(400, "Request body must be a JSON object.");
        }

        try
        {
            byte[] excel = ExportService.ToExcelFromJson(PyTypes.Get(data, "rows"));
            return new FileBodyResult(excel, ExportService.ExcelContentType, "queuelab_results.xlsx");
        }
        catch (ValueError error)
        {
            return Errors.ErrorOnly(400, error.Message);
        }
    }

    private static void RequirePositiveFiniteRate(object? value, string name)
    {
        if (value is bool || !PyTypes.IsNumber(value))
        {
            throw new ValueError(name + " must be a positive finite number.");
        }

        double number = PyTypes.ToDouble(value!);
        if (!double.IsFinite(number) || number <= 0)
        {
            throw new ValueError(name + " must be a positive finite number.");
        }
    }

    /// <summary>Validates the export request, runs the simulation and returns the result.</summary>
    private static SimulationResult RunSimulationForExport(Dictionary<string, object?>? data)
    {
        if (data == null)
        {
            throw new ValueError("Request body must be a JSON object.");
        }

        object? simulationType = PyTypes.Get(data, "simulation_type", "single_server");
        if (!RequestParsing.IsSimulationType(simulationType))
        {
            throw new ValueError(SimulationTypeMessage);
        }

        object? customerCount = PyTypes.Get(data, "customer_count", 100L);
        object? arrivalRate = PyTypes.Get(data, "arrival_rate", 0.5);
        object? serviceRate = PyTypes.Get(data, "service_rate", 1.0);
        object? arrivalDistribution = PyTypes.Get(data, "arrival_distribution", "exponential");
        object? serviceDistribution = PyTypes.Get(data, "service_distribution", "exponential");
        object? seed = PyTypes.Get(data, "seed");
        object? rawServerCount = PyTypes.Get(data, "server_count", 1L);

        if (!PyTypes.IsInt(customerCount) || PyTypes.ToBigInteger(customerCount!) <= 0)
        {
            throw new ValueError("customer_count must be a positive integer.");
        }

        if (!PyTypes.IsInt(rawServerCount) || PyTypes.ToBigInteger(rawServerCount!) <= 0)
        {
            throw new ValueError("server_count must be a positive integer.");
        }

        RequirePositiveFiniteRate(arrivalRate, "arrival_rate");
        RequirePositiveFiniteRate(serviceRate, "service_rate");

        if (seed != null && !PyTypes.IsInt(seed))
        {
            throw new ValueError("seed must be an integer or null.");
        }

        string? arrivalText = arrivalDistribution as string;
        if (arrivalText != DistributionGenerator.Exponential && arrivalText != DistributionGenerator.Deterministic)
        {
            throw new ValueError("arrival_distribution must be 'exponential' or 'deterministic'.");
        }

        string? serviceText = serviceDistribution as string;
        if (serviceText != DistributionGenerator.Exponential && serviceText != DistributionGenerator.Deterministic)
        {
            throw new ValueError("service_distribution must be 'exponential' or 'deterministic'.");
        }

        List<DistributionGenerator.GeneratedCustomer> generated = DistributionGenerator.GenerateCustomers(
            customerCount,
            arrivalRate,
            serviceRate,
            arrivalDistribution,
            serviceDistribution,
            seed);

        string type = (string)simulationType!;
        int serverCount = type == "single_server" ? 1 : RequestParsing.ToServerCount(rawServerCount!);
        return SimulationService.Run(type, AsCustomers(generated), serverCount);
    }

    private static string ExportTypeOf(Dictionary<string, object?> data)
    {
        // A non-text export_type is invalid; "" never matches 'customers' or 'summary'.
        return PyTypes.Get(data, "export_type", "customers") as string ?? string.Empty;
    }

    /// <summary>POST /api/simulation/run-and-export/csv</summary>
    public static async Task<IResult> RunAndExportCsv(HttpRequest request)
    {
        Dictionary<string, object?>? data = await ReadObject(request);

        try
        {
            SimulationResult result = RunSimulationForExport(data);
            string csv = ExportService.SimulationToCsv(result, ExportTypeOf(data!));
            return FileBodyResult.FromText(csv, "text/csv; charset=utf-8", "queuelab_simulation.csv");
        }
        catch (ValueError error)
        {
            return Errors.StatusMessage(400, error.Message);
        }
    }

    /// <summary>POST /api/simulation/run-and-export/excel</summary>
    public static async Task<IResult> RunAndExportExcel(HttpRequest request)
    {
        Dictionary<string, object?>? data = await ReadObject(request);

        try
        {
            SimulationResult result = RunSimulationForExport(data);
            byte[] excel = ExportService.SimulationToExcel(result, ExportTypeOf(data!));
            return new FileBodyResult(excel, ExportService.ExcelContentType, "queuelab_simulation.xlsx");
        }
        catch (ValueError error)
        {
            return Errors.StatusMessage(400, error.Message);
        }
    }
}
