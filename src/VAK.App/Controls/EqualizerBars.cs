using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VAK.App.Controls;

/// <summary>Three small bars that bounce while music plays (the "beat" on the closed notch).</summary>
public sealed class EqualizerBars : StackPanel
{
    private readonly List<Border> _bars = new();
    private bool _running;

    public EqualizerBars()
    {
        Orientation = Orientation.Horizontal;
        Height = 14;
        VerticalAlignment = VerticalAlignment.Center;
        for (var i = 0; i < 3; i++)
        {
            var bar = new Border { Width = 3, Height = 4, Margin = new Thickness(i == 0 ? 0 : 2, 0, 0, 0), CornerRadius = new CornerRadius(1.5), VerticalAlignment = VerticalAlignment.Bottom, Background = Brushes.White };
            _bars.Add(bar);
            Children.Add(bar);
        }
    }

    public void SetColor(Color color)
    {
        foreach (var b in _bars) b.Background = new SolidColorBrush(color);
    }

    public void Run(bool on)
    {
        if (on == _running) return;
        _running = on;
        for (var i = 0; i < _bars.Count; i++)
        {
            if (on)
            {
                var a = new DoubleAnimation(3, 13, TimeSpan.FromMilliseconds(380 + i * 130)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
                _bars[i].BeginAnimation(HeightProperty, a);
            }
            else _bars[i].BeginAnimation(HeightProperty, null);
        }
    }
}
