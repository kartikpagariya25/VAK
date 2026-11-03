using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VAK.App.Controls;
using VAK.Core.ScreenTime;

namespace VAK.App.Cards;

/// <summary>Home card: today's total, the top app, and seven mini bars with today highlighted.</summary>
public sealed class ScreenTimeCard : UserControl
{
    private readonly AppHost _host;
    private readonly TextBlock _total, _topName;
    private readonly Image _topIcon;
    private readonly StackPanel _bars = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
    private int _ticks;

    public ScreenTimeCard(AppHost host)
    {
        _host = host;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(new CardHeader("chart", "Screen time"));

        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _total = new TextBlock { Style = (Style)FindResource("Style.Big"), FontSize = 32 };
        body.Children.Add(_total);

        _topIcon = new Image { Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0) };
        _topName = new TextBlock { Style = (Style)FindResource("Style.TextDim"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        top.Children.Add(_topIcon); top.Children.Add(_topName);
        Grid.SetRow(top, 1);
        body.Children.Add(top);
        Grid.SetRow(_bars, 2);
        body.Children.Add(_bars);
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        Content = root;

        Loaded += (_, _) => { if (_host.ScreenTimeTracker is { } t) t.Updated += OnTick; Render(); };
        Unloaded += (_, _) => { if (_host.ScreenTimeTracker is { } t) t.Updated -= OnTick; };
    }

    private void OnTick() { if (++_ticks % 5 == 0) Render(); }

    private void Render()
    {
        var log = _host.ScreenTime;
        var today = DateTime.Now;
        _total.Text = DurationFormatter.Short(log.TotalSeconds(today));

        var top = log.TopApps(today, 1).FirstOrDefault();
        _topName.Text = top is null ? "No app time yet" : $"{top.Name} · {DurationFormatter.Short(top.Seconds)}";
        _topIcon.Source = top is null ? null : _host.ScreenTimeTracker?.IconFor(top.App);
        _topIcon.Visibility = _topIcon.Source is null ? Visibility.Collapsed : Visibility.Visible;

        var days = log.Last7Days();
        var max = Math.Max(days.Max(d => d.Seconds), 60);
        _bars.Children.Clear();
        foreach (var d in days)
        {
            var isToday = d.Date == today.Date;
            _bars.Children.Add(new Border
            {
                Width = isToday ? 10 : 22, Height = isToday ? Math.Max(8, 24 * d.Seconds / max) : 3, CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, isToday ? 0 : 12, 0), VerticalAlignment = VerticalAlignment.Bottom,
                Background = isToday ? (Brush)FindResource("Brush.Accent") : (Brush)FindResource("Brush.Control"),
            });
        }
    }
}
