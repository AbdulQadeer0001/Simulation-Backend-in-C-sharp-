using Avalonia.Media;

namespace QueueLab.Desktop;

/// <summary>Colour palette of the QueueLab web UI (converted from its OKLCH design tokens).</summary>
internal static class AppColors
{
    public static readonly Color BackgroundColor = Color.Parse("#F9F6F2");
    public static readonly SolidColorBrush Background = new SolidColorBrush(BackgroundColor);
    public static readonly Color ForegroundColor = Color.Parse("#0F171F");
    public static readonly SolidColorBrush Foreground = new SolidColorBrush(ForegroundColor);
    public static readonly Color CardColor = Color.Parse("#FFFFFF");
    public static readonly SolidColorBrush Card = new SolidColorBrush(CardColor);
    public static readonly Color PrimaryColor = Color.Parse("#007C7C");
    public static readonly SolidColorBrush Primary = new SolidColorBrush(PrimaryColor);
    public static readonly Color PrimaryForegroundColor = Color.Parse("#F8FDFD");
    public static readonly SolidColorBrush PrimaryForeground = new SolidColorBrush(PrimaryForegroundColor);
    public static readonly Color SecondaryColor = Color.Parse("#EFEBE2");
    public static readonly SolidColorBrush Secondary = new SolidColorBrush(SecondaryColor);
    public static readonly Color MutedColor = Color.Parse("#F1EEE9");
    public static readonly SolidColorBrush Muted = new SolidColorBrush(MutedColor);
    public static readonly Color MutedForegroundColor = Color.Parse("#5B646F");
    public static readonly SolidColorBrush MutedForeground = new SolidColorBrush(MutedForegroundColor);
    public static readonly Color AccentColor = Color.Parse("#FEE3C5");
    public static readonly SolidColorBrush Accent = new SolidColorBrush(AccentColor);
    public static readonly Color AccentForegroundColor = Color.Parse("#44250C");
    public static readonly SolidColorBrush AccentForeground = new SolidColorBrush(AccentForegroundColor);
    public static readonly Color DestructiveColor = Color.Parse("#DA2B2A");
    public static readonly SolidColorBrush Destructive = new SolidColorBrush(DestructiveColor);
    public static readonly Color SuccessColor = Color.Parse("#319751");
    public static readonly SolidColorBrush Success = new SolidColorBrush(SuccessColor);
    public static readonly Color WarningColor = Color.Parse("#E99B2A");
    public static readonly SolidColorBrush Warning = new SolidColorBrush(WarningColor);
    public static readonly Color BorderColor = Color.Parse("#E1DED7");
    public static readonly SolidColorBrush Border = new SolidColorBrush(BorderColor);
    public static readonly Color InputColor = Color.Parse("#DBD7CF");
    public static readonly SolidColorBrush Input = new SolidColorBrush(InputColor);
    public static readonly Color Chart1Color = Color.Parse("#007C7C");
    public static readonly SolidColorBrush Chart1 = new SolidColorBrush(Chart1Color);
    public static readonly Color Chart2Color = Color.Parse("#E78B30");
    public static readonly SolidColorBrush Chart2 = new SolidColorBrush(Chart2Color);
    public static readonly Color Chart3Color = Color.Parse("#3A5582");
    public static readonly SolidColorBrush Chart3 = new SolidColorBrush(Chart3Color);
    public static readonly Color Chart4Color = Color.Parse("#D15C56");
    public static readonly SolidColorBrush Chart4 = new SolidColorBrush(Chart4Color);
    public static readonly Color Chart5Color = Color.Parse("#53A367");
    public static readonly SolidColorBrush Chart5 = new SolidColorBrush(Chart5Color);
    public static readonly Color SidebarColor = Color.Parse("#0E1A23");
    public static readonly SolidColorBrush Sidebar = new SolidColorBrush(SidebarColor);
    public static readonly Color SidebarForegroundColor = Color.Parse("#CCD2D7");
    public static readonly SolidColorBrush SidebarForeground = new SolidColorBrush(SidebarForegroundColor);
    public static readonly Color SidebarPrimaryColor = Color.Parse("#3ED0C8");
    public static readonly SolidColorBrush SidebarPrimary = new SolidColorBrush(SidebarPrimaryColor);
    public static readonly Color SidebarPrimaryForegroundColor = Color.Parse("#0A1723");
    public static readonly SolidColorBrush SidebarPrimaryForeground = new SolidColorBrush(SidebarPrimaryForegroundColor);
    public static readonly Color SidebarAccentColor = Color.Parse("#1C2B37");
    public static readonly SolidColorBrush SidebarAccent = new SolidColorBrush(SidebarAccentColor);
    public static readonly Color SidebarBorderColor = Color.Parse("#232F3A");
    public static readonly SolidColorBrush SidebarBorder = new SolidColorBrush(SidebarBorderColor);
    public static readonly Color HeroFromColor = Color.Parse("#0E212E");
    public static readonly SolidColorBrush HeroFrom = new SolidColorBrush(HeroFromColor);
    public static readonly Color HeroToColor = Color.Parse("#003B41");
    public static readonly SolidColorBrush HeroTo = new SolidColorBrush(HeroToColor);

    public static readonly Color[] ChartColors = { Chart1Color, Chart2Color, Chart3Color, Chart4Color, Chart5Color };
}
