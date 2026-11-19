using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace QueueLab.Desktop.Ui;

/// <summary>A flat button in the QueueLab style (filled "primary" or outlined).</summary>
internal sealed class ActionButton : Border
{
    private readonly TextBlock _label;
    private readonly bool _primary;
    private bool _hover;

    public event EventHandler? Clicked;

    public ActionButton(string text, bool primary = true)
    {
        _primary = primary;
        _label = new TextBlock
        {
            Text = text,
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Padding = new Thickness(14, 9);
        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
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
        PointerReleased += OnPointerReleased;
    }

    public string Text
    {
        get { return _label.Text ?? string.Empty; }
        set { _label.Text = value; }
    }

    /// <summary>Enables or disables the button (disabled buttons are dimmed and ignore clicks).</summary>
    public bool Enabled
    {
        get { return IsEnabled; }
        set
        {
            IsEnabled = value;
            Opacity = value ? 1.0 : 0.55;
        }
    }

    private void Apply()
    {
        if (_primary)
        {
            Background = _hover ? new SolidColorBrush(AppColors.PrimaryColor, 0.88) : AppColors.Primary;
            BorderBrush = AppColors.Primary;
            _label.Foreground = AppColors.PrimaryForeground;
        }
        else
        {
            Background = _hover ? AppColors.Secondary : AppColors.Card;
            BorderBrush = AppColors.Input;
            _label.Foreground = AppColors.Foreground;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsEnabled || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        Point position = e.GetPosition(this);
        if (new Rect(Bounds.Size).Contains(position))
        {
            Clicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
