using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using QueueLab.Core.Common;
using QueueLab.Core.Services;
using QueueLab.Core.Statistics;
using QueueLab.Desktop.Ui;

namespace QueueLab.Desktop.Pages;

/// <summary>Side-by-side comparison of up to 20 configurations.</summary>
internal sealed class ComparePage : PageBase
{
    private static readonly string[] MetricKeys =
    {
        "average_waiting_time",
        "average_system_time",
        "average_number_in_queue",
        "average_number_in_system",
        "utilization",
        "throughput",
    };

    private sealed class ScenarioRow
    {
        public TextBox Name = Widgets.Input(string.Empty);
        public ComboBox Type = Widgets.Combo(new[] { "Single", "Multi" });
        public TextBox Lambda = Widgets.Input("0.8");
        public TextBox Mu = Widgets.Input("1");
        public TextBox Servers = Widgets.Input("1");
        public TextBox Customers = Widgets.Input("100");
        public TextBox Replications = Widgets.Input("20");
    }

    private readonly List<ScenarioRow> _rows = new List<ScenarioRow>();
    private readonly StackPanel _rowHost = new StackPanel { Spacing = 8 };
    private readonly ContentControl _results = new ContentControl();
    private readonly ActionButton _add;
    private readonly ActionButton _run;
    private ComparisonResult? _last;

    public ComparePage()
    {
        Body.Children.Add(Widgets.PageHeader(
            "What-if analysis",
            "Compare Scenarios",
            "Define up to 20 configurations; each is replicated and summarised with confidence intervals."));

        _add = new ActionButton("Add scenario", false);
        _add.Clicked += (sender, e) =>
        {
            if (_rows.Count < ComparisonService.MaxScenarios)
            {
                _rows.Add(NewRow(_rows.Count + 1, 1));
                RebuildRows();
            }
        };

        _run = new ActionButton("Compare");
        _run.Clicked += (sender, e) => Run();

        _rows.Add(NewRow(1, 1));
        _rows.Add(NewRow(2, 2));
        RebuildRows();

        Body.Children.Add(Widgets.Panel("Scenarios", null, _rowHost, Widgets.ButtonRow(_add, _run)));

        _results.Margin = new Thickness(0, 16, 0, 0);
        Body.Children.Add(_results);
    }

    private static ScenarioRow NewRow(int number, int servers)
    {
        ScenarioRow row = new ScenarioRow();
        row.Name.Text = "Scenario " + number;
        row.Servers.Text = servers.ToString();
        row.Type.SelectedIndex = servers == 1 ? 0 : 1;
        return row;
    }

    private void RebuildRows()
    {
        // The input controls are reused for the new row layout, so release them from the old rows first
        // (a control can only have one parent).
        foreach (Control child in _rowHost.Children.ToList())
        {
            Grid? oldRow = child as Grid;
            if (oldRow != null)
            {
                oldRow.Children.Clear();
            }
        }

        _rowHost.Children.Clear();

        string[] headers = { "Name", "Type", "λ", "μ", "Servers", "Customers", "Reps", string.Empty };
        string columns = "2*,1.2*,*,*,*,1.2*,*,36";
        _rowHost.Children.Add(MakeGridRow(columns, headers.Select(h => (Control)Widgets.Text(h, 12, FontWeight.SemiBold, AppColors.MutedForeground)).ToList()));

        foreach (ScenarioRow row in _rows)
        {
            ScenarioRow current = row;
            ActionButton remove = new ActionButton("×", false);
            remove.Padding = new Thickness(0, 7);
            remove.Enabled = _rows.Count > 1;
            remove.Clicked += (sender, e) =>
            {
                if (_rows.Count > 1)
                {
                    _rows.Remove(current);
                    RebuildRows();
                }
            };

            _rowHost.Children.Add(MakeGridRow(
                columns,
                new List<Control> { row.Name, row.Type, row.Lambda, row.Mu, row.Servers, row.Customers, row.Replications, remove }));
        }

        _add.Enabled = _rows.Count < ComparisonService.MaxScenarios;
    }

    private static Grid MakeGridRow(string columns, IReadOnlyList<Control> cells)
    {
        Grid grid = new Grid { ColumnDefinitions = new ColumnDefinitions(columns) };
        for (int i = 0; i < cells.Count; i++)
        {
            cells[i].Margin = new Thickness(i == 0 ? 0 : 4, 0, i == cells.Count - 1 ? 0 : 4, 0);
            cells[i].VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(cells[i], i);
            grid.Children.Add(cells[i]);
        }

        return grid;
    }

    private async void Run()
    {
        _run.Enabled = false;
        _run.Text = "Running…";
        try
        {
            List<object?> scenarios = new List<object?>();
            foreach (ScenarioRow row in _rows)
            {
                Dictionary<string, object?> scenario = new Dictionary<string, object?>();
                scenario["name"] = row.Name.Text ?? string.Empty;
                scenario["simulation_type"] = row.Type.SelectedIndex == 1 ? "multi_server" : "single_server";
                scenario["arrival_rate"] = Inputs.PositiveNumber(row.Lambda.Text, "Arrival rate");
                scenario["service_rate"] = Inputs.PositiveNumber(row.Mu.Text, "Service rate");
                scenario["server_count"] = (long)Inputs.PositiveInt(row.Servers.Text, "Servers", Inputs.MaxServers);
                scenario["customer_count"] = (long)Inputs.PositiveInt(row.Customers.Text, "Customers", Inputs.MaxCustomers);
                scenario["replication_count"] = (long)Inputs.PositiveInt(row.Replications.Text, "Replications", ReplicationService.MaxReplications);
                scenarios.Add(scenario);
            }

            ComparisonResult result = await Task.Run(() => ComparisonService.CompareScenarios(scenarios));
            _last = result;
            Show(_results, Build(result));
        }
        catch (Exception error)
        {
            Show(_results, Widgets.ErrorBox(error.Message));
        }
        finally
        {
            _run.Enabled = true;
            _run.Text = "Compare";
        }
    }

    private Control Build(ComparisonResult result)
    {
        StackPanel root = new StackPanel { Spacing = 16 };

        ComboBox metricPicker = Widgets.Combo(MetricKeys.Select(Fmt.Label).ToList(), 0);
        BarChart chart = new BarChart();
        metricPicker.Width = 230;
        Border chartPanel = Widgets.Panel(
            "Metric comparison",
            "Mean with confidence-interval error bars",
            chart,
            metricPicker);

        Action refresh = () =>
        {
            string metric = MetricKeys[Math.Max(0, metricPicker.SelectedIndex)];
            List<string> labels = new List<string>();
            List<double> means = new List<double>();
            List<double> errors = new List<double>();
            List<Color> colors = new List<Color>();
            int i = 0;
            foreach (ScenarioResult scenario in result.Scenarios)
            {
                MetricSummary? m;
                scenario.Metrics.TryGetValue(metric, out m);
                labels.Add(scenario.ScenarioName);
                means.Add(m == null ? 0.0 : m.Mean);
                errors.Add(m == null || !m.MarginOfError.HasValue ? 0.0 : m.MarginOfError.Value);
                colors.Add(AppColors.ChartColors[i % AppColors.ChartColors.Length]);
                i++;
            }

            chart.SetData(labels, means, errors, colors, null, "F3");
        };
        refresh();
        metricPicker.SelectionChanged += (sender, e) => refresh();
        root.Children.Add(chartPanel);

        // Results table: mean ± margin for each metric.
        string[] headers = { "Scenario", "Type", "Servers", "Wq", "W", "Lq", "L", "ρ", "Throughput" };
        List<string[]> rows = new List<string[]>();
        foreach (ScenarioResult scenario in result.Scenarios)
        {
            List<string> row = new List<string>
            {
                scenario.ScenarioName,
                scenario.SimulationType == "multi_server" ? "Multi" : "Single",
                scenario.ServerCount.ToString(),
            };
            foreach (string metric in MetricKeys)
            {
                MetricSummary? m;
                scenario.Metrics.TryGetValue(metric, out m);
                row.Add(m == null ? "—" : Fmt.Num(m.Mean, 3) + (m.MarginOfError.HasValue ? " ± " + Fmt.Num(m.MarginOfError, 3) : string.Empty));
            }

            rows.Add(row.ToArray());
        }

        ActionButton csv = new ActionButton("Export CSV", false);
        ActionButton excel = new ActionButton("Export Excel", false);
        csv.Clicked += async (sender, e) => await Export(false);
        excel.Clicked += async (sender, e) => await Export(true);

        root.Children.Add(Widgets.Panel("Results", null, Widgets.Table(headers, rows, 50, 360), Widgets.ButtonRow(csv, excel)));
        return root;
    }

    private async Task Export(bool excel)
    {
        ComparisonResult? result = _last;
        if (result == null)
        {
            return;
        }

        try
        {
            List<Dictionary<string, object?>> rows = new List<Dictionary<string, object?>>();
            foreach (ScenarioResult scenario in result.Scenarios)
            {
                Dictionary<string, object?> row = new Dictionary<string, object?>();
                row["scenario"] = scenario.ScenarioName;
                row["type"] = scenario.SimulationType;
                row["servers"] = scenario.ServerCount;
                row["arrival_rate"] = scenario.ArrivalRate;
                row["service_rate"] = scenario.ServiceRate;
                row["replications"] = scenario.ReplicationCount;
                foreach (KeyValuePair<string, MetricSummary?> pair in scenario.Metrics)
                {
                    row[pair.Key + "_mean"] = pair.Value == null ? null : (object?)pair.Value.Mean;
                    row[pair.Key + "_margin"] = pair.Value == null ? null : (object?)pair.Value.MarginOfError;
                }

                rows.Add(row);
            }

            if (excel)
            {
                byte[] content = await Task.Run(() => ExportService.ToExcel(rows, "Comparison"));
                await FileDialogs.SaveAsync(this, "queuelab_comparison.xlsx", "Excel workbook", "*.xlsx", content);
            }
            else
            {
                string text = await Task.Run(() => ExportService.ToCsv(rows));
                await FileDialogs.SaveAsync(this, "queuelab_comparison.csv", "CSV file", "*.csv", new System.Text.UTF8Encoding(false).GetBytes(text));
            }
        }
        catch (Exception)
        {
            // Saving was cancelled or failed.
        }
    }
}
