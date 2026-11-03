using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VAK.App.Controls;
using VAK.App.Theme;
using VAK.Core.Clipboard;

namespace VAK.App.Cards;

/// <summary>Home card: the latest copied item, or a hint when nothing was copied yet.</summary>
public sealed class ClipboardCard : UserControl
{
    private readonly AppHost _host;
    private readonly Grid _body = new();

    public ClipboardCard(AppHost host)
    {
        _host = host;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(new CardHeader("clipboard", "Clipboard"));
        Grid.SetRow(_body, 1);
        _body.VerticalAlignment = VerticalAlignment.Center;
        root.Children.Add(_body);
        Content = root;

        Loaded += (_, _) => { if (_host.ClipboardMonitor is { } m) m.Captured += Render; Render(); };
        Unloaded += (_, _) => { if (_host.ClipboardMonitor is { } m) m.Captured -= Render; };
    }

    private void Render()
    {
        _body.Children.Clear();
        var latest = _host.Clipboard.All.FirstOrDefault();
        if (latest is null)
        {
            var s = new StackPanel();
            s.Children.Add(new TextBlock { Text = "Nothing copied yet", Style = (Style)FindResource("Style.Text"), FontSize = 15, FontWeight = FontWeights.SemiBold });
            s.Children.Add(new TextBlock { Text = "Copied items appear here", Style = (Style)FindResource("Style.TextDim"), FontSize = 12 });
            _body.Children.Add(s);
            return;
        }
        var tile = new Border
        {
            Width = 34, Height = 34, CornerRadius = new CornerRadius(8), Background = (Brush)FindResource("Brush.Control"),
            Child = new TextBlock { Text = Icons.Glyph(latest.Kind == ClipKind.Link ? "link" : latest.Kind == ClipKind.Image ? "image" : "document"), Style = (Style)FindResource("Style.Icon"), FontSize = 16 },
        };
        var label = latest.Kind == ClipKind.Image ? $"Image {latest.Width} × {latest.Height}" : latest.Text.Trim().Split('\n')[0];
        var text = new TextBlock { Text = label, Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 8, 0) };
        var ago = new TextBlock { Text = Pages.ClipboardPage.Ago(latest.Time), Style = (Style)FindResource("Style.TextDim"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        var line = new Grid();
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.Children.Add(tile);
        Grid.SetColumn(text, 1); line.Children.Add(text);
        Grid.SetColumn(ago, 2); line.Children.Add(ago);
        _body.Children.Add(line);
    }
}
