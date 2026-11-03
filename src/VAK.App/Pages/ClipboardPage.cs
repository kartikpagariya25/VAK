using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VAK.App.Services;
using VAK.App.Theme;
using VAK.Core.Clipboard;

namespace VAK.App.Pages;

public sealed class ClipboardPage : UserControl
{
    private readonly AppHost _host;
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _list = new();
    private readonly Grid _detail = new();
    private TextBox _search = null!;      // created in the constructor, after the search button that refers to them
    private Border _searchBox = null!;
    private ClipKind? _tab;
    private string? _selectedId;

    public ClipboardPage(AppHost host)
    {
        _host = host;
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(370) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new Grid { Margin = new Thickness(5, 0, 10, 0) };
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var tabRow = new Grid { Height = 32 };
        var searchBtn = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = 28, Height = 28, HorizontalAlignment = HorizontalAlignment.Right, ToolTip = "Search",
            Content = new TextBlock { Text = Icons.Glyph("search"), Style = (Style)FindResource("Style.Icon"), FontSize = 15 },
        };
        searchBtn.Click += (_, _) => { _searchBox.Visibility = _searchBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; if (_searchBox.Visibility == Visibility.Visible) _search.Focus(); else { _search.Text = ""; } };
        tabRow.Children.Add(_tabs); tabRow.Children.Add(searchBtn);
        left.Children.Add(tabRow);

        _search = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White, CaretBrush = Brushes.White, FontFamily = (FontFamily)FindResource("Font.Ui"), FontSize = 13, Padding = new Thickness(10, 6, 10, 6) };
        _search.TextChanged += (_, _) => RebuildList();
        _searchBox = new Border { CornerRadius = new CornerRadius(10), Background = (Brush)FindResource("Brush.Card"), Child = _search, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4, 0, 4) };
        Grid.SetRow(_searchBox, 1);
        left.Children.Add(_searchBox);

        var scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
        Grid.SetRow(scroll, 2);
        left.Children.Add(scroll);
        root.Children.Add(left);

        var divider = new Border { Width = 1, Background = (Brush)FindResource("Brush.Divider"), HorizontalAlignment = HorizontalAlignment.Left };
        Grid.SetColumn(divider, 1);
        root.Children.Add(divider);
        _detail.Margin = new Thickness(18, 0, 6, 0);
        Grid.SetColumn(_detail, 1);
        root.Children.Add(_detail);
        Content = root;

        Loaded += (_, _) => { _host.ClipboardMonitor!.Captured += Refresh; Refresh(); };
        Unloaded += (_, _) => { if (_host.ClipboardMonitor is { } m) m.Captured -= Refresh; };
    }

    private void Refresh() { BuildTabs(); RebuildList(); ShowDetail(); }

    private void BuildTabs()
    {
        _tabs.Children.Clear();
        void Tab(string label, ClipKind? kind)
        {
            var selected = _tab == kind;
            var count = _host.Clipboard.Count(kind);
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, FontSize = 13, Foreground = selected ? Brushes.White : (Brush)FindResource("Brush.TextDim") });
            panel.Children.Add(new TextBlock { Text = count.ToString(), Style = (Style)FindResource("Style.TextDim"), FontSize = 11, Margin = new Thickness(4, 2, 0, 0) });
            var tab = new Border { Child = panel, Padding = new Thickness(0, 4, 0, 6), Margin = new Thickness(0, 0, 16, 0), Cursor = Cursors.Hand, BorderBrush = selected ? (Brush)FindResource("Brush.Accent") : Brushes.Transparent, BorderThickness = new Thickness(0, 0, 0, 2), Background = Brushes.Transparent };
            tab.MouseLeftButtonUp += (_, _) => { _tab = kind; Refresh(); };
            _tabs.Children.Add(tab);
        }
        Tab("All", null); Tab("Text", ClipKind.Text); Tab("Images", ClipKind.Image); Tab("Links", ClipKind.Link);
    }

    private void RebuildList()
    {
        _list.Children.Clear();
        var entries = _host.Clipboard.Filter(_tab, _search.Text);
        if (entries.Count == 0)
        {
            _list.Children.Add(new TextBlock { Text = _search.Text.Length > 0 ? "No matches" : "Nothing copied yet", Style = (Style)FindResource("Style.TextDim"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) });
            return;
        }
        string? lastGroup = null;
        foreach (var e in entries)
        {
            var group = e.Pinned ? "Pinned" : DayLabel(e.Time);
            if (group != lastGroup)
            {
                _list.Children.Add(new TextBlock { Text = group, Style = (Style)FindResource("Style.TextDim"), FontSize = 12, Margin = new Thickness(4, 8, 0, 4) });
                lastGroup = group;
            }
            _list.Children.Add(Row(e));
        }
    }

    private UIElement Row(ClipEntry e)
    {
        UIElement lead = e.Kind == ClipKind.Image && Thumb(e) is { } bmp
            ? new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill } }
            : new Border
            {
                Width = 40, Height = 40, CornerRadius = new CornerRadius(8), Background = (Brush)FindResource("Brush.Control"),
                Child = new TextBlock { Text = Icons.Glyph(e.Kind == ClipKind.Link ? "link" : e.Kind == ClipKind.Image ? "image" : "document"), Style = (Style)FindResource("Style.Icon"), FontSize = 18 },
            };
        var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = Label(e), Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = Sub(e), Style = (Style)FindResource("Style.TextDim"), FontSize = 11 });
        var line = new Grid();
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.Children.Add(lead);
        Grid.SetColumn(text, 1); line.Children.Add(text);
        var id = e.Id;
        var row = new Border
        {
            Child = line, Padding = new Thickness(8, 6, 8, 6), CornerRadius = new CornerRadius(10), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 2),
            Background = id == _selectedId ? (Brush)FindResource("Brush.Card") : Brushes.Transparent,
        };
        row.MouseLeftButtonUp += (_, _) => { _selectedId = id; RebuildList(); ShowDetail(); };
        return row;
    }

    private BitmapImage? Thumb(ClipEntry e)
    {
        try
        {
            var path = _host.ClipboardMonitor!.ImagePath(e.ImageFile);
            if (!File.Exists(path)) return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 160;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or InvalidOperationException) { return null; }
    }

    private static string Label(ClipEntry e)
    {
        if (e.Kind == ClipKind.Image) return "Image";
        var first = e.Text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
        return first.Length > 60 ? first[..60] : first;
    }

    private static string Sub(ClipEntry e) => e.Kind switch
    {
        ClipKind.Image => $"{e.Width} × {e.Height} · {Ago(e.Time)}",
        _ => $"{ClipboardText.WordCount(e.Text)} word{(ClipboardText.WordCount(e.Text) == 1 ? "" : "s")} · {Ago(e.Time)}",
    };

    internal static string Ago(DateTime t)
    {
        var d = DateTime.Now - t;
        if (d.TotalSeconds < 45) return "Just now";
        if (d.TotalMinutes < 60) return $"{Math.Max(1, (int)d.TotalMinutes)}m ago";
        if (d.TotalHours < 24) return $"{(int)d.TotalHours}h ago";
        return t.ToString("MMM d", CultureInfo.InvariantCulture);
    }

    private static string DayLabel(DateTime t)
    {
        if (t.Date == DateTime.Today) return "Today";
        if (t.Date == DateTime.Today.AddDays(-1)) return "Yesterday";
        return t.ToString("MMM d", CultureInfo.InvariantCulture);
    }

    private void ShowDetail()
    {
        _detail.Children.Clear();
        var entry = _selectedId is null ? null : _host.Clipboard.All.FirstOrDefault(e => e.Id == _selectedId);
        if (entry is null)
        {
            _detail.Children.Add(new TextBlock { Text = "Select an item to see it here", Style = (Style)FindResource("Style.TextDim"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            return;
        }
        _detail.RowDefinitions.Clear();
        _detail.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _detail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _detail.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var kind = entry.Kind switch { ClipKind.Image => "Image", ClipKind.Link => "Link", _ => "Text" };
        var head = new Grid { Height = 32 };
        head.Children.Add(new TextBlock { Text = $"{kind} · {Ago(entry.Time)}", Style = (Style)FindResource("Style.TextDim"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        var icons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var id = entry.Id;
        icons.Children.Add(IconBtn("pin", entry.Pinned ? "Unpin" : "Pin", () => { _host.Clipboard.SetPinned(id, !entry.Pinned); Refresh(); }, entry.Pinned));
        icons.Children.Add(IconBtn("trash", "Delete", () => { _host.Clipboard.Delete(id); _selectedId = null; Refresh(); }, false));
        head.Children.Add(icons);
        _detail.Children.Add(head);

        UIElement body;
        if (entry.Kind == ClipKind.Image && Thumb(entry) is not null)
        {
            var full = new BitmapImage();
            full.BeginInit(); full.CacheOption = BitmapCacheOption.OnLoad; full.UriSource = new Uri(_host.ClipboardMonitor!.ImagePath(entry.ImageFile)); full.EndInit(); full.Freeze();
            body = new Image { Source = full, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 4) };
        }
        else
        {
            body = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                Content = new TextBlock { Text = entry.Text, Style = (Style)FindResource("Style.Text"), FontSize = 14, TextWrapping = TextWrapping.Wrap },
            };
        }
        Grid.SetRow(body, 1);
        _detail.Children.Add(body);

        var foot = new Grid { Height = 40, Margin = new Thickness(0, 6, 0, 0) };
        var meta = entry.Kind == ClipKind.Image
            ? $"{entry.Width} × {entry.Height} px"
            : $"{ClipboardText.WordCount(entry.Text)} word{(ClipboardText.WordCount(entry.Text) == 1 ? "" : "s")} · {entry.Text.Length} chars";
        foot.Children.Add(new TextBlock { Text = meta, Style = (Style)FindResource("Style.TextDim"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        if (entry.Kind == ClipKind.Link)
        {
            var open = new Button
            {
                Style = (Style)FindResource("Style.RoundButton"), Width = 70, Height = 34, Background = (Brush)FindResource("Brush.Control"), Margin = new Thickness(0, 0, 8, 0),
                Content = new TextBlock { Text = "Open", Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium },
            };
            open.Click += (_, _) => Launcher.OpenUrl(entry.Text.Trim());
            actions.Children.Add(open);
        }
        var copy = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = 92, Height = 34, Background = (Brush)FindResource("Brush.Primary"),
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new TextBlock { Text = Icons.Glyph("copy"), Style = (Style)FindResource("Style.Icon"), FontSize = 14, Margin = new Thickness(0, 0, 6, 0) },
                new TextBlock { Text = "Copy", Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.SemiBold },
            } },
        };
        copy.Click += (_, _) => _host.ClipboardMonitor!.CopyBack(entry);
        actions.Children.Add(copy);
        foot.Children.Add(actions);
        Grid.SetRow(foot, 2);
        _detail.Children.Add(foot);
    }

    private Button IconBtn(string icon, string tip, Action click, bool active)
    {
        var b = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = 30, Height = 30, ToolTip = tip,
            Content = new TextBlock { Text = Icons.Glyph(icon), Style = (Style)FindResource("Style.Icon"), FontSize = 16, Foreground = active ? (Brush)FindResource("Brush.Accent") : Brushes.White },
        };
        b.Click += (_, _) => click();
        return b;
    }
}
