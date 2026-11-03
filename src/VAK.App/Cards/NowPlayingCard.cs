using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VAK.App.Controls;
using VAK.App.Theme;

namespace VAK.App.Cards;

/// <summary>What is playing on this PC (from Windows media controls) with previous, play/pause and next. Used as a Home card and as the Now Playing page.</summary>
public sealed class NowPlayingCard : UserControl
{
    private readonly AppHost _host;
    private readonly Image _art = new() { Stretch = Stretch.UniformToFill };
    private readonly Border _artBox = new() { CornerRadius = new CornerRadius(10), ClipToBounds = true, Background = (Brush)Application.Current.FindResource("Brush.Control") };
    private readonly TextBlock _title;
    private readonly TextBlock _artist;
    private readonly TextBlock _play;
    private readonly EqualizerBars _bars = new() { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Action<bool> _changed;

    public NowPlayingCard(AppHost host, bool large = false)
    {
        _host = host;
        var size = large ? 150.0 : 56.0;
        _artBox.Width = size; _artBox.Height = size;
        _artBox.Child = _art;
        _title = new TextBlock { Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.SemiBold, FontSize = large ? 20 : 14, TextTrimming = TextTrimming.CharacterEllipsis };
        _artist = new TextBlock { Style = (Style)FindResource("Style.TextDim"), FontSize = large ? 15 : 12, TextTrimming = TextTrimming.CharacterEllipsis };
        _play = new TextBlock { Style = (Style)FindResource("Style.Icon"), FontSize = large ? 26 : 20, Foreground = (Brush)FindResource("Brush.Text") };

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, large ? 14 : 6, 0, 0) };
        controls.Children.Add(Control("up", "Previous", _host.NowPlaying.Previous, true, large));
        var playButton = Control(null, "Play or pause", _host.NowPlaying.PlayPause, false, large);
        controls.Children.Add(playButton);
        controls.Children.Add(Control("next", "Next", _host.NowPlaying.Next, false, large));

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        text.Children.Add(_bars); text.Children.Add(_title); text.Children.Add(_artist); text.Children.Add(controls);
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(_artBox);
        Grid.SetColumn(text, 1); row.Children.Add(text);

        if (large)
        {
            Content = new Border { Style = (Style)FindResource("Style.Card"), Margin = new Thickness(5), Padding = new Thickness(24), Child = new Grid { VerticalAlignment = VerticalAlignment.Center, Children = { row } } };
        }
        else
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(new CardHeader("play", "Now Playing"));
            row.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(row, 1); root.Children.Add(row);
            Content = root;
        }

        _changed = _ => Dispatcher.Invoke(Render);
        Loaded += (_, _) => { _host.NowPlaying.Changed += _changed; Render(); };
        Unloaded += (_, _) => _host.NowPlaying.Changed -= _changed;
    }

    private Button Control(string? icon, string name, Action click, bool flip, bool large)
    {
        var glyph = icon is null ? _play : new TextBlock { Text = Icons.Glyph(icon), Style = (Style)FindResource("Style.Icon"), FontSize = large ? 22 : 16, Foreground = (Brush)FindResource("Brush.Text"), RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(flip ? -90 : 90) };
        var b = new Button { Style = (Style)FindResource("Style.RoundButton"), Width = large ? 46 : 34, Height = large ? 40 : 30, Background = Brushes.Transparent, Content = glyph };
        b.Click += (_, _) => click();
        System.Windows.Automation.AutomationProperties.SetName(b, name);
        return b;
    }

    private void Render()
    {
        var t = _host.NowPlaying.Current;
        if (t is null)
        {
            _title.Text = "Nothing playing";
            _artist.Text = "Play something in Spotify, a browser or a video player";
            _art.Source = null;
            _bars.Run(false); _bars.Visibility = Visibility.Collapsed;
            _play.Text = Icons.Glyph("play");
            return;
        }
        _title.Text = t.Title;
        _artist.Text = t.Artist;
        _art.Source = t.Art;
        _play.Text = Icons.Glyph(t.Playing ? "pause" : "play");
        _bars.Visibility = t.Playing ? Visibility.Visible : Visibility.Collapsed;
        _bars.SetColor(_host.Settings.Live.MusicColor == "auto" ? t.Color : ThemeService.Parse(_host.Settings.Live.MusicColor, Colors.White));
        _bars.Run(t.Playing);
    }
}
