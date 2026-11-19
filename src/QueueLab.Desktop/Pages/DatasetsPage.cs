using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using QueueLab.Core.Common;
using QueueLab.Core.DataProcessing;
using QueueLab.Core.Models;
using QueueLab.Core.Services;
using QueueLab.Core.Statistics;
using QueueLab.Desktop.Ui;

namespace QueueLab.Desktop.Pages;

/// <summary>Upload a CSV / Excel file to describe it or simulate it, plus the built-in NADRA data.</summary>
internal sealed class DatasetsPage : PageBase
{
    private readonly ActionButton _choose;
    private readonly TextBlock _fileName;
    private readonly ComboBox _type;
    private readonly TextBox _servers;
    private readonly StackPanel _serversField;
    private readonly ActionButton _describe;
    private readonly ActionButton _simulate;
    private readonly ContentControl _upload = new ContentControl();
    private readonly ContentControl _nadra = new ContentControl();

    private string? _name;
    private byte[]? _content;

    public DatasetsPage()
    {
        Body.Children.Add(Widgets.PageHeader(
            "Input data",
            "Datasets",
            "Upload a CSV or Excel file with arrival_time and service_time columns (minutes)."));

        _choose = new ActionButton("Choose CSV / XLSX / XLS…", false);
        _choose.HorizontalAlignment = HorizontalAlignment.Stretch;
        _choose.Clicked += (sender, e) => Choose();

        _fileName = Widgets.Text("No file chosen", 12, FontWeight.Normal, AppColors.MutedForeground, true);

        _type = Widgets.Combo(new[] { "Single-server", "Multi-server" });
        _servers = Widgets.Input("2");
        _serversField = Widgets.Field("Servers", _servers);
        _serversField.IsVisible = false;
        _type.SelectionChanged += (sender, e) => _serversField.IsVisible = _type.SelectedIndex == 1;

        _describe = new ActionButton("Describe", false);
        _describe.Clicked += (sender, e) => Describe();
        _simulate = new ActionButton("Simulate");
        _simulate.Clicked += (sender, e) => Simulate();
        _describe.Enabled = false;
        _simulate.Enabled = false;

        Grid buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        _describe.Margin = new Thickness(0, 0, 4, 0);
        _simulate.Margin = new Thickness(4, 0, 0, 0);
        Grid.SetColumn(_simulate, 1);
        buttons.Children.Add(_describe);
        buttons.Children.Add(_simulate);

        Control form = Widgets.Panel(
            "Upload dataset",
            null,
            FormStack(
                _choose,
                _fileName,
                Widgets.Field("Simulation type", _type),
                _serversField,
                buttons));

        StackPanel right = new StackPanel { Spacing = 16 };
        right.Children.Add(_upload);
        right.Children.Add(_nadra);

        Body.Children.Add(TwoColumns(form, right));

        _nadra.Content = Widgets.EmptyState("Loading observed data…", "Reading the built-in NADRA dataset.");
        LoadNadra();
    }

    private async void Choose()
    {
        try
        {
            (string Name, byte[] Content)? picked = await FileDialogs.OpenDatasetAsync(this);
            if (picked == null)
            {
                return;
            }

            _name = picked.Value.Name;
            _content = picked.Value.Content;
            _fileName.Text = _name;
            _describe.Enabled = true;
            _simulate.Enabled = true;
        }
        catch (Exception error)
        {
            Show(_upload, Widgets.ErrorBox(error.Message));
        }
    }

    private List<SimulationCustomer> LoadCustomers()
    {
        using (MemoryStream stream = new MemoryStream(_content!))
        {
            return SimulationUploadLoader.Load(stream, _name);
        }
    }

    private async void Describe()
    {
        if (_content == null)
        {
            return;
        }

        _describe.Enabled = false;
        try
        {
            InputDataStatistics.Summary summary = await Task.Run(() => InputDataStatistics.SummarizeCustomers(LoadCustomers()));
            Show(_upload, BuildSummary(summary));
        }
        catch (Exception error)
        {
            Show(_upload, Widgets.ErrorBox(error.Message));
        }
        finally
        {
            _describe.Enabled = true;
        }
    }

    private async void Simulate()
    {
        if (_content == null)
        {
            return;
        }

        _simulate.Enabled = false;
        _simulate.Text = "Running…";
        try
        {
            bool multi = _type.SelectedIndex == 1;
            int servers = multi ? Inputs.PositiveInt(_servers.Text, "Servers", Inputs.MaxServers) : 1;

            SimulationResult result = await Task.Run(() =>
            {
                List<SimulationCustomer> customers = LoadCustomers();
                List<ISimCustomer> list = new List<ISimCustomer>(customers.Count);
                foreach (SimulationCustomer customer in customers)
                {
                    list.Add(customer);
                }

                return SimulationService.Run(multi ? "multi_server" : "single_server", list, servers);
            });

            Show(_upload, ResultsView.Build(result, this));
        }
        catch (Exception error)
        {
            Show(_upload, Widgets.ErrorBox(error.Message));
        }
        finally
        {
            _simulate.Enabled = true;
            _simulate.Text = "Simulate";
        }
    }

    private static Control BuildSummary(InputDataStatistics.Summary summary)
    {
        StackPanel root = new StackPanel { Spacing = 16 };

        List<Control> stats = new List<Control>
        {
            Widgets.Stat("Customers", Fmt.Num(summary.CustomerCount, 0)),
            Widgets.Stat("Estimated λ", Fmt.Num(summary.EstimatedArrivalRate), "/min"),
            Widgets.Stat("Estimated μ", Fmt.Num(summary.EstimatedServiceRate), "/min"),
        };

        List<string[]> rows = new List<string[]>
        {
            SeriesRow("Arrival times", summary.ArrivalTimes),
            SeriesRow("Interarrival times", summary.InterarrivalTimes),
            SeriesRow("Service times", summary.ServiceTimes),
        };

        StackPanel body = new StackPanel { Spacing = 16 };
        body.Children.Add(Widgets.Tiles(3, stats));
        body.Children.Add(Widgets.Table(
            new[] { "Variable", "Count", "Mean", "Median", "Std. deviation", "Minimum", "Maximum" },
            rows,
            10,
            200));

        root.Children.Add(Widgets.Panel(
            "Descriptive statistics",
            summary.CustomerCount + " customers · " + summary.TimeUnit,
            body));
        return root;
    }

    private static string[] SeriesRow(string name, InputDataStatistics.SeriesSummary series)
    {
        return new[]
        {
            name,
            series.Count.ToString(),
            Fmt.Num(series.Mean, 3),
            Fmt.Num(series.Median, 3),
            Fmt.Num(series.StandardDeviation, 3),
            Fmt.Num(series.Minimum, 3),
            Fmt.Num(series.Maximum, 3),
        };
    }

    private async void LoadNadra()
    {
        try
        {
            List<Customer> customers = await Task.Run(() => NadraDataLoader.LoadFile(Inputs.NadraFile));

            List<string[]> rows = new List<string[]>();
            foreach (Customer c in customers)
            {
                rows.Add(new[]
                {
                    c.CustomerId,
                    c.Gender ?? "—",
                    Fmt.Num(c.ArrivalMin, 2),
                    Fmt.Num(c.ServiceStartMin, 2),
                    Fmt.Num(c.ServiceEndMin, 2),
                    Fmt.Num(c.MainWaitMin, 2),
                    Fmt.Num(c.TotalTimeMin, 2),
                    c.Reneged ? "Yes" : "No",
                });
            }

            Show(_nadra, Widgets.Panel(
                "NADRA Day 1 First 100",
                customers.Count + " customers · built-in observed data",
                Widgets.Table(
                    new[] { "ID", "Gender", "Arrival", "Service start", "Service end", "Main wait", "Total", "Reneged" },
                    rows,
                    500,
                    420)));
        }
        catch (Exception error)
        {
            Show(_nadra, Widgets.ErrorBox("Could not load the built-in dataset: " + error.Message));
        }
    }
}
