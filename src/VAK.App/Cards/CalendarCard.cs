using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using VAK.App.Controls;

namespace VAK.App.Cards;

/// <summary>Home card: today's next events.</summary>
public sealed class CalendarCard : UserControl
{
    private readonly AppHost _host;
    private readonly StackPanel _body = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };

    public CalendarCard(AppHost host)
    {
        _host = host;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(new CardHeader("calendar", "Calendar"));
        Grid.SetRow(_body, 1);
        root.Children.Add(_body);
        Content = root;

        _timer.Tick += (_, _) => Render();
        Loaded += (_, _) => { _host.Calendar.Changed += Render; _timer.Start(); Render(); };
        Unloaded += (_, _) => { _host.Calendar.Changed -= Render; _timer.Stop(); };
    }

    private string Time(DateTime t) => _host.Settings.Use24Hour ? t.ToString("HH:mm", CultureInfo.InvariantCulture) : t.ToString("h:mm tt", CultureInfo.InvariantCulture);

    private void Render()
    {
        _body.Children.Clear();
        var now = DateTime.Now;
        var today = _host.Calendar.Store.Upcoming(now, 0, 3);
        if (today.Count == 0)
        {
            var next = _host.Calendar.Store.Upcoming(now, 30, 1).FirstOrDefault();
            _body.Children.Add(new TextBlock { Text = "No more events today", Style = (Style)FindResource("Style.Text"), FontSize = 15, FontWeight = FontWeights.SemiBold });
            _body.Children.Add(new TextBlock
            {
                Text = next is null ? "Your day is clear" : $"Next: {next.Start:ddd, MMM d} · {next.Event.Title}",
                Style = (Style)FindResource("Style.TextDim"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis,
            });
            return;
        }
        foreach (var o in today)
        {
            var dot = new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(o.Event.Color)), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            var when = new TextBlock { Text = o.Event.AllDay ? "All day" : Time(o.Start), Style = (Style)FindResource("Style.TextDim"), FontSize = 11, Width = 58, VerticalAlignment = VerticalAlignment.Center };
            var title = new TextBlock { Text = o.Event.Title, Style = (Style)FindResource("Style.Text"), FontSize = 13, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(dot); row.Children.Add(when); row.Children.Add(title);
            _body.Children.Add(row);
        }
    }
}
