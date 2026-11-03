using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using VAK.App.Controls;
using VAK.App.Theme;
using VAK.App.Widgets;
using VAK.Core.Weather;

namespace VAK.App.Pages;

public sealed class WeatherPage : UserControl, IHeaderExtras
{
    private readonly AppHost _host;
    private readonly Grid _root = new();
    private readonly TextBlock _empty;
    private Action _refresh = () => { };

    public WeatherPage(AppHost host)
    {
        _host = host;
        _empty = new TextBlock
        {
            Style = (Style)FindResource("Style.TextDim"), FontSize = 14, TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(215) });
        Content = new Grid { Children = { _root, _empty } };

        Loaded += async (_, _) =>
        {
            _host.Weather.Changed += Render;
            Render();
            await _host.Weather.EnsureFreshAsync();
        };
        Unloaded += (_, _) => _host.Weather.Changed -= Render;
    }

    public UIElement CreateHeaderExtras()
    {
        var units = new SegmentedControl("°C", "°F") { SelectedIndex = _host.Settings.Weather.Fahrenheit ? 1 : 0 };
        units.SelectionChanged += i => { _host.Settings.Weather.Fahrenheit = i == 1; _host.Save(); Render(); };

        var change = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = double.NaN, Height = 34, Padding = new Thickness(14, 0, 14, 0),
            Background = (Brush)FindResource("Brush.Control"), ToolTip = "Change city",
        };
        var line = new StackPanel { Orientation = Orientation.Horizontal };
        line.Children.Add(new TextBlock { Text = Icons.Glyph("location"), Style = (Style)FindResource("Style.Icon"), FontSize = 15, Margin = new Thickness(0, 0, 6, 0) });
        line.Children.Add(new TextBlock { Text = "Change City", Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium });
        change.Content = line;
        change.Click += (_, _) => CityPicker.Show(change, _host, async p => await _host.Weather.SetCityAsync(p));

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(units);
        row.Children.Add(new Border { Width = 10 });
        row.Children.Add(change);
        return row;
    }

    private bool F => _host.Settings.Weather.Fahrenheit;

    private void Render()
    {
        _root.Children.Clear();
        var report = _host.Weather.Report;
        if (!_host.Weather.HasCity)
        {
            _empty.Text = "Choose your city to see the weather.\nUse Change City at the top.";
            _empty.Visibility = Visibility.Visible;
            return;
        }
        if (report is null)
        {
            _empty.Text = _host.Weather.Failed ? "Couldn't load the weather. Check your connection." : "Loading weather…";
            _empty.Visibility = Visibility.Visible;
            return;
        }
        _empty.Visibility = Visibility.Collapsed;

        Add(0, Current(report));
        Add(1, Hourly(report));
        Add(2, FiveDay(report));
    }

    private void Add(int column, UIElement e)
    {
        Grid.SetColumn(e, column);
        _root.Children.Add(e);
    }

    private TextBlock Text(string text, string style = "Style.Text", double size = 13, FontWeight? weight = null)
    {
        var t = new TextBlock { Text = text, Style = (Style)FindResource(style), FontSize = size };
        if (weight is { } w) t.FontWeight = w;
        return t;
    }

    private UIElement Current(WeatherReport r)
    {
        var n = r.Now;
        var (text, icon) = WeatherCodes.Describe(n.Code, n.IsDay);
        var today = r.Daily.FirstOrDefault();

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = Icons.Glyph(icon), Style = (Style)FindResource("Style.Icon"), FontSize = 44, HorizontalAlignment = HorizontalAlignment.Left });
        stack.Children.Add(Text(WeatherCodes.FormatTemp(n.Temp, F), "Style.Big", 54));
        stack.Children.Add(Text(text, "Style.Text", 15, FontWeights.Medium));
        var hl = today is null ? "" : $"H {WeatherCodes.FormatTemp(today.Max, F)}  L {WeatherCodes.FormatTemp(today.Min, F)}  ·  ";
        stack.Children.Add(Text($"{hl}Feels like {WeatherCodes.FormatTemp(n.Feels, F)}", "Style.TextDim", 12));

        var footer = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
        footer.Children.Add(new Border { Height = 1, Background = (Brush)FindResource("Brush.Divider"), Margin = new Thickness(0, 0, 0, 8) });
        if (today?.Sunrise is { } sr && today.Sunset is { } ss)
            footer.Children.Add(Text($"Sunrise {Clock(sr)}  ·  Sunset {Clock(ss)}", "Style.TextDim", 11));
        footer.Children.Add(Text($"{(_host.Weather.Failed ? "Offline · last updated" : "Updated")} {Clock(r.Updated)}", "Style.TextDim", 11));

        var grid = new Grid();
        grid.Children.Add(stack);
        grid.Children.Add(footer);
        return new Border { Style = (Style)FindResource("Style.Card"), Child = grid, Margin = new Thickness(5) };
    }

    private UIElement Hourly(WeatherReport r)
    {
        var hours = OpenMeteo.NextHours(r, 6);
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(Text("Hourly", "Style.TextDim", 12));

        var body = new Grid { Margin = new Thickness(0, 8, 0, 8) };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var cols = new UniformGrid { Columns = Math.Max(1, hours.Count) };
        for (var i = 0; i < hours.Count; i++)
        {
            var h = hours[i];
            var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var label = Text(i == 0 ? "Now" : HourLabel(h.Time), "Style.TextDim", 11);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            var ic = new TextBlock { Text = Icons.Glyph(WeatherCodes.Describe(h.Code, h.Time.Hour is >= 6 and < 18).Icon), Style = (Style)FindResource("Style.Icon"), FontSize = 16, Margin = new Thickness(0, 6, 0, 4) };
            var t = Text(WeatherCodes.FormatTemp(h.Temp, F), "Style.Text", 13, FontWeights.Medium);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            col.Children.Add(label); col.Children.Add(ic); col.Children.Add(t);
            cols.Children.Add(col);
        }
        body.Children.Add(cols);
        var chart = new HourChart { Values = hours.Select(h => F ? h.Temp * 9 / 5 + 32 : h.Temp).ToList(), Margin = new Thickness(0, 8, 0, 0) };
        Grid.SetRow(chart, 1);
        body.Children.Add(chart);
        Grid.SetRow(body, 1);
        grid.Children.Add(body);

        var stats = new UniformGrid { Columns = 3 };
        stats.Children.Add(Stat("water", "Humidity", $"{r.Now.Humidity}%"));
        stats.Children.Add(Stat("speed", "Wind", $"{Math.Round(r.Now.WindKmh)} km/h"));
        stats.Children.Add(Stat("sunny", "UV index", $"{Math.Round(r.Now.Uv)} {WeatherCodes.UvLabel(r.Now.Uv)}"));
        Grid.SetRow(stats, 2);
        grid.Children.Add(new Border { Height = 1, Background = (Brush)FindResource("Brush.Divider"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 0, 0) });
        Grid.SetRow(grid.Children[^1], 2);
        grid.Children.Add(stats);

        return new Border { Style = (Style)FindResource("Style.Card"), Child = grid, Margin = new Thickness(5) };
    }

    private UIElement Stat(string icon, string label, string value)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        p.Children.Add(new TextBlock { Text = Icons.Glyph(icon), Style = (Style)FindResource("Style.Icon"), FontSize = 16, Foreground = (Brush)FindResource("Brush.TextDim"), Margin = new Thickness(0, 0, 8, 0) });
        var s = new StackPanel();
        s.Children.Add(Text(label, "Style.TextDim", 11));
        s.Children.Add(Text(value, "Style.Text", 13, FontWeights.Medium));
        p.Children.Add(s);
        return p;
    }

    private UIElement FiveDay(WeatherReport r)
    {
        var days = r.Daily.Take(5).ToList();
        var lo = days.Count == 0 ? 0 : days.Min(d => d.Min);
        var hi = days.Count == 0 ? 1 : days.Max(d => d.Max);
        var span = Math.Max(hi - lo, 1);

        var list = new StackPanel();
        list.Children.Add(Text("5-Day", "Style.TextDim", 12));
        for (var i = 0; i < days.Count; i++)
        {
            var d = days[i];
            var row = new Grid { Margin = new Thickness(0, 7, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var name = Text(i == 0 ? "Today" : d.Date.ToString("ddd", CultureInfo.InvariantCulture), "Style.Text", 13, FontWeights.Medium);
            name.VerticalAlignment = VerticalAlignment.Center;
            var ic = new TextBlock { Text = Icons.Glyph(WeatherCodes.Describe(d.Code, true).Icon), Style = (Style)FindResource("Style.Icon"), FontSize = 15 };
            Grid.SetColumn(ic, 1);

            var range = new Grid { Margin = new Thickness(8, 0, 0, 0) };
            range.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            range.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            range.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var loText = Text(WeatherCodes.FormatTemp(d.Min, F), "Style.TextDim", 12); loText.Width = 30; loText.VerticalAlignment = VerticalAlignment.Center;
            var hiText = Text(WeatherCodes.FormatTemp(d.Max, F), "Style.Text", 12, FontWeights.Medium); hiText.Width = 30; hiText.TextAlignment = TextAlignment.Right; hiText.VerticalAlignment = VerticalAlignment.Center;
            var bar = new Grid { Height = 4, Margin = new Thickness(4, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength((d.Min - lo) / span, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max((d.Max - d.Min) / span, 0.08), GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1 - (d.Max - lo) / span, 0), GridUnitType.Star) });
            var track = new Border { CornerRadius = new CornerRadius(2), Background = (Brush)FindResource("Brush.Control") };
            Grid.SetColumnSpan(track, 3);
            var fill = new Border { CornerRadius = new CornerRadius(2), Background = Brushes.White };
            Grid.SetColumn(fill, 1);
            bar.Children.Add(track); bar.Children.Add(fill);
            Grid.SetColumn(loText, 0); Grid.SetColumn(bar, 1); Grid.SetColumn(hiText, 2);
            range.Children.Add(loText); range.Children.Add(bar); range.Children.Add(hiText);
            Grid.SetColumn(range, 2);

            row.Children.Add(name); row.Children.Add(ic); row.Children.Add(range);
            list.Children.Add(row);
        }
        return new Border { Style = (Style)FindResource("Style.Card"), Child = list, Margin = new Thickness(5) };
    }

    private string Clock(DateTime t) => _host.Settings.Use24Hour ? t.ToString("HH:mm", CultureInfo.InvariantCulture) : t.ToString("h:mm", CultureInfo.InvariantCulture);

    private string HourLabel(DateTime t) => _host.Settings.Use24Hour ? t.ToString("HH:00", CultureInfo.InvariantCulture) : t.ToString("h tt", CultureInfo.InvariantCulture);
}
