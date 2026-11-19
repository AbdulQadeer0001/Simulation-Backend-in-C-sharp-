using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using QueueLab.Core.DataProcessing;
using QueueLab.Core.Models;
using QueueLab.Core.Services;
using QueueLab.Core.Statistics;
using QueueLab.Desktop.Ui;

namespace QueueLab.Desktop.Pages;

/// <summary>Landing page: quick links plus a summary of the observed NADRA data.</summary>
internal sealed class DashboardPage : PageBase
{
    private readonly Action<string> _navigate;
    private readonly ContentControl _observed = new ContentControl();

    public DashboardPage(Action<string> navigate)
    {
        _navigate = navigate;

        // Hero banner.
        StackPanel heroText = new StackPanel { Spacing = 8 };
        heroText.Children.Add(Widgets.Text("QUEUE SIMULATION", 11, FontWeight.SemiBold, new SolidColorBrush(AppColors.SidebarPrimaryColor)));
        heroText.Children.Add(Widgets.Text("Run, analyze and compare queueing systems", 26, FontWeight.Bold, Brushes.White, true));
        heroText.Children.Add(Widgets.Text(
            "Simulate single- and multi-server queues, replay observed NADRA data, build confidence intervals and compare what-if scenarios.",
            13,
            FontWeight.Normal,
            new SolidColorBrush(AppColors.SidebarForegroundColor),
            true));

        LinearGradientBrush gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        };
        gradient.GradientStops.Add(new GradientStop { Color = AppColors.HeroFromColor, Offset = 0 });
        gradient.GradientStops.Add(new GradientStop { Color = AppColors.HeroToColor, Offset = 1 });

        Body.Children.Add(new Border
        {
            Background = gradient,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(32, 28),
            Margin = new Thickness(0, 0, 0, 20),
            Child = heroText,
        });

        // Navigation cards.
        List<Control> cards = new List<Control>
        {
            NavCard("single", "Single-Server", "One FCFS server, custom λ and μ"),
            NavCard("multi", "Multi-Server", "c parallel servers, shared line"),
            NavCard("datasets", "Datasets", "Upload CSV / Excel arrivals"),
            NavCard("compare", "Compare", "Side-by-side scenarios"),
            NavCard("replications", "Replications", "Confidence intervals"),
        };
        Body.Children.Add(Widgets.Tiles(5, cards));

        _observed.Margin = new Thickness(0, 20, 0, 0);
        _observed.Content = Widgets.EmptyState("Loading observed data…", "Reading the built-in NADRA dataset.");
        Body.Children.Add(_observed);

        LoadObserved();
    }

    private Control NavCard(string key, string title, string description)
    {
        StackPanel stack = new StackPanel { Spacing = 4, Margin = new Thickness(16) };
        stack.Children.Add(Widgets.Text(title, 14, FontWeight.SemiBold, AppColors.Primary));
        stack.Children.Add(Widgets.Text(description, 12, FontWeight.Normal, AppColors.MutedForeground, true));

        Border card = Widgets.Card(stack);
        card.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        card.PointerEntered += (sender, e) => card.BorderBrush = new SolidColorBrush(AppColors.PrimaryColor, 0.6);
        card.PointerExited += (sender, e) => card.BorderBrush = AppColors.Border;
        card.PointerReleased += (sender, e) => _navigate(key);
        return card;
    }

    private static double? Dig(Dictionary<string, object?> summary, string group, string field)
    {
        object? inner;
        if (summary.TryGetValue(group, out inner))
        {
            Dictionary<string, object?>? dict = inner as Dictionary<string, object?>;
            object? value;
            if (dict != null && dict.TryGetValue(field, out value) && value is double d)
            {
                return d;
            }
        }

        return null;
    }

    private async void LoadObserved()
    {
        try
        {
            (Dictionary<string, object?> Summary, SimulationResult Result) loaded = await Task.Run(() =>
            {
                List<Customer> customers = NadraDataLoader.LoadFile(Inputs.NadraFile);
                Dictionary<string, object?> summary = DescriptiveStatistics.Summary(customers);
                SimulationResult result = SimulationService.RunSingleServer(customers);
                return (summary, result);
            });

            Dictionary<string, object?> s = loaded.Summary;
            SimulationResult sim = loaded.Result;

            StackPanel root = new StackPanel { Spacing = 16 };

            List<Control> observed = new List<Control>
            {
                Widgets.Stat("Customers observed", Fmt.Num(Convert.ToDouble(s["total_customers"]), 0)),
                Widgets.Stat("Completion rate", Fmt.Num(Convert.ToDouble(s["completion_rate"]), 1) + "%"),
                Widgets.Stat("Reneging rate", Fmt.Num(Convert.ToDouble(s["reneging_rate"]), 1) + "%"),
                Widgets.Stat("Avg main wait", Fmt.Num(Dig(s, "main_wait", "average"), 2), "min"),
            };
            root.Children.Add(Widgets.Tiles(4, observed));

            List<Control> simulated = new List<Control>
            {
                Widgets.Stat("Simulated Wq (1 server)", Fmt.Num(sim.AverageWaitingTime, 2), "min", null, true),
                Widgets.Stat("Simulated utilization", Fmt.Pct(sim.Utilization)),
                Widgets.Stat("Arrival rate λ", Fmt.Num(sim.ArrivalRate, 3), "/min"),
                Widgets.Stat("Service rate μ", Fmt.Num(sim.ServiceRate, 3), "/min"),
            };
            root.Children.Add(Widgets.Tiles(4, simulated));

            int shown = Math.Min(60, sim.CustomerResults.Count);
            List<string> labels = new List<string>();
            List<double> waits = new List<double>();
            for (int i = 0; i < shown; i++)
            {
                labels.Add((i + 1).ToString());
                waits.Add(Math.Round(sim.CustomerResults[i].WaitingTime, 2));
            }

            BarChart chart = new BarChart();
            chart.SetData(labels, waits, null, null, AppColors.Chart1Color, "F1");
            root.Children.Add(Widgets.Panel(
                "Waiting time — first 60 NADRA customers",
                "Single-server replay of observed arrivals",
                chart));

            Show(_observed, root);
        }
        catch (Exception error)
        {
            Show(_observed, Widgets.ErrorBox("Could not load the built-in dataset: " + error.Message));
        }
    }
}
