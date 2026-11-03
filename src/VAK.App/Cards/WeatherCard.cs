using System.Windows;
using System.Windows.Controls;
using VAK.App.Controls;
using VAK.App.Theme;
using VAK.Core.Weather;

namespace VAK.App.Cards;

/// <summary>Home card: temperature, condition and city, with high and low.</summary>
public sealed class WeatherCard : UserControl
{
    private readonly AppHost _host;
    private readonly TextBlock _temp, _line1, _line2, _icon;

    public WeatherCard(AppHost host)
    {
        _host = host;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(new CardHeader("partly-day", "Weather"));

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _temp = new TextBlock { Style = (Style)FindResource("Style.Big"), FontSize = 34 };
        _line1 = new TextBlock { Style = (Style)FindResource("Style.Text"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
        _line2 = new TextBlock { Style = (Style)FindResource("Style.TextDim"), FontSize = 12 };
        stack.Children.Add(_temp); stack.Children.Add(_line1); stack.Children.Add(_line2);
        Grid.SetRow(stack, 1);
        root.Children.Add(stack);

        _icon = new TextBlock { Style = (Style)FindResource("Style.Icon"), FontSize = 34, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(_icon, 1);
        root.Children.Add(_icon);
        Content = root;

        Loaded += async (_, _) => { _host.Weather.Changed += Render; Render(); await _host.Weather.EnsureFreshAsync(); };
        Unloaded += (_, _) => _host.Weather.Changed -= Render;
    }

    private void Render()
    {
        var w = _host.Settings.Weather;
        if (!_host.Weather.HasCity) { _temp.Text = "-"; _line1.Text = "Choose a city"; _line2.Text = ""; _icon.Text = ""; return; }
        if (_host.Weather.Report is not { } r) { _temp.Text = "-"; _line1.Text = w.City; _line2.Text = _host.Weather.Failed ? "Offline" : "Loading…"; _icon.Text = ""; return; }

        var (text, icon) = WeatherCodes.Describe(r.Now.Code, r.Now.IsDay);
        _temp.Text = WeatherCodes.FormatTemp(r.Now.Temp, w.Fahrenheit);
        _line1.Text = $"{text} · {w.City}";
        var today = r.Daily.FirstOrDefault();
        _line2.Text = today is null ? "" : $"H {WeatherCodes.FormatTemp(today.Max, w.Fahrenheit)}  L {WeatherCodes.FormatTemp(today.Min, w.Fahrenheit)}";
        _icon.Text = Icons.Glyph(icon);
    }
}
