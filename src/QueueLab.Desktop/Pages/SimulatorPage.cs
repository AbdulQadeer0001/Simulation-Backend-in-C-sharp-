using System.Numerics;
using Avalonia.Controls;
using Avalonia.Layout;
using QueueLab.Core.Common;
using QueueLab.Core.DataProcessing;
using QueueLab.Core.Models;
using QueueLab.Core.Services;
using QueueLab.Core.Simulation;
using QueueLab.Desktop.Ui;

namespace QueueLab.Desktop.Pages;

/// <summary>Single-server and multi-server simulation (the same page in two modes, like the web UI).</summary>
internal sealed class SimulatorPage : PageBase
{
    private readonly bool _multi;
    private readonly TextBox _lambda;
    private readonly TextBox _mu;
    private readonly TextBox _servers;
    private readonly TextBox _customers;
    private readonly TextBox _seed;
    private readonly ComboBox _arrival;
    private readonly ComboBox _service;
    private readonly ActionButton _run;
    private readonly ActionButton _nadra;
    private readonly ContentControl _results = new ContentControl();

    public SimulatorPage(bool multi)
    {
        _multi = multi;

        Body.Children.Add(Widgets.PageHeader(
            multi ? "M/M/c · G/G/c" : "M/M/1 · G/G/1",
            multi ? "Multi-Server Simulation" : "Single-Server Simulation",
            multi
                ? "Simulate a queue served by several parallel servers with a shared FCFS line."
                : "Simulate a single FCFS server with configurable arrival and service processes."));

        _lambda = Widgets.Input("0.5");
        _mu = Widgets.Input(multi ? "0.3" : "1");
        _servers = Widgets.Input("3");
        _customers = Widgets.Input("100");
        _seed = Widgets.Input("42");
        _arrival = Widgets.Combo(new[] { "Exponential", "Deterministic" });
        _service = Widgets.Combo(new[] { "Exponential", "Deterministic" });

        _run = new ActionButton("Run simulation");
        _run.HorizontalAlignment = HorizontalAlignment.Stretch;
        _run.Clicked += (sender, e) => RunGenerated();

        _nadra = new ActionButton("Replay NADRA observed data", false);
        _nadra.HorizontalAlignment = HorizontalAlignment.Stretch;
        _nadra.Clicked += (sender, e) => RunNadra();

        List<Control> fields = new List<Control>
        {
            Widgets.Field("Arrival rate λ (per min)", _lambda),
            Widgets.Field(multi ? "Service rate μ per server" : "Service rate μ (per min)", _mu),
        };
        if (multi)
        {
            fields.Add(Widgets.Field("Number of servers c", _servers));
        }

        fields.Add(Widgets.Field("Customers", _customers));
        fields.Add(Widgets.Field("Arrival distribution", _arrival));
        fields.Add(Widgets.Field("Service distribution", _service));
        fields.Add(Widgets.Field("Random seed", _seed, "Leave empty for random."));
        fields.Add(_run);
        fields.Add(_nadra);

        Control form = Widgets.Panel("Configuration", null, FormStack(fields.ToArray()));

        _results.Content = Widgets.EmptyState(
            "No simulation yet",
            "Set the parameters on the left and press “Run simulation” to see waiting times, queue lengths and utilization.");

        Body.Children.Add(TwoColumns(form, _results));
    }

    private void SetBusy(bool busy)
    {
        _run.Enabled = !busy;
        _nadra.Enabled = !busy;
        _run.Text = busy ? "Running…" : "Run simulation";
    }

    private async void RunGenerated()
    {
        SetBusy(true);
        try
        {
            double lambda = Inputs.PositiveNumber(_lambda.Text, "Arrival rate");
            double mu = Inputs.PositiveNumber(_mu.Text, "Service rate");
            int customerCount = Inputs.PositiveInt(_customers.Text, "Customers", Inputs.MaxCustomers);
            int servers = _multi ? Inputs.PositiveInt(_servers.Text, "Servers", Inputs.MaxServers) : 1;
            BigInteger? seed = Inputs.OptionalSeed(_seed.Text);
            bool exponentialArrivals = _arrival.SelectedIndex == 0;
            bool exponentialService = _service.SelectedIndex == 0;
            string type = _multi ? "multi_server" : "single_server";

            SimulationResult result = await Task.Run(() =>
            {
                List<DistributionGenerator.GeneratedCustomer> generated = DistributionGenerator.Generate(
                    customerCount, lambda, mu, exponentialArrivals, exponentialService, seed);

                List<ISimCustomer> customers = new List<ISimCustomer>(generated.Count);
                foreach (DistributionGenerator.GeneratedCustomer item in generated)
                {
                    customers.Add(item.ToSimulationCustomer());
                }

                return SimulationService.Run(type, customers, servers);
            });

            Show(_results, ResultsView.Build(result, this));
        }
        catch (Exception error)
        {
            Show(_results, Widgets.ErrorBox(error.Message));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void RunNadra()
    {
        SetBusy(true);
        try
        {
            int servers = _multi ? Inputs.PositiveInt(_servers.Text, "Servers", Inputs.MaxServers) : 1;
            bool multi = _multi;

            SimulationResult result = await Task.Run(() =>
            {
                List<Customer> customers = NadraDataLoader.LoadFile(Inputs.NadraFile);
                return multi
                    ? SimulationService.RunMultiServer(customers, servers)
                    : SimulationService.RunSingleServer(customers);
            });

            Show(_results, ResultsView.Build(result, this));
        }
        catch (Exception error)
        {
            Show(_results, Widgets.ErrorBox(error.Message));
        }
        finally
        {
            SetBusy(false);
        }
    }
}
