using System.Windows;
using System.Windows.Media;

namespace VAK.App.Controls;

/// <summary>A circular gauge: a dim track and an accent arc from 12 o'clock for <see cref="Value"/> (0-100).</summary>
public sealed class RingGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(RingGauge),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Thickness { get; set; } = 6;
    public Brush? Track { get; set; }
    public Brush? Arc { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= Thickness) return;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = (size - Thickness) / 2;
        var track = Track ?? (Brush)FindResource("Brush.Control");
        var arc = Arc ?? (Brush)FindResource("Brush.Accent");

        dc.DrawEllipse(null, new Pen(track, Thickness), center, radius, radius);

        var fraction = Math.Clamp(Value, 0, 100) / 100;
        if (fraction <= 0.001) return;
        if (fraction >= 0.999) { dc.DrawEllipse(null, new Pen(arc, Thickness), center, radius, radius); return; }

        var angle = fraction * 2 * Math.PI;
        var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(center.X, center.Y - radius), false, false);
            ctx.ArcTo(end, new Size(radius, radius), 0, fraction > 0.5, SweepDirection.Clockwise, true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, new Pen(arc, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, geo);
    }
}
