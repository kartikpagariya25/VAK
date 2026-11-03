using System.Windows;
using System.Windows.Media;

namespace VAK.App.Controls;

/// <summary>A round analog clock face with hour, minute and (optionally) second hands.</summary>
public sealed class AnalogClock : FrameworkElement
{
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
        nameof(Time), typeof(DateTime), typeof(AnalogClock),
        new FrameworkPropertyMetadata(DateTime.Now, FrameworkPropertyMetadataOptions.AffectsRender));

    public DateTime Time { get => (DateTime)GetValue(TimeProperty); set => SetValue(TimeProperty, value); }
    public bool ShowSeconds { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size < 20) return;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        var r = size / 2 - 2;
        var white = Brushes.White;
        var dim = (Brush)FindResource("Brush.TextFaint");

        dc.DrawEllipse(null, new Pen((Brush)FindResource("Brush.Control"), 2), c, r, r);
        for (var i = 0; i < 12; i++)
        {
            var a = i * Math.PI / 6;
            var inner = r - (i % 3 == 0 ? 9 : 5);
            dc.DrawLine(new Pen(i % 3 == 0 ? white : dim, i % 3 == 0 ? 2 : 1),
                new Point(c.X + Math.Sin(a) * inner, c.Y - Math.Cos(a) * inner),
                new Point(c.X + Math.Sin(a) * (r - 2), c.Y - Math.Cos(a) * (r - 2)));
        }

        var t = Time;
        Hand(dc, c, (t.Hour % 12 + t.Minute / 60.0) * 30, r * 0.5, 4, white);
        Hand(dc, c, (t.Minute + t.Second / 60.0) * 6, r * 0.75, 3, white);
        if (ShowSeconds) Hand(dc, c, t.Second * 6, r * 0.85, 1.5, (Brush)FindResource("Brush.Accent"));
        dc.DrawEllipse(white, null, c, 3.5, 3.5);
    }

    private static void Hand(DrawingContext dc, Point c, double degrees, double length, double thickness, Brush brush)
    {
        var a = degrees * Math.PI / 180;
        dc.DrawLine(new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
            c, new Point(c.X + Math.Sin(a) * length, c.Y - Math.Cos(a) * length));
    }
}
