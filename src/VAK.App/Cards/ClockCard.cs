using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VAK.App.Controls;
using VAK.Core.Clock;

namespace VAK.App.Cards;

/// <summary>Home card: the current time and date.</summary>
public sealed class ClockCard : UserControl
{
    private readonly AppHost _host;
    private readonly TextBlock _time;
    private readonly TextBlock _date;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public ClockCard(AppHost host)
    {
        _host = host;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(new CardHeader("clock", "Clock"));

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _time = new TextBlock { Style = (Style)FindResource("Style.Big"), FontSize = 34 };
        _date = new TextBlock { Style = (Style)FindResource("Style.TextDim"), FontSize = 12 };
        stack.Children.Add(_time);
        stack.Children.Add(_date);
        Grid.SetRow(stack, 1);
        root.Children.Add(stack);
        Content = root;

        _timer.Tick += (_, _) => Render();
        Loaded += (_, _) => { _timer.Start(); Render(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private void Render()
    {
        var s = _host.Settings;
        var now = DateTimeOffset.Now;
        _time.Text = ClockFormatter.Time(now, TimeZoneInfo.Local, s.Use24Hour, s.Clock.ShowSeconds);
        _date.Text = s.Clock.ShowDate ? ClockFormatter.Date(now, s.Clock.DateStyle) : "";
    }
}
