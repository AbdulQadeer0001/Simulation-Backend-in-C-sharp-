using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace QueueLab.Desktop;

/// <summary>One entry of the sidebar navigation.</summary>
internal sealed class NavItem : Border
{
    private readonly TextBlock _label;
    private bool _selected;
    private bool _hover;

    public event EventHandler? Clicked;

    public NavItem(string text)
    {
        _label = new TextBlock
        {
            Text = text,
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };

        Padding = new Thickness(14, 10);
        CornerRadius = new CornerRadius(6);
        Margin = new Thickness(0, 1);
        Cursor = new Cursor(StandardCursorType.Hand);
        Child = _label;
        Apply();

        PointerEntered += (sender, e) =>
        {
            _hover = true;
            Apply();
        };
        PointerExited += (sender, e) =>
        {
            _hover = false;
            Apply();
        };
        PointerReleased += (sender, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                Clicked?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public bool Selected
    {
        get { return _selected; }
        set
        {
            _selected = value;
            Apply();
        }
    }

    private void Apply()
    {
        if (_selected)
        {
            Background = AppColors.SidebarAccent;
            _label.Foreground = AppColors.SidebarPrimary;
        }
        else
        {
            Background = _hover ? new SolidColorBrush(AppColors.SidebarAccentColor, 0.6) : Brushes.Transparent;
            _label.Foreground = AppColors.SidebarForeground;
        }
    }
}
