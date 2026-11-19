using System.Globalization;
using System.Numerics;
using System.Text;
using ExcelDataReader;
using QueueLab.Core.Common;

namespace QueueLab.Core.DataProcessing;

/// <summary>Column types, following pandas' dtype inference.</summary>
public enum ColumnKind
{
    /// <summary>int64: every cell is a whole number and none is missing.</summary>
    Int,

    /// <summary>float64: numbers, possibly with missing cells (NaN).</summary>
    Float,

    /// <summary>bool: every cell is true/false and none is missing.</summary>
    Bool,

    /// <summary>object: text, mixed content, dates, or booleans with gaps.</summary>
    Object,
}

/// <summary>One column of a <see cref="RawTable"/>. Cells are null (missing), long, double, bool, string or other.</summary>
public sealed class RawColumn
{
    public RawColumn(string name, ColumnKind kind, object?[] values)
    {
        Name = name;
        Kind = kind;
        Values = values;
    }

    public string Name { get; }

    public ColumnKind Kind { get; }

    public object?[] Values { get; }
}

/// <summary>
/// A table read from a CSV or Excel file with pandas-compatible typing. It reproduces the behaviour of
/// <c>pd.read_csv</c> / <c>pd.read_excel</c> that is visible in the API: which text counts as missing,
/// how columns are typed, how duplicate headers are renamed, and how a row's values are converted when the
/// row is read with <c>DataFrame.iterrows()</c> (an all-numeric row with a float column turns whole numbers
/// into floats, so an id of 1 reads as "1.0").
/// </summary>
public sealed class RawTable
{
    /// <summary>The strings pandas treats as missing values by default.</summary>
    private static readonly HashSet<string> NaTokens = new HashSet<string>(StringComparer.Ordinal)
    {
        string.Empty, "#N/A", "#N/A N/A", "#NA", "-1.#IND", "-1.#QNAN", "-NaN", "-nan", "1.#IND", "1.#QNAN",
        "<NA>", "N/A", "NA", "NULL", "NaN", "None", "n/a", "nan", "null",
    };

    private static readonly HashSet<string> TrueTokens = new HashSet<string>(StringComparer.Ordinal) { "True", "TRUE", "true" };

    private static readonly HashSet<string> FalseTokens = new HashSet<string>(StringComparer.Ordinal) { "False", "FALSE", "false" };

    private static bool encodingsRegistered;

    public RawTable(List<RawColumn> columns, int rowCount)
    {
        Columns = columns;
        RowCount = rowCount;
    }

    public List<RawColumn> Columns { get; }

    public int RowCount { get; }

    /// <summary>pandas' <c>DataFrame.empty</c>: no rows or no columns.</summary>
    public bool IsEmpty
    {
        get { return RowCount == 0 || Columns.Count == 0; }
    }

    public RawColumn? Find(string name)
    {
        foreach (RawColumn column in Columns)
        {
            if (column.Name == name)
            {
                return column;
            }
        }

        return null;
    }

    // ------------------------------------------------------- row values

    /// <summary>
    /// The common type pandas gives a row of this table: int when every column is int, float when all
    /// columns are numeric and at least one is float, otherwise object.
    /// </summary>
    public ColumnKind RowKind
    {
        get
        {
            if (!_rowKind.HasValue)
            {
                _rowKind = ComputeRowKind();
            }

            return _rowKind.Value;
        }
    }

    private ColumnKind? _rowKind;

    private ColumnKind ComputeRowKind()
    {
        bool allInt = Columns.Count > 0;
        bool allNumeric = Columns.Count > 0;
        foreach (RawColumn column in Columns)
        {
            if (column.Kind != ColumnKind.Int)
            {
                allInt = false;
            }

            if (column.Kind != ColumnKind.Int && column.Kind != ColumnKind.Float)
            {
                allNumeric = false;
            }
        }

        if (allInt)
        {
            return ColumnKind.Int;
        }

        return allNumeric ? ColumnKind.Float : ColumnKind.Object;
    }

    /// <summary>The value of a cell as <c>iterrows()</c> would hand it out (null = missing).</summary>
    public object? RowValue(RawColumn column, int row)
    {
        object? value = column.Values[row];
        if (value == null)
        {
            return null;
        }

        if (RowKind == ColumnKind.Float)
        {
            if (value is long l)
            {
                return (double)l;
            }
        }

        return value;
    }

    /// <summary>Python <c>str(value)</c> of a cell value (null is NaN, which callers must handle first).</summary>
    public static string PyStr(object? value)
    {
        if (value is DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        return PyRepr.Str(value);
    }

    // ------------------------------------------------------------- CSV

    /// <summary>Parses CSV text (UTF-8) like <c>pd.read_csv</c>.</summary>
    public static RawTable FromCsv(Stream stream)
    {
        byte[] bytes;
        using (MemoryStream buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }

        string text = new UTF8Encoding(false, true).GetString(bytes);
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text.Substring(1);
        }

        List<List<string>> records = ParseCsvRecords(text);
        if (records.Count == 0)
        {
            throw new InvalidDataException("No columns to parse from file");
        }

        List<string> header = records[0];
        int width = header.Count;
        List<List<string?>> columnTokens = new List<List<string?>>();
        for (int c = 0; c < width; c++)
        {
            columnTokens.Add(new List<string?>(records.Count - 1));
        }

        for (int r = 1; r < records.Count; r++)
        {
            List<string> record = records[r];
            if (record.Count > width)
            {
                throw new InvalidDataException("Error tokenizing data: expected " + width + " fields, saw " + record.Count);
            }

            for (int c = 0; c < width; c++)
            {
                string? token = c < record.Count ? record[c] : null;
                columnTokens[c].Add(token == null || NaTokens.Contains(token) ? null : token);
            }
        }

        List<string> names = MakeColumnNames(header.Cast<object?>().ToList());
        List<RawColumn> columns = new List<RawColumn>();
        for (int c = 0; c < width; c++)
        {
            columns.Add(InferCsvColumn(names[c], columnTokens[c]));
        }

        return new RawTable(columns, records.Count - 1);
    }

    private static List<List<string>> ParseCsvRecords(string text)
    {
        List<List<string>> records = new List<List<string>>();
        List<string> record = new List<string>();
        StringBuilder field = new StringBuilder();
        bool inQuotes = false;
        bool fieldStarted = false;   // any content (or quote) seen in the current field
        bool lineHasContent = false; // distinguishes a blank line from a line with empty fields

        int i = 0;
        while (i < text.Length)
        {
            char ch = text[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    continue;
                }

                field.Append(ch);
                i++;
                continue;
            }

            if (ch == '"' && !fieldStarted)
            {
                inQuotes = true;
                fieldStarted = true;
                lineHasContent = true;
                i++;
                continue;
            }

            if (ch == ',')
            {
                record.Add(field.ToString());
                field.Clear();
                fieldStarted = false;
                lineHasContent = true;
                i++;
                continue;
            }

            if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                i++;

                if (lineHasContent)
                {
                    record.Add(field.ToString());
                    records.Add(record);
                }

                record = new List<string>();
                field.Clear();
                fieldStarted = false;
                lineHasContent = false;
                continue;
            }

            field.Append(ch);
            fieldStarted = true;
            lineHasContent = true;
            i++;
        }

        if (inQuotes)
        {
            throw new InvalidDataException("Unterminated quoted field");
        }

        if (lineHasContent)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        return records;
    }

    private static bool TryParseNumber(string token, out double value)
    {
        value = 0.0;
        if (token.IndexOf('_') >= 0)
        {
            return false; // Python float() accepts underscores, the pandas parser does not.
        }

        return PyFloat.TryParse(token, out value);
    }

    private static bool IsIntegerToken(string token)
    {
        string s = token.Trim();
        if (s.Length == 0)
        {
            return false;
        }

        int start = (s[0] == '+' || s[0] == '-') ? 1 : 0;
        if (start == s.Length)
        {
            return false;
        }

        for (int i = start; i < s.Length; i++)
        {
            if (s[i] < '0' || s[i] > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static RawColumn InferCsvColumn(string name, List<string?> tokens)
    {
        int count = tokens.Count;
        int missing = 0;
        foreach (string? token in tokens)
        {
            if (token == null)
            {
                missing++;
            }
        }

        // A column without any value is a float column of NaN.
        if (missing == count)
        {
            return new RawColumn(name, ColumnKind.Float, new object?[count]);
        }

        bool allBool = true;
        bool allInt = true;
        bool allNumber = true;
        foreach (string? token in tokens)
        {
            if (token == null)
            {
                continue;
            }

            if (!TrueTokens.Contains(token) && !FalseTokens.Contains(token))
            {
                allBool = false;
            }

            if (!IsIntegerToken(token))
            {
                allInt = false;
            }

            double ignored;
            if (!TryParseNumber(token, out ignored))
            {
                allNumber = false;
            }
        }

        object?[] values = new object?[count];

        if (allBool)
        {
            for (int i = 0; i < count; i++)
            {
                values[i] = tokens[i] == null ? null : (object)TrueTokens.Contains(tokens[i]!);
            }

            return new RawColumn(name, missing == 0 ? ColumnKind.Bool : ColumnKind.Object, values);
        }

        if (allInt)
        {
            bool fitsLong = true;
            long[] longs = new long[count];
            for (int i = 0; i < count; i++)
            {
                if (tokens[i] == null)
                {
                    continue;
                }

                long parsed;
                if (!long.TryParse(tokens[i]!.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out parsed))
                {
                    fitsLong = false;
                    break;
                }

                longs[i] = parsed;
            }

            if (!fitsLong)
            {
                // Too large for int64: pandas keeps such a column as Python ints in an object column.
                for (int i = 0; i < count; i++)
                {
                    values[i] = tokens[i] == null
                        ? null
                        : (object)BigInteger.Parse(tokens[i]!.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                }

                return new RawColumn(name, ColumnKind.Object, values);
            }

            if (missing == 0)
            {
                for (int i = 0; i < count; i++)
                {
                    values[i] = longs[i];
                }

                return new RawColumn(name, ColumnKind.Int, values);
            }

            for (int i = 0; i < count; i++)
            {
                values[i] = tokens[i] == null ? null : (object)(double)longs[i];
            }

            return new RawColumn(name, ColumnKind.Float, values);
        }

        if (allNumber)
        {
            for (int i = 0; i < count; i++)
            {
                if (tokens[i] == null)
                {
                    continue;
                }

                double parsed;
                TryParseNumber(tokens[i]!, out parsed);
                values[i] = double.IsNaN(parsed) ? null : (object)parsed;
            }

            return new RawColumn(name, ColumnKind.Float, values);
        }

        // Text: raw strings, missing cells stay missing.
        for (int i = 0; i < count; i++)
        {
            values[i] = tokens[i];
        }

        return new RawColumn(name, ColumnKind.Object, values);
    }

    // ----------------------------------------------------------- Excel

    /// <summary>Reads the first sheet of an .xlsx or .xls file like <c>pd.read_excel</c>.</summary>
    public static RawTable FromExcel(Stream stream)
    {
        if (!encodingsRegistered)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            encodingsRegistered = true;
        }

        MemoryStream buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        byte[] head = new byte[8];
        int read = buffer.Read(head, 0, head.Length);
        buffer.Position = 0;

        bool isZip = read >= 4 && head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04;
        bool isOle = read >= 8
            && head[0] == 0xD0 && head[1] == 0xCF && head[2] == 0x11 && head[3] == 0xE0
            && head[4] == 0xA1 && head[5] == 0xB1 && head[6] == 0x1A && head[7] == 0xE1;

        if (!isZip && !isOle)
        {
            throw new InvalidDataException("Excel file format cannot be determined");
        }

        List<object?[]> rows = new List<object?[]>();

        using (IExcelDataReader reader = isZip
            ? ExcelReaderFactory.CreateOpenXmlReader(buffer)
            : ExcelReaderFactory.CreateBinaryReader(buffer))
        {
            while (reader.Read())
            {
                object?[] row = new object?[reader.FieldCount];
                for (int c = 0; c < row.Length; c++)
                {
                    row[c] = NormalizeExcelValue(reader.GetValue(c));
                }

                rows.Add(row);
            }
        }

        // Trailing completely empty rows are dropped, like pandas does.
        while (rows.Count > 0 && rows[rows.Count - 1].All(v => v == null))
        {
            rows.RemoveAt(rows.Count - 1);
        }

        if (rows.Count == 0)
        {
            return new RawTable(new List<RawColumn>(), 0);
        }

        int width = 0;
        foreach (object?[] row in rows)
        {
            for (int c = row.Length - 1; c >= 0; c--)
            {
                if (row[c] != null)
                {
                    width = Math.Max(width, c + 1);
                    break;
                }
            }
        }

        List<object?> headerCells = new List<object?>();
        for (int c = 0; c < width; c++)
        {
            headerCells.Add(c < rows[0].Length ? rows[0][c] : null);
        }

        List<string> names = MakeColumnNames(headerCells);
        int dataRows = rows.Count - 1;
        List<RawColumn> columns = new List<RawColumn>();

        for (int c = 0; c < width; c++)
        {
            object?[] cells = new object?[dataRows];
            for (int r = 0; r < dataRows; r++)
            {
                object?[] row = rows[r + 1];
                cells[r] = c < row.Length ? row[c] : null;
            }

            columns.Add(InferTypedColumn(names[c], cells));
        }

        return new RawTable(columns, dataRows);
    }

    /// <summary>Maps what the Excel reader returns to long / double / bool / string / DateTime / null.</summary>
    private static object? NormalizeExcelValue(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is string s)
        {
            return NaTokens.Contains(s) ? null : s;
        }

        if (value is bool || value is DateTime)
        {
            return value;
        }

        if (value is double d)
        {
            return NumberToValue(d);
        }

        if (value is float f)
        {
            return NumberToValue(f);
        }

        if (value is decimal m)
        {
            return NumberToValue((double)m);
        }

        if (value is int || value is long || value is short || value is byte)
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        return value.ToString();
    }

    /// <summary>Excel stores all numbers as doubles; pandas turns whole numbers into ints.</summary>
    private static object? NumberToValue(double d)
    {
        if (double.IsNaN(d))
        {
            return null;
        }

        if (!double.IsInfinity(d) && d == Math.Floor(d) && Math.Abs(d) < 9.0e18)
        {
            return (long)d;
        }

        return d;
    }

    private static RawColumn InferTypedColumn(string name, object?[] cells)
    {
        int count = cells.Length;
        int missing = 0;
        bool allLong = true;
        bool allNumeric = true;
        bool allBool = true;
        bool allBoolText = true;

        foreach (object? cell in cells)
        {
            if (cell == null)
            {
                missing++;
                continue;
            }

            if (!(cell is long))
            {
                allLong = false;
            }

            if (!(cell is long || cell is double))
            {
                allNumeric = false;
            }

            if (!(cell is bool))
            {
                allBool = false;
            }

            string? text = cell as string;
            if (text == null || (!TrueTokens.Contains(text) && !FalseTokens.Contains(text)))
            {
                allBoolText = false;
            }
        }

        if (missing == count)
        {
            return new RawColumn(name, ColumnKind.Float, new object?[count]);
        }

        // Booleans, or the text TRUE/FALSE (for instance the cached result of a =FALSE() formula).
        if (allBool || allBoolText)
        {
            object?[] bools = new object?[count];
            for (int i = 0; i < count; i++)
            {
                object? cell = cells[i];
                if (cell == null)
                {
                    bools[i] = null;
                }
                else if (cell is bool)
                {
                    bools[i] = cell;
                }
                else
                {
                    bools[i] = TrueTokens.Contains((string)cell);
                }
            }

            return new RawColumn(name, missing == 0 ? ColumnKind.Bool : ColumnKind.Object, bools);
        }

        if (allLong && missing == 0)
        {
            return new RawColumn(name, ColumnKind.Int, cells);
        }

        if (allNumeric)
        {
            object?[] floats = new object?[count];
            for (int i = 0; i < count; i++)
            {
                object? cell = cells[i];
                floats[i] = cell is long l ? (object)(double)l : cell;
            }

            return new RawColumn(name, ColumnKind.Float, floats);
        }

        return new RawColumn(name, ColumnKind.Object, cells);
    }

    // ----------------------------------------------------- column names

    /// <summary>
    /// Header text to column names: blank headers become "Unnamed: n" and duplicates get ".1", ".2" suffixes,
    /// exactly like pandas.
    /// </summary>
    private static List<string> MakeColumnNames(List<object?> headerCells)
    {
        List<string> names = new List<string>();
        for (int i = 0; i < headerCells.Count; i++)
        {
            object? cell = headerCells[i];
            string text;
            if (cell == null)
            {
                text = "Unnamed: " + i.ToString(CultureInfo.InvariantCulture);
            }
            else if (cell is string s)
            {
                text = s.Length == 0 ? "Unnamed: " + i.ToString(CultureInfo.InvariantCulture) : s;
            }
            else
            {
                text = PyStr(cell);
            }

            names.Add(text);
        }

        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i];
            int current;
            counts.TryGetValue(name, out current);

            while (current > 0)
            {
                counts[name] = current + 1;
                name = name + "." + current.ToString(CultureInfo.InvariantCulture);
                counts.TryGetValue(name, out current);
            }

            names[i] = name;
            counts[name] = current + 1;
        }

        return names;
    }
}
