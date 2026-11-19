using System.Globalization;
using QueueLab.Core.Common;
using QueueLab.Core.Models;

namespace QueueLab.Core.DataProcessing;

/// <summary>Reads customers from an uploaded CSV, XLSX or XLS file (columns arrival_time, service_time, optional customer_id).</summary>
public static class SimulationUploadLoader
{
    public const int MaxCustomers = 100_000;

    private static readonly string[] RequiredColumns = { "arrival_time", "service_time" };

    /// <summary>Equivalent of Python <c>Path(name).suffix.lower()</c>.</summary>
    private static string Suffix(string fileName)
    {
        int slash = fileName.LastIndexOf('/');
        string name = slash >= 0 ? fileName.Substring(slash + 1) : fileName;
        int dot = name.LastIndexOf('.');
        if (dot <= 0 || dot == name.Length - 1)
        {
            return string.Empty;
        }

        return name.Substring(dot).ToLowerInvariant();
    }

    public static List<SimulationCustomer> Load(Stream? stream, string? fileName)
    {
        if (stream == null || string.IsNullOrEmpty(fileName))
        {
            throw new ValueError("No file was uploaded.");
        }

        string extension = Suffix(fileName);
        if (extension != ".csv" && extension != ".xlsx" && extension != ".xls")
        {
            throw new ValueError("Unsupported file type. Upload CSV, XLSX, or XLS.");
        }

        RawTable table;
        try
        {
            table = extension == ".csv" ? RawTable.FromCsv(stream) : RawTable.FromExcel(stream);
        }
        catch (Exception)
        {
            throw new ValueError("Could not read the uploaded file. Check its format.");
        }

        if (table.IsEmpty)
        {
            throw new ValueError("The uploaded file contains no customers.");
        }

        if (table.RowCount > MaxCustomers)
        {
            throw new ValueError("The file exceeds the limit of 100,000 customers.");
        }

        // Normalize headers (trim + lower-case) without renaming anything else.
        Dictionary<string, RawColumn> columns = new Dictionary<string, RawColumn>(StringComparer.Ordinal);
        foreach (RawColumn column in table.Columns)
        {
            string normalized = column.Name.Trim().ToLowerInvariant();
            if (columns.ContainsKey(normalized))
            {
                throw new ValueError("The file contains duplicate column names.");
            }

            columns[normalized] = column;
        }

        List<string> missing = new List<string>();
        foreach (string required in RequiredColumns)
        {
            if (!columns.ContainsKey(required))
            {
                missing.Add(required);
            }
        }

        if (missing.Count > 0)
        {
            missing.Sort(StringComparer.Ordinal);
            throw new ValueError("Missing required columns: " + string.Join(", ", missing));
        }

        RawColumn arrivalColumn = columns["arrival_time"];
        RawColumn serviceColumn = columns["service_time"];
        RawColumn? idColumn;
        columns.TryGetValue("customer_id", out idColumn);

        List<SimulationCustomer> customers = new List<SimulationCustomer>(table.RowCount);
        HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < table.RowCount; index++)
        {
            int rowNumber = index + 2;
            string numericMessage = "Row " + rowNumber.ToString(CultureInfo.InvariantCulture)
                + ": arrival_time and service_time must be numeric.";

            object? arrivalValue = table.RowValue(arrivalColumn, index);
            object? serviceValue = table.RowValue(serviceColumn, index);

            if (arrivalValue == null || serviceValue == null || arrivalValue is bool || serviceValue is bool)
            {
                throw new ValueError(numericMessage);
            }

            double arrivalTime;
            double serviceTime;
            try
            {
                arrivalTime = PyTypes.ToFloat(arrivalValue);
                serviceTime = PyTypes.ToFloat(serviceValue);
            }
            catch (ValueError)
            {
                throw new ValueError(numericMessage);
            }

            if (!double.IsFinite(arrivalTime) || !double.IsFinite(serviceTime))
            {
                throw new ValueError("Row " + rowNumber.ToString(CultureInfo.InvariantCulture) + ": time values must be finite.");
            }

            if (arrivalTime < 0)
            {
                throw new ValueError("Row " + rowNumber.ToString(CultureInfo.InvariantCulture) + ": arrival_time cannot be negative.");
            }

            if (serviceTime <= 0)
            {
                throw new ValueError("Row " + rowNumber.ToString(CultureInfo.InvariantCulture) + ": service_time must be positive.");
            }

            string customerId;
            string generatedId = "Customer-" + (customers.Count + 1).ToString("D3", CultureInfo.InvariantCulture);

            if (idColumn != null)
            {
                object? rawId = table.RowValue(idColumn, index);
                string? text = rawId == null ? null : RawTable.PyStr(rawId).Trim();
                customerId = string.IsNullOrEmpty(text) ? generatedId : text;
            }
            else
            {
                customerId = generatedId;
            }

            if (!seenIds.Add(customerId))
            {
                throw new ValueError(
                    "Row " + rowNumber.ToString(CultureInfo.InvariantCulture)
                    + ": duplicate customer_id '" + customerId + "'.");
            }

            customers.Add(new SimulationCustomer(customerId, arrivalTime, serviceTime));
        }

        return customers;
    }
}
