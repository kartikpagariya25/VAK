using System.Windows;
using System.Windows.Media;

namespace VAK.App.Controls;

/// <summary>A tiny line chart. Values are scaled to the largest value seen (never below <see cref="MinScale"/>).</summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values { get => (IReadOnlyList<double>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    /// <summary>Smallest full-height value; stops a near-idle line from looking like a mountain range.</summary>
    public double MinScale { get; set; } = 1;

    /// <summary>Fixed full-height value (for example 100 for percentages); when set, MinScale is ignored.</summary>
    public double? FixedMax { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        var values = Values;
        if (values is null || values.Count < 2 || ActualWidth < 2 || ActualHeight < 2) return;

        var max = FixedMax ?? Math.Max(MinScale, values.Max());
        var step = ActualWidth / (values.Count - 1);
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (var i = 0; i < values.Count; i++)
            {
                var y = ActualHeight - Math.Clamp(values[i] / max, 0, 1) * (ActualHeight - 2) - 1;
                var p = new Point(i * step, y);
                if (i == 0) ctx.BeginFigure(p, false, false); else ctx.LineTo(p, true, true);
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, new Pen(Stroke, 1.5) { LineJoin = PenLineJoin.Round }, geo);
    }
}
