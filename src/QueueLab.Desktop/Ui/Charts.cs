using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace QueueLab.Desktop.Ui;

/// <summary>Shared drawing code: plot area, axes, grid and text.</summary>
internal abstract class ChartBase : Control
{
    protected const double LeftMargin = 54;
    protected const double RightMargin = 14;
    protected const double TopMargin = 12;
    protected const double BottomMargin = 30;
    protected const double LegendHeight = 20;

    protected static readonly Typeface Face = new Typeface(FontFamily.Default);

    protected ChartBase()
    {
        Height = 260;
        MinWidth = 220;
        ClipToBounds = true;
    }

    protected bool HasLegend { get; set; }

    protected Rect PlotArea
    {
        get
        {
            double top = TopMargin + (HasLegend ? LegendHeight : 0);
            double width = Math.Max(20, Bounds.Width - LeftMargin - RightMargin);
            double height = Math.Max(20, Bounds.Height - top - BottomMargin);
            return new Rect(LeftMargin, top, width, height);
        }
    }

    /// <summary>Draws text. anchor: -1 = x is the left edge, 0 = centred, 1 = x is the right edge.</summary>
    protected static void DrawLabel(DrawingContext context, string text, double x, double y, double size, IBrush brush, int anchor, bool centerVertically = false)
    {
        FormattedText formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Face,
            size,
            brush);

        double px = anchor < 0 ? x : (anchor == 0 ? x - formatted.Width / 2 : x - formatted.Width);
        double py = centerVertically ? y - formatted.Height / 2 : y;
        context.DrawText(formatted, new Point(px, py));
    }

    private static double NiceNumber(double range, bool round)
    {
        double exponent = Math.Floor(Math.Log10(range));
        double fraction = range / Math.Pow(10, exponent);
        double nice;

        if (round)
        {
            nice = fraction < 1.5 ? 1 : (fraction < 3 ? 2 : (fraction < 7 ? 5 : 10));
        }
        else
        {
            nice = fraction <= 1 ? 1 : (fraction <= 2 ? 2 : (fraction <= 5 ? 5 : 10));
        }

        return nice * Math.Pow(10, exponent);
    }

    /// <summary>Round tick values covering [min, max].</summary>
    protected static List<double> NiceTicks(double min, double max, int target, out double step)
    {
        if (!(max > min))
        {
            max = min + 1;
        }

        double range = NiceNumber(max - min, false);
        step = NiceNumber(range / Math.Max(2, target - 1), true);
        double start = Math.Floor(min / step) * step;
        double end = Math.Ceiling(max / step) * step;

        List<double> ticks = new List<double>();
        for (double v = start; v <= end + step * 0.5 && ticks.Count < 200; v += step)
        {
            ticks.Add(Math.Abs(v) < step * 1e-9 ? 0.0 : v);
        }

        if (ticks.Count < 2)
        {
            // Degenerate input (NaN or an absurd range): fall back to a simple 0..1 axis.
            ticks.Clear();
            ticks.Add(0.0);
            ticks.Add(1.0);
            step = 1.0;
        }

        return ticks;
    }

    protected static string TickText(double value, double step)
    {
        int decimals = step >= 1 ? 0 : Math.Min(6, (int)Math.Ceiling(-Math.Log10(step) - 1e-9));
        return value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    protected static Pen GridPen()
    {
        return new Pen(new SolidColorBrush(AppColors.BorderColor), 1, new DashStyle(new double[] { 3, 3 }, 0));
    }

    protected static void DrawLegend(DrawingContext context, IReadOnlyList<(string Name, Color Color)> entries, double left, double top)
    {
        double x = left;
        foreach ((string name, Color color) in entries)
        {
            context.DrawRectangle(new SolidColorBrush(color), null, new Rect(x, top + 4, 10, 10), 2, 2);
            FormattedText formatted = new FormattedText(
                name,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Face,
                11,
                AppColors.MutedForeground);
            context.DrawText(formatted, new Point(x + 14, top + 2));
            x += 14 + formatted.Width + 16;
        }
    }
}

/// <summary>One line, step or area series of an <see cref="XYChart"/>.</summary>
internal sealed class ChartSeries
{
    public ChartSeries(string name, Color color, double[] x, double[] y)
    {
        Name = name;
        Color = color;
        X = x;
        Y = y;
    }

    public string Name { get; }

    public Color Color { get; }

    public double[] X { get; }

    public double[] Y { get; }

    /// <summary>Hold each value until the next x (a staircase) instead of joining points diagonally.</summary>
    public bool Step { get; init; }

    public bool Fill { get; init; }

    public double FillOpacity { get; init; } = 0.2;

    public bool Dots { get; init; }

    public bool Dashed { get; init; }

    public double Thickness { get; init; } = 1.8;
}

/// <summary>Line / step / area chart over numeric x values.</summary>
internal sealed class XYChart : ChartBase
{
    private List<ChartSeries> _series = new List<ChartSeries>();
    private string _xLabel = string.Empty;
    private bool _startAtZero = true;

    public void SetData(IEnumerable<ChartSeries> series, string xLabel = "", bool startAtZero = true)
    {
        _series = series.ToList();
        _xLabel = xLabel;
        _startAtZero = startAtZero;
        HasLegend = _series.Count(s => !string.IsNullOrEmpty(s.Name)) > 1;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        Rect plot = PlotArea;
        List<ChartSeries> drawable = _series.Where(s => s.X.Length > 0 && s.X.Length == s.Y.Length).ToList();

        if (drawable.Count == 0)
        {
            DrawLabel(context, "No data", Bounds.Width / 2, Bounds.Height / 2, 12, AppColors.MutedForeground, 0, true);
            return;
        }

        double xMin = drawable.Min(s => s.X.Min());
        double xMax = drawable.Max(s => s.X.Max());
        double yMin = drawable.Min(s => s.Y.Min());
        double yMax = drawable.Max(s => s.Y.Max());

        if (_startAtZero && yMin > 0)
        {
            yMin = 0;
        }

        if (!(xMax > xMin))
        {
            xMax = xMin + 1;
        }

        if (!(yMax > yMin))
        {
            yMax = yMin + 1;
        }

        double yStep;
        List<double> yTicks = NiceTicks(yMin, yMax, 5, out yStep);
        yMin = yTicks[0];
        yMax = yTicks[yTicks.Count - 1];

        double xStep;
        List<double> xTicks = NiceTicks(xMin, xMax, 6, out xStep);

        Func<double, double> mapX = x => plot.Left + (x - xMin) / (xMax - xMin) * plot.Width;
        Func<double, double> mapY = y => plot.Bottom - (y - yMin) / (yMax - yMin) * plot.Height;

        Pen grid = GridPen();
        foreach (double tick in yTicks)
        {
            double y = mapY(tick);
            context.DrawLine(grid, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawLabel(context, TickText(tick, yStep), plot.Left - 6, y, 11, AppColors.MutedForeground, 1, true);
        }

        foreach (double tick in xTicks)
        {
            if (tick < xMin - xStep * 1e-9 || tick > xMax + xStep * 1e-9)
            {
                continue;
            }

            double x = mapX(tick);
            DrawLabel(context, TickText(tick, xStep), x, plot.Bottom + 6, 11, AppColors.MutedForeground, 0);
        }

        if (_xLabel.Length > 0)
        {
            DrawLabel(context, _xLabel, plot.Left + plot.Width / 2, plot.Bottom + 18, 11, AppColors.MutedForeground, 0);
        }

        context.DrawLine(new Pen(new SolidColorBrush(AppColors.InputColor), 1), new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));

        foreach (ChartSeries series in drawable)
        {
            DrawSeries(context, series, mapX, mapY, plot);
        }

        if (HasLegend)
        {
            DrawLegend(context, drawable.Where(s => !string.IsNullOrEmpty(s.Name)).Select(s => (s.Name, s.Color)).ToList(), plot.Left, 2);
        }
    }

    private static void DrawSeries(DrawingContext context, ChartSeries series, Func<double, double> mapX, Func<double, double> mapY, Rect plot)
    {
        List<Point> points = new List<Point>();
        for (int i = 0; i < series.X.Length; i++)
        {
            Point current = new Point(mapX(series.X[i]), mapY(series.Y[i]));
            if (series.Step && i > 0)
            {
                points.Add(new Point(current.X, points[points.Count - 1].Y));
            }

            points.Add(current);
        }

        if (series.Fill && points.Count > 1)
        {
            StreamGeometry area = new StreamGeometry();
            using (StreamGeometryContext ctx = area.Open())
            {
                ctx.BeginFigure(new Point(points[0].X, plot.Bottom), true);
                foreach (Point p in points)
                {
                    ctx.LineTo(p);
                }

                ctx.LineTo(new Point(points[points.Count - 1].X, plot.Bottom));
                ctx.EndFigure(true);
            }

            context.DrawGeometry(new SolidColorBrush(series.Color, series.FillOpacity), null, area);
        }

        Pen pen = series.Dashed
            ? new Pen(new SolidColorBrush(series.Color), series.Thickness, new DashStyle(new double[] { 4, 3 }, 0))
            : new Pen(new SolidColorBrush(series.Color), series.Thickness);

        if (points.Count > 1)
        {
            StreamGeometry line = new StreamGeometry();
            using (StreamGeometryContext ctx = line.Open())
            {
                ctx.BeginFigure(points[0], false);
                for (int i = 1; i < points.Count; i++)
                {
                    ctx.LineTo(points[i]);
                }

                ctx.EndFigure(false);
            }

            context.DrawGeometry(null, pen, line);
        }

        if (series.Dots)
        {
            SolidColorBrush dotBrush = new SolidColorBrush(series.Color);
            for (int i = 0; i < series.X.Length; i++)
            {
                context.DrawEllipse(dotBrush, null, new Point(mapX(series.X[i]), mapY(series.Y[i])), 3.5, 3.5);
            }
        }
    }
}

/// <summary>Vertical bar chart with optional error bars (used for histograms, utilization and confidence intervals).</summary>
internal sealed class BarChart : ChartBase
{
    private string[] _labels = Array.Empty<string>();
    private double[] _values = Array.Empty<double>();
    private double[]? _errors;
    private Color[]? _colors;
    private Color _barColor = AppColors.Chart1Color;
    private string _valueFormat = "F2";
    private double? _fixedMax;

    /// <param name="errors">Optional symmetric error (±) per bar, drawn as a whisker.</param>
    /// <param name="colors">Optional colour per bar.</param>
    /// <param name="fixedMax">Optional fixed top of the value axis (for example 100 for percentages).</param>
    public void SetData(
        IReadOnlyList<string> labels,
        IReadOnlyList<double> values,
        IReadOnlyList<double>? errors = null,
        IReadOnlyList<Color>? colors = null,
        Color? barColor = null,
        string valueFormat = "F2",
        double? fixedMax = null)
    {
        _labels = labels.ToArray();
        _values = values.ToArray();
        _errors = errors?.ToArray();
        _colors = colors?.ToArray();
        _barColor = barColor ?? AppColors.Chart1Color;
        _valueFormat = valueFormat;
        _fixedMax = fixedMax;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        Rect plot = PlotArea;
        int count = _values.Length;

        if (count == 0)
        {
            DrawLabel(context, "No data", Bounds.Width / 2, Bounds.Height / 2, 12, AppColors.MutedForeground, 0, true);
            return;
        }

        double top = 0;
        for (int i = 0; i < count; i++)
        {
            double err = _errors != null && i < _errors.Length ? _errors[i] : 0.0;
            top = Math.Max(top, _values[i] + err);
        }

        double bottom = 0;
        if (_errors != null)
        {
            for (int i = 0; i < count && i < _errors.Length; i++)
            {
                bottom = Math.Min(bottom, _values[i] - _errors[i]);
            }
        }

        if (_fixedMax.HasValue)
        {
            top = _fixedMax.Value;
        }

        if (!(top > bottom))
        {
            top = bottom + 1;
        }

        double step;
        List<double> ticks = NiceTicks(bottom, top * (_fixedMax.HasValue ? 1.0 : 1.05), 5, out step);
        double yMin = ticks[0];
        double yMax = ticks[ticks.Count - 1];

        Func<double, double> mapY = y => plot.Bottom - (y - yMin) / (yMax - yMin) * plot.Height;

        Pen grid = GridPen();
        foreach (double tick in ticks)
        {
            double y = mapY(tick);
            context.DrawLine(grid, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawLabel(context, TickText(tick, step), plot.Left - 6, y, 11, AppColors.MutedForeground, 1, true);
        }

        double slot = plot.Width / count;
        double barWidth = Math.Min(60, slot * 0.68);
        int labelEvery = Math.Max(1, (int)Math.Ceiling(count / Math.Max(1.0, plot.Width / 56.0)));
        double zeroY = mapY(0);

        for (int i = 0; i < count; i++)
        {
            double centerX = plot.Left + slot * (i + 0.5);
            double valueY = mapY(_values[i]);
            Color color = _colors != null && i < _colors.Length ? _colors[i] : _barColor;

            double barTop = Math.Min(valueY, zeroY);
            double barHeight = Math.Max(1.0, Math.Abs(zeroY - valueY));
            context.DrawRectangle(new SolidColorBrush(color), null, new Rect(centerX - barWidth / 2, barTop, barWidth, barHeight), 3, 3);

            if (_errors != null && i < _errors.Length && _errors[i] > 0)
            {
                Pen whisker = new Pen(new SolidColorBrush(AppColors.ForegroundColor), 1.5);
                double high = mapY(_values[i] + _errors[i]);
                double low = mapY(_values[i] - _errors[i]);
                context.DrawLine(whisker, new Point(centerX, high), new Point(centerX, low));
                context.DrawLine(whisker, new Point(centerX - 5, high), new Point(centerX + 5, high));
                context.DrawLine(whisker, new Point(centerX - 5, low), new Point(centerX + 5, low));
            }

            if (i % labelEvery == 0 && i < _labels.Length)
            {
                DrawLabel(context, _labels[i], centerX, plot.Bottom + 6, 11, AppColors.MutedForeground, 0);
            }
        }

        if (count <= 14)
        {
            for (int i = 0; i < count; i++)
            {
                double err = _errors != null && i < _errors.Length ? _errors[i] : 0.0;
                double centerX = plot.Left + slot * (i + 0.5);
                double labelY = mapY(_values[i] + err) - 15;
                DrawLabel(context, _values[i].ToString(_valueFormat, CultureInfo.InvariantCulture), centerX, labelY, 10.5, AppColors.Foreground, 0);
            }
        }

        context.DrawLine(new Pen(new SolidColorBrush(AppColors.InputColor), 1), new Point(plot.Left, zeroY), new Point(plot.Right, zeroY));
    }
}
