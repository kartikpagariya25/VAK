using System.Windows;
using System.Windows.Media;

namespace VAK.App.Controls;

/// <summary>A small area line chart with a dot and a dashed guide at each value, used for the hourly temperatures.</summary>
public sealed class HourChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(HourChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values { get => (IReadOnlyList<double>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var v = Values;
        if (v is null || v.Count < 2 || ActualWidth < 10 || ActualHeight < 10) return;

        double min = v.Min(), max = v.Max(), span = Math.Max(max - min, 1);
        var colWidth = ActualWidth / v.Count; // points sit in the centre of equal columns, matching the labels above
        Point P(int i) => new(colWidth * (i + 0.5), 6 + (1 - (v[i] - min) / span) * (ActualHeight - 16));

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(P(0), false, false);
            for (var i = 1; i < v.Count; i++) ctx.LineTo(P(i), true, true);
        }
        line.Freeze();

        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(P(0).X, ActualHeight), true, true);
            for (var i = 0; i < v.Count; i++) ctx.LineTo(P(i), true, false);
            ctx.LineTo(new Point(P(v.Count - 1).X, ActualHeight), true, false);
        }
        area.Freeze();

        var accent = ((SolidColorBrush)FindResource("Brush.Accent")).Color;
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(40, accent.R, accent.G, accent.B)), null, area);

        var guide = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1) { DashStyle = new DashStyle(new[] { 2.0, 3.0 }, 0) };
        for (var i = 0; i < v.Count; i++)
            dc.DrawLine(guide, P(i), new Point(P(i).X, ActualHeight));

        dc.DrawGeometry(null, new Pen(Brushes.White, 1.5) { LineJoin = PenLineJoin.Round }, line);
        for (var i = 0; i < v.Count; i++) dc.DrawEllipse(Brushes.White, null, P(i), 3, 3);
    }
}
