using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using QueueLab.Desktop.Ui;

namespace QueueLab.Desktop.Pages;

/// <summary>A scrollable page with a centred content column.</summary>
internal abstract class PageBase : UserControl
{
    protected PageBase()
    {
        Body = new StackPanel();

        Border column = new Border
        {
            Padding = new Thickness(32, 28),
            MaxWidth = 1320,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = Body,
        };

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = column,
        };
    }

    /// <summary>The vertical stack that page content is added to.</summary>
    protected StackPanel Body { get; }

    /// <summary>
    /// A form column on the left and a results column on the right (the layout used by most pages).
    /// </summary>
    protected static Grid TwoColumns(Control left, Control right, double leftWidth = 340)
    {
        Grid grid = new Grid { ColumnDefinitions = new ColumnDefinitions(leftWidth.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",*") };
        left.VerticalAlignment = VerticalAlignment.Top;
        left.Margin = new Thickness(0, 0, 20, 0);
        right.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    /// <summary>Replaces what a host control shows.</summary>
    protected static void Show(ContentControl host, Control? content)
    {
        host.Content = content;
    }

    protected static Control FormStack(params Control[] items)
    {
        StackPanel stack = new StackPanel { Spacing = 14 };
        foreach (Control item in items)
        {
            stack.Children.Add(item);
        }

        return stack;
    }
}
