using Avalonia.Controls;
using Avalonia.Layout;
using QueueLab.Core.Queueing;
using QueueLab.Desktop.Ui;

namespace QueueLab.Desktop.Pages;

/// <summary>Analytical M/M/1 and M/M/c calculators.</summary>
internal sealed class TheoryPage : PageBase
{
    private readonly ComboBox _model;
    private readonly TextBox _lambda;
    private readonly TextBox _mu;
    private readonly TextBox _servers;
    private readonly StackPanel _serversField;
    private readonly Border _muField;
    private readonly ActionButton _compute;
    private readonly ContentControl _results = new ContentControl();
    private readonly TextBlock _muLabel;

    public TheoryPage()
    {
        Body.Children.Add(Widgets.PageHeader(
            "Analytical models",
            "Queueing Theory",
            "Steady-state formulas for Poisson arrivals and exponential service — use them to validate your simulations."));

        _model = Widgets.Combo(new[] { "M/M/1 (single server)", "M/M/c (multi-server)" });
        _lambda = Widgets.Input("0.8");
        _mu = Widgets.Input("1");
        _servers = Widgets.Input("2");

        _muLabel = Widgets.Text("Service rate μ", 12, Avalonia.Media.FontWeight.Medium);
        StackPanel muStack = new StackPanel { Spacing = 5 };
        muStack.Children.Add(_muLabel);
        muStack.Children.Add(_mu);
        _muField = new Border { Child = muStack };

        _serversField = Widgets.Field("Servers c", _servers);
        _serversField.IsVisible = false;

        _model.SelectionChanged += (sender, e) =>
        {
            bool multi = _model.SelectedIndex == 1;
            _serversField.IsVisible = multi;
            _muLabel.Text = multi ? "Service rate μ per server" : "Service rate μ";
        };

        _compute = new ActionButton("Compute");
        _compute.HorizontalAlignment = HorizontalAlignment.Stretch;
        _compute.Clicked += (sender, e) => Compute();

        Control form = Widgets.Panel(
            "Model",
            null,
            FormStack(
                Widgets.Field("Model", _model),
                Widgets.Field("Arrival rate λ", _lambda),
                _muField,
                _serversField,
                _compute));

        _results.Content = Widgets.EmptyState("Nothing computed yet", "Choose a model, enter λ and μ, and press “Compute”.");
        Body.Children.Add(TwoColumns(form, _results));
    }

    private void Compute()
    {
        try
        {
            double lambda = Inputs.PositiveNumber(_lambda.Text, "Arrival rate");
            double mu = Inputs.PositiveNumber(_mu.Text, "Service rate");

            QueueModelResult result;
            if (_model.SelectedIndex == 1)
            {
                int servers = Inputs.PositiveInt(_servers.Text, "Servers", 100000);
                result = new MMCQueue(lambda, mu, servers).Calculate();
            }
            else
            {
                result = new MM1Queue(lambda, mu).Calculate();
            }

            Show(_results, Build(result));
        }
        catch (Exception error)
        {
            Show(_results, Widgets.ErrorBox(error.Message));
        }
    }

    private static Control Build(QueueModelResult result)
    {
        StackPanel root = new StackPanel { Spacing = 16 };
        Dictionary<string, double> m = result.Metrics;

        List<Control> stats = new List<Control>
        {
            Widgets.Stat("Utilization ρ", Fmt.Pct(result.Input["utilization"]), null, null, true),
            Widgets.Stat("Wq", Fmt.Num(m["average_waiting_time"], 4), "min"),
            Widgets.Stat("W", Fmt.Num(m["average_system_time"], 4), "min"),
            Widgets.Stat("Lq", Fmt.Num(m["average_number_in_queue"], 4)),
            Widgets.Stat("L", Fmt.Num(m["average_number_in_system"], 4)),
            Widgets.Stat("P(empty) P₀", Fmt.Num(m["probability_empty"], 4)),
        };

        double waitProbability;
        if (m.TryGetValue("probability_wait", out waitProbability))
        {
            stats.Add(Widgets.Stat("P(wait)", Fmt.Pct(waitProbability)));
        }

        stats.Add(Widgets.Stat("Throughput", Fmt.Num(m["throughput"], 4), "/min"));
        root.Children.Add(Widgets.Tiles(3, stats));

        List<string[]> rows = new List<string[]>();
        foreach (KeyValuePair<string, object> pair in result.Assumptions)
        {
            string value = pair.Value is bool flag ? (flag ? "Yes" : "No") : Convert.ToString(pair.Value) ?? string.Empty;
            rows.Add(new[] { Fmt.Label(pair.Key), value });
        }

        root.Children.Add(Widgets.Panel(
            result.Model + " assumptions",
            null,
            Widgets.Table(new[] { "Assumption", "Value" }, rows, 50, 300)));

        return root;
    }
}
