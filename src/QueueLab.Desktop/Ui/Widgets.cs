using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace QueueLab.Desktop.Ui;

/// <summary>Small factory methods for the building blocks that the pages share.</summary>
internal static class Widgets
{
    public static TextBlock Text(
        string text,
        double size = 13,
        FontWeight weight = FontWeight.Normal,
        IBrush? brush = null,
        bool wrap = false)
    {
        TextBlock block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            Foreground = brush ?? AppColors.Foreground,
        };

        if (wrap)
        {
            block.TextWrapping = TextWrapping.Wrap;
        }

        return block;
    }

    public static Control PageHeader(string eyebrow, string title, string description)
    {
        StackPanel stack = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 20) };
        stack.Children.Add(Text(eyebrow.ToUpperInvariant(), 11, FontWeight.SemiBold, AppColors.Primary));
        stack.Children.Add(Text(title, 26, FontWeight.Bold));
        stack.Children.Add(Text(description, 13, FontWeight.Normal, AppColors.MutedForeground, true));
        return stack;
    }

    public static Border Card(Control content)
    {
        return new Border
        {
            Background = AppColors.Card,
            BorderBrush = AppColors.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = content,
        };
    }

    /// <summary>A titled card: header line (title, optional description, optional action) above the body.</summary>
    public static Border Panel(string title, string? description, Control body, Control? action = null)
    {
        Grid header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(20, 14) };

        StackPanel titles = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(Text(title, 14, FontWeight.SemiBold));
        if (!string.IsNullOrEmpty(description))
        {
            titles.Children.Add(Text(description, 12, FontWeight.Normal, AppColors.MutedForeground, true));
        }

        header.Children.Add(titles);

        if (action != null)
        {
            action.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(action, 1);
            header.Children.Add(action);
        }

        Border headerBorder = new Border
        {
            BorderBrush = AppColors.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = header,
        };

        body.Margin = new Thickness(20);

        StackPanel stack = new StackPanel();
        stack.Children.Add(headerBorder);
        stack.Children.Add(body);
        return Card(stack);
    }

    /// <summary>A key figure: label, big value, optional unit and hint.</summary>
    public static Border Stat(string label, string value, string? unit = null, string? hint = null, bool accent = false)
    {
        StackPanel valueRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 8, 0, 0) };
        valueRow.Children.Add(Text(value, 24, FontWeight.SemiBold));
        if (!string.IsNullOrEmpty(unit))
        {
            TextBlock unitText = Text(unit, 12, FontWeight.Normal, AppColors.MutedForeground);
            unitText.VerticalAlignment = VerticalAlignment.Bottom;
            unitText.Margin = new Thickness(0, 0, 0, 3);
            valueRow.Children.Add(unitText);
        }

        StackPanel stack = new StackPanel { Margin = new Thickness(16) };
        stack.Children.Add(Text(label, 12, FontWeight.Medium, AppColors.MutedForeground));
        stack.Children.Add(valueRow);
        if (!string.IsNullOrEmpty(hint))
        {
            TextBlock hintText = Text(hint, 11, FontWeight.Normal, AppColors.MutedForeground);
            hintText.Margin = new Thickness(0, 4, 0, 0);
            stack.Children.Add(hintText);
        }

        Border card = Card(stack);
        if (accent)
        {
            card.BorderBrush = new SolidColorBrush(AppColors.PrimaryColor, 0.45);
        }

        return card;
    }

    /// <summary>Lays controls out in a grid with equal-width columns.</summary>
    public static Grid Tiles(int columns, IReadOnlyList<Control> items, double gap = 12)
    {
        Grid grid = new Grid();
        for (int c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        }

        int rows = (items.Count + columns - 1) / columns;
        for (int r = 0; r < rows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (int i = 0; i < items.Count; i++)
        {
            Control item = items[i];
            int column = i % columns;
            int row = i / columns;
            item.Margin = new Thickness(column == 0 ? 0 : gap / 2, row == 0 ? 0 : gap / 2, column == columns - 1 ? 0 : gap / 2, row == rows - 1 ? 0 : gap / 2);
            Grid.SetColumn(item, column);
            Grid.SetRow(item, row);
            grid.Children.Add(item);
        }

        return grid;
    }

    public static TextBox Input(string text)
    {
        return new TextBox
        {
            Text = text,
            FontSize = 13,
            Background = AppColors.Card,
            Foreground = AppColors.Foreground,
            BorderBrush = AppColors.Input,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 7),
            MinHeight = 34,
        };
    }

    public static ComboBox Combo(IReadOnlyList<string> items, int selectedIndex = 0)
    {
        return new ComboBox
        {
            ItemsSource = items.ToList(),
            SelectedIndex = selectedIndex,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 13,
            Background = AppColors.Card,
            BorderBrush = AppColors.Input,
            CornerRadius = new CornerRadius(6),
            MinHeight = 34,
        };
    }

    /// <summary>A label above an input control, with an optional hint underneath.</summary>
    public static StackPanel Field(string label, Control input, string? hint = null)
    {
        StackPanel stack = new StackPanel { Spacing = 5 };
        stack.Children.Add(Text(label, 12, FontWeight.Medium));
        stack.Children.Add(input);
        if (!string.IsNullOrEmpty(hint))
        {
            stack.Children.Add(Text(hint, 11, FontWeight.Normal, AppColors.MutedForeground));
        }

        return stack;
    }

    public static Control ErrorBox(string message)
    {
        return new Border
        {
            Background = new SolidColorBrush(AppColors.DestructiveColor, 0.08),
            BorderBrush = new SolidColorBrush(AppColors.DestructiveColor, 0.4),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12),
            Child = Text(message, 13, FontWeight.Medium, AppColors.Destructive, true),
        };
    }

    public static Control EmptyState(string title, string description)
    {
        StackPanel stack = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        TextBlock heading = Text(title, 15, FontWeight.SemiBold);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        TextBlock body = Text(description, 13, FontWeight.Normal, AppColors.MutedForeground, true);
        body.HorizontalAlignment = HorizontalAlignment.Center;
        body.TextAlignment = TextAlignment.Center;
        body.MaxWidth = 420;
        stack.Children.Add(heading);
        stack.Children.Add(body);

        return new Border
        {
            BorderBrush = AppColors.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(AppColors.MutedColor, 0.5),
            MinHeight = 220,
            Padding = new Thickness(24),
            Child = stack,
        };
    }

    /// <summary>A simple scrollable table. Only the first <paramref name="maxRows"/> rows are shown.</summary>
    public static Control Table(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, int maxRows = 500, double maxHeight = 380)
    {
        Grid grid = new Grid();
        for (int c = 0; c < headers.Count; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star) { MinWidth = 70 });
        }

        int shown = Math.Min(rows.Count, maxRows);
        for (int r = 0; r <= shown; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        // Header row.
        Border headerBackground = new Border { Background = AppColors.Muted };
        Grid.SetRow(headerBackground, 0);
        Grid.SetColumnSpan(headerBackground, headers.Count);
        grid.Children.Add(headerBackground);

        for (int c = 0; c < headers.Count; c++)
        {
            TextBlock cell = Text(headers[c], 12, FontWeight.SemiBold, AppColors.MutedForeground);
            cell.Margin = new Thickness(10, 8);
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, c);
            grid.Children.Add(cell);
        }

        for (int r = 0; r < shown; r++)
        {
            Border line = new Border { BorderBrush = AppColors.Border, BorderThickness = new Thickness(0, 0, 0, 1) };
            Grid.SetRow(line, r + 1);
            Grid.SetColumnSpan(line, headers.Count);
            grid.Children.Add(line);

            string[] values = rows[r];
            for (int c = 0; c < headers.Count && c < values.Length; c++)
            {
                TextBlock cell = Text(values[c], 12);
                cell.Margin = new Thickness(10, 6);
                Grid.SetRow(cell, r + 1);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        ScrollViewer scroll = new ScrollViewer
        {
            MaxHeight = maxHeight,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = grid,
        };

        StackPanel stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new Border
        {
            BorderBrush = AppColors.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Child = scroll,
        });

        if (rows.Count > shown)
        {
            stack.Children.Add(Text(
                "Showing the first " + shown + " of " + rows.Count + " rows. Export to get all of them.",
                11,
                FontWeight.Normal,
                AppColors.MutedForeground));
        }

        return stack;
    }

    /// <summary>A horizontal row of buttons (used for the export actions).</summary>
    public static StackPanel ButtonRow(params Control[] buttons)
    {
        StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (Control button in buttons)
        {
            row.Children.Add(button);
        }

        return row;
    }
}
