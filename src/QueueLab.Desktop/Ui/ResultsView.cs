using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using QueueLab.Core.Models;
using QueueLab.Core.Services;

namespace QueueLab.Desktop.Ui;

/// <summary>Everything the web UI shows for one simulation run: key figures, charts and the customer log.</summary>
internal static class ResultsView
{
    public static Control Build(SimulationResult result, Control owner)
    {
        StackPanel root = new StackPanel { Spacing = 16 };

        int servers = result.ServerCount;
        List<Control> stats = new List<Control>
        {
            Widgets.Stat("Avg waiting time (Wq)", Fmt.Num(result.AverageWaitingTime), "min", null, true),
            Widgets.Stat("Avg time in system (W)", Fmt.Num(result.AverageSystemTime), "min"),
            Widgets.Stat("Avg queue length (Lq)", Fmt.Num(result.AverageNumberInQueue)),
            Widgets.Stat("Avg in system (L)", Fmt.Num(result.AverageNumberInSystem)),
            Widgets.Stat("Utilization (ρ)", Fmt.Pct(result.Utilization), null, servers + (servers > 1 ? " servers" : " server")),
            Widgets.Stat("Throughput", Fmt.Num(result.Throughput), "/min"),
            Widgets.Stat("Customers processed", Fmt.Num(result.CustomersProcessed, 0)),
            Widgets.Stat("Total idle time", Fmt.Num(result.TotalIdleTime), "min"),
        };
        root.Children.Add(Widgets.Tiles(4, stats));

        // Queue length over time (step area): customers waiting vs. in the system.
        XYChart timeline = new XYChart();
        BuildTimeline(timeline, result.CustomerResults);
        root.Children.Add(Widgets.Panel("Queue length over time", "Customers waiting vs. in system", timeline));

        // Waiting and system time per customer.
        XYChart perCustomer = new XYChart();
        double[] index = new double[result.CustomerResults.Count];
        double[] waits = new double[index.Length];
        double[] system = new double[index.Length];
        for (int i = 0; i < index.Length; i++)
        {
            index[i] = i + 1;
            waits[i] = result.CustomerResults[i].WaitingTime;
            system[i] = result.CustomerResults[i].SystemTime;
        }

        perCustomer.SetData(
            new[]
            {
                new ChartSeries("In system", AppColors.Chart3Color, index, system) { Thickness = 1.5 },
                new ChartSeries("Waiting", AppColors.Chart2Color, index, waits) { Thickness = 1.5 },
            },
            "Customer #");
        root.Children.Add(Widgets.Panel("Waiting & system time per customer", "Customer flow through the system", perCustomer));

        // Histogram and per-server utilization side by side.
        BarChart histogram = new BarChart();
        BuildHistogram(histogram, waits);

        BarChart utilization = new BarChart();
        List<string> serverLabels = new List<string>();
        List<double> serverValues = new List<double>();
        List<Color> serverColors = new List<Color>();
        int colorIndex = 0;
        foreach (KeyValuePair<string, double> pair in result.ServerUtilization)
        {
            serverLabels.Add("S" + pair.Key);
            serverValues.Add(Math.Round(pair.Value * 100, 2));
            serverColors.Add(AppColors.ChartColors[colorIndex % AppColors.ChartColors.Length]);
            colorIndex++;
        }

        utilization.SetData(serverLabels, serverValues, null, serverColors, null, "F1", 100);

        Grid pair2 = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        Border histogramPanel = Widgets.Panel("Waiting time distribution", "Histogram (minutes)", histogram);
        Border utilizationPanel = Widgets.Panel("Server utilization", "Busy time share per server (%)", utilization);
        histogramPanel.Margin = new Thickness(0, 0, 8, 0);
        utilizationPanel.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(utilizationPanel, 1);
        pair2.Children.Add(histogramPanel);
        pair2.Children.Add(utilizationPanel);
        root.Children.Add(pair2);

        // Customer log with exports.
        ActionButton csv = new ActionButton("Export CSV", false);
        ActionButton excel = new ActionButton("Export Excel", false);
        csv.Clicked += async (sender, e) => await Export(owner, result, false);
        excel.Clicked += async (sender, e) => await Export(owner, result, true);

        List<string> headers = new List<string> { "ID", "Arrival", "Service", "Start", "End", "Wait", "In system" };
        if (servers > 1)
        {
            headers.Add("Server");
        }

        List<string[]> rows = new List<string[]>();
        foreach (CustomerSimulationResult c in result.CustomerResults)
        {
            List<string> row = new List<string>
            {
                c.CustomerId,
                Fmt.Num(c.ArrivalTime, 2),
                Fmt.Num(c.ServiceTime, 2),
                Fmt.Num(c.ServiceStart, 2),
                Fmt.Num(c.ServiceEnd, 2),
                Fmt.Num(c.WaitingTime, 2),
                Fmt.Num(c.SystemTime, 2),
            };
            if (servers > 1)
            {
                row.Add(c.ServerId.ToString());
            }

            rows.Add(row.ToArray());
        }

        root.Children.Add(Widgets.Panel(
            "Customer log",
            result.CustomerResults.Count + " customers",
            Widgets.Table(headers, rows),
            Widgets.ButtonRow(csv, excel)));

        return root;
    }

    private static async Task Export(Control owner, SimulationResult result, bool excel)
    {
        try
        {
            if (excel)
            {
                byte[] content = await Task.Run(() => ExportService.SimulationToExcel(result, "customers"));
                await FileDialogs.SaveAsync(owner, "queuelab_simulation.xlsx", "Excel workbook", "*.xlsx", content);
            }
            else
            {
                string text = await Task.Run(() => ExportService.SimulationToCsv(result, "customers"));
                await FileDialogs.SaveAsync(owner, "queuelab_simulation.csv", "CSV file", "*.csv", new System.Text.UTF8Encoding(false).GetBytes(text));
            }
        }
        catch (Exception)
        {
            // Saving was cancelled or failed; there is nothing useful to show beyond the missing file.
        }
    }

    /// <summary>Waiting count and system population as step series, like the web UI's timeline().</summary>
    private static void BuildTimeline(XYChart chart, IReadOnlyList<CustomerSimulationResult> customers)
    {
        List<(double Time, int QueueDelta, int SystemDelta)> events = new List<(double Time, int QueueDelta, int SystemDelta)>();
        foreach (CustomerSimulationResult c in customers)
        {
            events.Add((c.ArrivalTime, 1, 1));
            events.Add((c.ServiceStart, -1, 0));
            events.Add((c.ServiceEnd, 0, -1));
        }

        events.Sort((a, b) => a.Time.CompareTo(b.Time));

        List<double> times = new List<double>();
        List<double> queue = new List<double>();
        List<double> system = new List<double>();
        int q = 0;
        int s = 0;

        foreach ((double time, int dq, int ds) in events)
        {
            q += dq;
            s += ds;
            if (times.Count > 0 && Math.Abs(times[times.Count - 1] - time) < 1e-9)
            {
                queue[queue.Count - 1] = q;
                system[system.Count - 1] = s;
            }
            else
            {
                times.Add(time);
                queue.Add(q);
                system.Add(s);
            }
        }

        // Keep the drawing light for very long runs.
        int stride = Math.Max(1, (int)Math.Ceiling(times.Count / 600.0));
        List<double> tx = new List<double>();
        List<double> tq = new List<double>();
        List<double> ts = new List<double>();
        for (int i = 0; i < times.Count; i += stride)
        {
            tx.Add(times[i]);
            tq.Add(queue[i]);
            ts.Add(system[i]);
        }

        chart.SetData(
            new[]
            {
                new ChartSeries("In system", AppColors.Chart3Color, tx.ToArray(), ts.ToArray()) { Step = true, Fill = true, FillOpacity = 0.12, Thickness = 1.5 },
                new ChartSeries("Waiting", AppColors.Chart1Color, tx.ToArray(), tq.ToArray()) { Step = true, Fill = true, FillOpacity = 0.25, Thickness = 1.5 },
            },
            "Time (min)");
    }

    /// <summary>12-bin histogram of waiting times.</summary>
    private static void BuildHistogram(BarChart chart, double[] waits)
    {
        const int bins = 12;
        double max = waits.Length == 0 ? 0 : waits.Max();
        double size = max / bins;
        if (size <= 0)
        {
            size = 1;
        }

        double[] counts = new double[bins];
        string[] labels = new string[bins];
        for (int k = 0; k < bins; k++)
        {
            labels[k] = (k * size).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        }

        foreach (double w in waits)
        {
            int bin = Math.Min(bins - 1, (int)Math.Floor(w / size));
            counts[bin]++;
        }

        chart.SetData(labels, counts, null, null, AppColors.Chart1Color, "F0");
    }
}
