using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using QueueLab.Core.Common;
using QueueLab.Core.Models;

namespace QueueLab.Core.Services;

/// <summary>Exports tabular data and simulation results to CSV and Excel (.xlsx).</summary>
public static class ExportService
{
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Validates loosely-typed "rows" input: it must be a list of objects.</summary>
    public static List<Dictionary<string, object?>> ValidateRows(object? rows)
    {
        List<object?>? list = rows as List<object?>;
        if (list == null)
        {
            throw new ValueError("rows must be a list.");
        }

        List<Dictionary<string, object?>> result = new List<Dictionary<string, object?>>(list.Count);
        foreach (object? item in list)
        {
            Dictionary<string, object?>? row = item as Dictionary<string, object?>;
            if (row == null)
            {
                throw new ValueError("Each row must be an object.");
            }

            result.Add(row);
        }

        return result;
    }

    private static List<string> FieldNames(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        List<string> names = new List<string>();
        HashSet<string> seen = new HashSet<string>();
        foreach (Dictionary<string, object?> row in rows)
        {
            foreach (string key in row.Keys)
            {
                if (seen.Add(key))
                {
                    names.Add(key);
                }
            }
        }

        return names;
    }

    // ---------------------------------------------------------------- CSV

    public static string ToCsvFromJson(object? rows)
    {
        return ToCsv(ValidateRows(rows));
    }

    /// <summary>CSV text with the same layout as Python's <c>csv.DictWriter</c> (CRLF line endings).</summary>
    public static string ToCsv(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        if (rows.Count == 0)
        {
            throw new ValueError("Cannot export an empty dataset.");
        }

        List<string> fieldNames = FieldNames(rows);
        StringBuilder sb = new StringBuilder();

        WriteCsvRow(sb, fieldNames);

        foreach (Dictionary<string, object?> row in rows)
        {
            List<string> cells = new List<string>(fieldNames.Count);
            foreach (string field in fieldNames)
            {
                object? value;
                if (!row.TryGetValue(field, out value) || value == null)
                {
                    cells.Add(string.Empty);
                }
                else
                {
                    cells.Add(PyRepr.Str(value));
                }
            }

            WriteCsvRow(sb, cells);
        }

        return sb.ToString();
    }

    private static void WriteCsvRow(StringBuilder sb, IReadOnlyList<string> cells)
    {
        // Python's csv module writes a record consisting of one empty field as "".
        if (cells.Count == 1 && cells[0].Length == 0)
        {
            sb.Append("\"\"\r\n");
            return;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            string cell = cells[i];
            bool needsQuotes = cell.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (needsQuotes)
            {
                sb.Append('"').Append(cell.Replace("\"", "\"\"")).Append('"');
            }
            else
            {
                sb.Append(cell);
            }
        }

        sb.Append("\r\n");
    }

    // -------------------------------------------------------------- Excel

    public static byte[] ToExcelFromJson(object? rows, string sheetName = "Simulation Results")
    {
        return ToExcel(ValidateRows(rows), sheetName);
    }

    /// <summary>An .xlsx workbook with a styled header row, frozen header, autofilter and sized columns.</summary>
    public static byte[] ToExcel(IReadOnlyList<Dictionary<string, object?>> rows, string sheetName = "Simulation Results")
    {
        if (rows.Count == 0)
        {
            throw new ValueError("Cannot export an empty dataset.");
        }

        if (string.IsNullOrWhiteSpace(sheetName))
        {
            throw new ValueError("sheet_name must be a non-empty string.");
        }

        List<string> fieldNames = FieldNames(rows);
        int columnCount = fieldNames.Count;
        int rowCount = rows.Count;

        // Longest text per column, measured like the original (falsy values count as empty).
        int[] maxLength = new int[columnCount];

        using (XLWorkbook workbook = new XLWorkbook())
        {
            string title = sheetName.Length > 31 ? sheetName.Substring(0, 31) : sheetName;
            IXLWorksheet sheet = workbook.Worksheets.Add(title);

            for (int c = 0; c < columnCount; c++)
            {
                sheet.Cell(1, c + 1).Value = fieldNames[c];
                maxLength[c] = fieldNames[c].Length;
            }

            IXLRange header = sheet.Range(1, 1, 1, columnCount);
            header.Style.Font.Bold = true;
            header.Style.Font.FontColor = XLColor.White;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");

            for (int r = 0; r < rowCount; r++)
            {
                Dictionary<string, object?> row = rows[r];
                for (int c = 0; c < columnCount; c++)
                {
                    object? value;
                    row.TryGetValue(fieldNames[c], out value);

                    object? cellValue = value;
                    if (value is System.Collections.IDictionary || (value is System.Collections.IEnumerable && !(value is string)))
                    {
                        cellValue = PyJson.Dumps(value);
                    }

                    WriteCell(sheet.Cell(r + 2, c + 1), cellValue);

                    int length = TextLength(cellValue);
                    if (length > maxLength[c])
                    {
                        maxLength[c] = length;
                    }
                }
            }

            sheet.SheetView.FreezeRows(1);
            sheet.Range(1, 1, rowCount + 1, columnCount).SetAutoFilter();

            for (int c = 0; c < columnCount; c++)
            {
                sheet.Column(c + 1).Width = Math.Min(Math.Max(maxLength[c] + 2, 12), 40);
            }

            using (MemoryStream stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
        }
    }

    private static void WriteCell(IXLCell cell, object? value)
    {
        if (value == null)
        {
            return;
        }

        if (value is bool b)
        {
            cell.Value = b;
        }
        else if (value is string s)
        {
            cell.Value = s;
        }
        else if (value is long || value is int || value is System.Numerics.BigInteger)
        {
            cell.Value = (double)PyTypes.ToDouble(value);
        }
        else if (value is double d)
        {
            if (double.IsFinite(d))
            {
                cell.Value = d;
            }
            else
            {
                cell.Value = PyFloat.Repr(d);
            }
        }
        else
        {
            cell.Value = PyRepr.Str(value);
        }
    }

    private static int TextLength(object? value)
    {
        if (value == null)
        {
            return 0;
        }

        // Python: str(cell.value or "") -- zero, False and "" count as empty.
        if (value is bool b && !b)
        {
            return 0;
        }

        if (value is string s)
        {
            return s.Length;
        }

        if (value is long l && l == 0)
        {
            return 0;
        }

        if (value is int i && i == 0)
        {
            return 0;
        }

        if (value is double d && d == 0.0)
        {
            return 0;
        }

        return PyRepr.Str(value).Length;
    }

    // ------------------------------------------------- simulation results

    private static List<Dictionary<string, object?>> RowsFor(SimulationResult result, string exportType, out string sheetName)
    {
        if (exportType == "customers")
        {
            sheetName = "Customer Results";
            return ResultFormatter.CustomerRows(result);
        }

        if (exportType == "summary")
        {
            sheetName = "Simulation Summary";
            return ResultFormatter.SummaryRows(result);
        }

        throw new ValueError("export_type must be 'customers' or 'summary'.");
    }

    public static string SimulationToCsv(SimulationResult result, string exportType = "customers")
    {
        string sheetName;
        return ToCsv(RowsFor(result, exportType, out sheetName));
    }

    public static byte[] SimulationToExcel(SimulationResult result, string exportType = "customers")
    {
        string sheetName;
        List<Dictionary<string, object?>> rows = RowsFor(result, exportType, out sheetName);
        return ToExcel(rows, sheetName);
    }
}
