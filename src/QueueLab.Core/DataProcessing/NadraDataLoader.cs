using QueueLab.Core.Common;
using QueueLab.Core.Models;

namespace QueueLab.Core.DataProcessing;

/// <summary>Loads the observed NADRA customer data from its Excel workbook.</summary>
public static class NadraDataLoader
{
    public static readonly string[] RequiredColumns = { "Customer ID", "Arrival (min)", "Reneged" };

    public static List<Customer> LoadFile(string filePath)
    {
        using (FileStream stream = File.OpenRead(filePath))
        {
            return Load(stream);
        }
    }

    public static List<Customer> Load(Stream stream)
    {
        RawTable table = RawTable.FromExcel(stream);

        List<string> missing = new List<string>();
        foreach (string column in RequiredColumns)
        {
            if (table.Find(column) == null)
            {
                missing.Add(column);
            }
        }

        if (missing.Count > 0)
        {
            throw new ValueError("Missing required columns: " + PyRepr.Of(missing));
        }

        List<Customer> customers = new List<Customer>(table.RowCount);
        for (int row = 0; row < table.RowCount; row++)
        {
            string? customerId = Text(table, "Customer ID", row);

            customers.Add(new Customer
            {
                Day = Numeric(table, "Day", row),
                SequenceNumber = Numeric(table, "# in Day", row),

                // str(None) is "None": a missing id is kept as that text, like the original.
                CustomerId = customerId ?? "None",
                Gender = Text(table, "Gender", row),

                ArrivalMin = Numeric(table, "Arrival (min)", row),
                AdmittedMin = Numeric(table, "Admitted (min)", row),
                TokenStartMin = Numeric(table, "Queue1 Token Start (min)", row),
                TokenEndMin = Numeric(table, "Queue1 Token End (min)", row),
                MainWaitStartMin = Numeric(table, "Queue2 Main Wait Start (min)", row),
                ServiceStartMin = Numeric(table, "Queue3 Service Start (min)", row),
                ServiceEndMin = Numeric(table, "Queue3 Service End (min)", row),

                Reneged = Truthy(table, "Reneged", row),

                OutsideWaitMin = Numeric(table, "Outside Wait (min)", row),
                MainWaitMin = Numeric(table, "Main Wait (min)", row),
                TotalTimeMin = Numeric(table, "Total Time (min)", row),
            });
        }

        return customers;
    }

    /// <summary><c>pd.to_numeric(errors="coerce")</c>: numbers stay, numeric text is parsed, anything else is missing.</summary>
    private static double? Numeric(RawTable table, string header, int row)
    {
        RawColumn? column = table.Find(header);
        if (column == null)
        {
            return null;
        }

        object? value = column.Values[row];

        if (value is long l)
        {
            return l;
        }

        if (value is double d)
        {
            return double.IsNaN(d) ? (double?)null : d;
        }

        if (value is bool b)
        {
            return b ? 1.0 : 0.0;
        }

        string? text = value as string;
        if (text != null && text.IndexOf('_') < 0)
        {
            double parsed;
            if (PyFloat.TryParse(text, out parsed) && !double.IsNaN(parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    /// <summary><c>.astype("string").str.strip()</c>; missing cells stay missing.</summary>
    private static string? Text(RawTable table, string header, int row)
    {
        RawColumn? column = table.Find(header);
        if (column == null)
        {
            return null;
        }

        object? value = column.Values[row];
        if (value == null)
        {
            return null;
        }

        return RawTable.PyStr(value).Trim();
    }

    /// <summary><c>.fillna(False).astype(bool)</c>: Python truthiness of each cell.</summary>
    private static bool Truthy(RawTable table, string header, int row)
    {
        RawColumn? column = table.Find(header);
        if (column == null)
        {
            return false;
        }

        object? value = column.Values[row];

        if (value == null)
        {
            return false;
        }

        if (value is bool b)
        {
            return b;
        }

        if (value is long l)
        {
            return l != 0;
        }

        if (value is double d)
        {
            return d != 0.0;
        }

        if (value is string s)
        {
            return s.Length > 0;
        }

        return true;
    }
}
