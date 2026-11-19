using Avalonia.Controls;
using Avalonia.Layout;
using QueueLab.Core.Services;
using QueueLab.Core.Statistics;
using QueueLab.Desktop.Ui;

namespace QueueLab.Desktop.Pages;

/// <summary>Independent replications with confidence intervals.</summary>
internal sealed class ReplicationsPage : PageBase
{
    private static readonly string[] ChartedMetrics =
    {
        "average_waiting_time",
        "average_system_time",
        "average_number_in_queue",
        "utilization",
    };

    private readonly ComboBox _type;
    private readonly TextBox _lambda;
    private readonly TextBox _mu;
    private readonly TextBox _servers;
    private readonly StackPanel _serversField;
    private readonly TextBox _customers;
    private readonly TextBox _replications;
    private readonly TextBox _confidence;
    private readonly ComboBox _arrival;
    private readonly ComboBox _service;
    private readonly TextBox _seed;
    private readonly ActionButton _run;
    private readonly ContentControl _results = new ContentControl();

    public ReplicationsPage()
    {
        Body.Children.Add(Widgets.PageHeader(
            "Statistical output analysis",
            "Replications",
            "Run independent replications and estimate each metric with a confidence interval."));

        _type = Widgets.Combo(new[] { "Single-server", "Multi-server" });
        _lambda = Widgets.Input("0.5");
        _mu = Widgets.Input("1");
        _servers = Widgets.Input("2");
        _customers = Widgets.Input("100");
        _replications = Widgets.Input("30");
        _confidence = Widgets.Input("0.95");
        _arrival = Widgets.Combo(new[] { "Exponential", "Deterministic" });
        _service = Widgets.Combo(new[] { "Exponential", "Deterministic" });
        _seed = Widgets.Input("42");

        _serversField = Widgets.Field("Servers c", _servers);
        _serversField.IsVisible = false;
        _type.SelectionChanged += (sender, e) => _serversField.IsVisible = _type.SelectedIndex == 1;

        _run = new ActionButton("Run replications");
        _run.HorizontalAlignment = HorizontalAlignment.Stretch;
        _run.Clicked += (sender, e) => Run();

        Control form = Widgets.Panel(
            "Experiment",
            null,
            FormStack(
                Widgets.Field("Simulation type", _type),
                Widgets.Field("Arrival rate λ", _lambda),
                Widgets.Field("Service rate μ", _mu),
                _serversField,
                Widgets.Field("Customers per replication", _customers),
                Widgets.Field("Replications", _replications),
                Widgets.Field("Confidence level", _confidence, "e.g. 0.90, 0.95, 0.99"),
                Widgets.Field("Arrival distribution", _arrival),
                Widgets.Field("Service distribution", _service),
                Widgets.Field("Base seed", _seed),
                _run));

        _results.Content = Widgets.EmptyState(
            "No replications yet",
            "Configure the experiment and press “Run replications” to get means and confidence intervals.");
        Body.Children.Add(TwoColumns(form, _results));
    }

    private async void Run()
    {
        _run.Enabled = false;
        _run.Text = "Running…";
        try
        {
            string type = _type.SelectedIndex == 1 ? "multi_server" : "single_server";
            double lambda = Inputs.PositiveNumber(_lambda.Text, "Arrival rate");
            double mu = Inputs.PositiveNumber(_mu.Text, "Service rate");
            int servers = _type.SelectedIndex == 1 ? Inputs.PositiveInt(_servers.Text, "Servers", Inputs.MaxServers) : 1;
            int customers = Inputs.PositiveInt(_customers.Text, "Customers per replication", Inputs.MaxCustomers);
            int replications = Inputs.PositiveInt(_replications.Text, "Replications", ReplicationService.MaxReplications);
            double confidence = Inputs.ConfidenceLevel(_confidence.Text);
            long? seed = Inputs.OptionalLongSeed(_seed.Text);
            string arrival = _arrival.SelectedIndex == 0 ? "exponential" : "deterministic";
            string service = _service.SelectedIndex == 0 ? "exponential" : "deterministic";

            ReplicationResult result = await Task.Run(() => ReplicationService.Run(
                type, customers, lambda, mu, replications, servers, arrival, service, seed, confidence));

            Show(_results, Build(result));
        }
        catch (Exception error)
        {
            Show(_results, Widgets.ErrorBox(error.Message));
        }
        finally
        {
            _run.Enabled = true;
            _run.Text = "Run replications";
        }
    }

    private static Control Build(ReplicationResult result)
    {
        StackPanel root = new StackPanel { Spacing = 16 };
        ReplicationSummary summary = result.Summary;

        List<string[]> rows = new List<string[]>();
        foreach (KeyValuePair<string, MetricSummary?> pair in summary.Metrics)
        {
            MetricSummary? m = pair.Value;
            if (m == null)
            {
                continue;
            }

            rows.Add(new[]
            {
                Fmt.Label(pair.Key),
                Fmt.Num(m.Mean, 4),
                Fmt.Num(m.StandardDeviation, 4),
                Fmt.Num(m.Lower, 4),
                Fmt.Num(m.Upper, 4),
                Fmt.Num(m.MarginOfError, 4),
            });
        }

        root.Children.Add(Widgets.Panel(
            Math.Round(summary.ConfidenceLevel * 100).ToString("F0") + "% confidence intervals",
            summary.ReplicationCount + " replications × " + result.CustomerCountPerReplication + " customers",
            Widgets.Table(new[] { "Metric", "Mean", "Std. dev", "Lower", "Upper", "± Margin" }, rows, 50, 300)));

        foreach (string metric in ChartedMetrics)
        {
            MetricSummary? m;
            if (!summary.Metrics.TryGetValue(metric, out m) || m == null)
            {
                continue;
            }

            double[] x = new double[result.Replications.Count];
            double[] y = new double[x.Length];
            for (int i = 0; i < x.Length; i++)
            {
                x[i] = result.Replications[i].Replication;
                y[i] = ValueOf(result.Replications[i], metric);
            }

            List<ChartSeries> series = new List<ChartSeries>
            {
                new ChartSeries("Replication", AppColors.Chart1Color, x, y) { Dots = true, Thickness = 1.4 },
                new ChartSeries("Mean", AppColors.Chart2Color, new[] { x[0], x[x.Length - 1] }, new[] { m.Mean, m.Mean }) { Dashed = true, Thickness = 1.6 },
            };

            if (m.Lower.HasValue && m.Upper.HasValue)
            {
                series.Add(new ChartSeries("CI bounds", AppColors.Chart3Color, new[] { x[0], x[x.Length - 1] }, new[] { m.Lower.Value, m.Lower.Value }) { Dashed = true, Thickness = 1.0 });
                series.Add(new ChartSeries(string.Empty, AppColors.Chart3Color, new[] { x[0], x[x.Length - 1] }, new[] { m.Upper.Value, m.Upper.Value }) { Dashed = true, Thickness = 1.0 });
            }

            XYChart chart = new XYChart();
            chart.SetData(series, "Replication #", false);
            root.Children.Add(Widgets.Panel(
                Fmt.Label(metric) + " across replications",
                "Each replication with the mean and confidence-interval bounds",
                chart));
        }

        return root;
    }

    private static double ValueOf(ReplicationRun run, string metric)
    {
        switch (metric)
        {
            case "average_waiting_time":
                return run.AverageWaitingTime ?? 0.0;
            case "average_system_time":
                return run.AverageSystemTime ?? 0.0;
            case "average_number_in_queue":
                return run.AverageNumberInQueue ?? 0.0;
            case "average_number_in_system":
                return run.AverageNumberInSystem ?? 0.0;
            case "utilization":
                return run.Utilization ?? 0.0;
            default:
                return run.Throughput ?? 0.0;
        }
    }
}
