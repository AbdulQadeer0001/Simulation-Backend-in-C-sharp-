using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using QueueLab.Desktop.Pages;
using QueueLab.Desktop.Ui;

namespace QueueLab.Desktop;

/// <summary>The application window: sidebar navigation on the left, the selected page on the right.</summary>
internal sealed class MainWindow : Window
{
    private readonly ContentControl _host = new ContentControl();
    private readonly Dictionary<string, Control> _pages = new Dictionary<string, Control>();
    private readonly Dictionary<string, NavItem> _items = new Dictionary<string, NavItem>();
    private readonly Dictionary<string, Func<Control>> _factories;

    public MainWindow()
    {
        Title = "QueueLab";
        Width = 1360;
        Height = 860;
        MinWidth = 1040;
        MinHeight = 640;
        Background = AppColors.Background;

        _factories = new Dictionary<string, Func<Control>>
        {
            ["dashboard"] = () => new DashboardPage(Navigate),
            ["single"] = () => new SimulatorPage(false),
            ["multi"] = () => new SimulatorPage(true),
            ["datasets"] = () => new DatasetsPage(),
            ["compare"] = () => new ComparePage(),
            ["replications"] = () => new ReplicationsPage(),
            ["theory"] = () => new TheoryPage(),
        };

        Grid layout = new Grid { ColumnDefinitions = new ColumnDefinitions("250,*") };
        layout.Children.Add(BuildSidebar());
        Grid.SetColumn(_host, 1);
        layout.Children.Add(_host);
        Content = layout;

        Navigate("dashboard");
    }

    private Control BuildSidebar()
    {
        StackPanel nav = new StackPanel { Margin = new Thickness(12, 8) };
        AddItem(nav, "dashboard", "Dashboard");
        AddItem(nav, "single", "Single-Server");
        AddItem(nav, "multi", "Multi-Server");
        AddItem(nav, "datasets", "Datasets");
        AddItem(nav, "compare", "Compare");
        AddItem(nav, "replications", "Replications");
        AddItem(nav, "theory", "Queueing Theory");

        StackPanel brand = new StackPanel { Spacing = 2, Margin = new Thickness(20, 22, 20, 14) };
        brand.Children.Add(Widgets.Text("QueueLab", 20, FontWeight.Bold, Brushes.White));
        brand.Children.Add(Widgets.Text("Queue simulation studio", 11, FontWeight.Normal, new SolidColorBrush(AppColors.SidebarForegroundColor, 0.7)));

        Border divider = new Border { Height = 1, Background = AppColors.SidebarBorder, Margin = new Thickness(0, 0, 0, 4) };

        StackPanel footer = new StackPanel { Spacing = 2, Margin = new Thickness(20, 14) };
        footer.Children.Add(Widgets.Text("Engine", 11, FontWeight.Medium, new SolidColorBrush(AppColors.SidebarForegroundColor, 0.7)));
        footer.Children.Add(Widgets.Text("Built-in C# · offline", 12, FontWeight.Medium, new SolidColorBrush(AppColors.SidebarPrimaryColor)));

        DockPanel dock = new DockPanel();
        DockPanel.SetDock(brand, Dock.Top);
        DockPanel.SetDock(divider, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        dock.Children.Add(brand);
        dock.Children.Add(divider);
        dock.Children.Add(footer);
        dock.Children.Add(nav);

        return new Border
        {
            Background = AppColors.Sidebar,
            BorderBrush = AppColors.SidebarBorder,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = dock,
        };
    }

    private void AddItem(Panel parent, string key, string text)
    {
        NavItem item = new NavItem(text);
        item.Clicked += (sender, e) => Navigate(key);
        _items[key] = item;
        parent.Children.Add(item);
    }

    private void Navigate(string key)
    {
        Control? page;
        if (!_pages.TryGetValue(key, out page))
        {
            page = _factories[key]();
            _pages[key] = page;
        }

        _host.Content = page;

        foreach (KeyValuePair<string, NavItem> pair in _items)
        {
            pair.Value.Selected = pair.Key == key;
        }
    }
}
