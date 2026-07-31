using System.Globalization;
using System.Windows;
using System.Windows.Media;
using OptiDiag.Application;

namespace OptiDiag.App.Controls;

public sealed class TrendChart : FrameworkElement
{
    private static readonly (string[] Ids, string Name, Color Color)[] Series =
    [
        (["temperature"], "温度", Color.FromRgb(255, 184, 92)),
        (["voltage"], "电压", Color.FromRgb(72, 198, 239)),
        (["bias", "bias-lane-1"], "偏置", Color.FromRgb(165, 124, 255)),
        (["tx-power", "tx-power-lane-1"], "TX", Color.FromRgb(64, 211, 151)),
        (["rx-power", "rx-power-lane-1"], "RX", Color.FromRgb(255, 102, 153))
    ];

    public static readonly DependencyProperty SamplesProperty = DependencyProperty.Register(
        nameof(Samples),
        typeof(IReadOnlyList<TrendSample>),
        typeof(TrendChart),
        new FrameworkPropertyMetadata(Array.Empty<TrendSample>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<TrendSample> Samples
    {
        get => (IReadOnlyList<TrendSample>)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        drawingContext.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(18, 27, 44)), null, bounds, 8, 8);
        if (ActualWidth < 120 || ActualHeight < 100)
        {
            return;
        }

        var plot = new Rect(44, 40, ActualWidth - 64, ActualHeight - 70);
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(65, 110, 135, 165)), 1);
        for (var index = 0; index <= 4; index++)
        {
            var y = plot.Top + plot.Height * index / 4;
            drawingContext.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        for (var index = 0; index <= 6; index++)
        {
            var x = plot.Left + plot.Width * index / 6;
            drawingContext.DrawLine(gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }

        var samples = Samples ?? [];
        if (samples.Count < 2)
        {
            DrawText(drawingContext, "启动周期读取后显示趋势曲线", new Point(plot.Left + 10, plot.Top + 10), Brushes.LightSlateGray, 13);
            return;
        }

        var legendX = plot.Left;
        foreach (var definition in Series)
        {
            var seriesId = definition.Ids.FirstOrDefault(
                id => samples.Any(sample => sample.Values.ContainsKey(id)));
            if (seriesId is null)
            {
                continue;
            }

            var values = samples
                .Select(x => x.Values.TryGetValue(seriesId, out var value) ? value : double.NaN)
                .ToArray();
            var finite = values.Where(double.IsFinite).ToArray();
            if (finite.Length < 2)
            {
                continue;
            }

            var minimum = finite.Min();
            var maximum = finite.Max();
            var span = Math.Max(maximum - minimum, Math.Max(Math.Abs(maximum) * 0.02, 0.001));
            minimum -= span * 0.1;
            maximum += span * 0.1;
            span = maximum - minimum;

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var started = false;
                for (var index = 0; index < values.Length; index++)
                {
                    if (!double.IsFinite(values[index]))
                    {
                        started = false;
                        continue;
                    }

                    var x = plot.Left + plot.Width * index / Math.Max(values.Length - 1, 1);
                    var normalized = (values[index] - minimum) / span;
                    var y = plot.Bottom - plot.Height * normalized;
                    if (!started)
                    {
                        context.BeginFigure(new Point(x, y), false, false);
                        started = true;
                    }
                    else
                    {
                        context.LineTo(new Point(x, y), true, false);
                    }
                }
            }

            geometry.Freeze();
            var brush = new SolidColorBrush(definition.Color);
            brush.Freeze();
            drawingContext.DrawGeometry(null, new Pen(brush, 1.8), geometry);
            drawingContext.DrawRectangle(brush, null, new Rect(legendX, 15, 12, 3));
            DrawText(
                drawingContext,
                $"{definition.Name} {finite[^1]:0.###}",
                new Point(legendX + 17, 7),
                brush,
                11);
            legendX += 112;
        }

        DrawText(
            drawingContext,
            samples[0].Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            new Point(plot.Left, plot.Bottom + 8),
            Brushes.SlateGray,
            10);
        DrawText(
            drawingContext,
            samples[^1].Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            new Point(plot.Right - 42, plot.Bottom + 8),
            Brushes.SlateGray,
            10);
    }

    private void DrawText(DrawingContext context, string text, Point location, Brush brush, double size)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        context.DrawText(formatted, location);
    }
}
