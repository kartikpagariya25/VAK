using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VAK.App.Theme;

namespace VAK.App.Settings;

/// <summary>The Settings window: a sidebar of sections, a search box (Ctrl+K) and one scrolling page per section.</summary>
public sealed partial class SettingsWindow : Window
{
    private static readonly (string Id, string Title, string Icon, string Color)[] Sections =
    {
        ("general", "General", "settings", "#5A5A60"),
        ("notch", "Notch", "open", "#3A3A3F"),
        ("appearance", "Appearance", "moon", "#6B5BE6"),
        ("modules", "Modules", "add-circle", "#2F7CF6"),
        ("live", "Live Activities", "message", "#FF453A"),
        ("privacy", "Privacy", "pin", "#32ADE6"),
        ("about-updates", "License & Updates", "tag", "#30D158"),
        ("about", "About", "check", "#2F7CF6"),
    };

    private readonly AppHost _host;
    private readonly Dictionary<string, FrameworkElement> _pages = new();
    private readonly Dictionary<string, Border> _navItems = new();
    private readonly List<SettingEntry> _index = new();
    private ScrollViewer _scroll = null!;
    private TextBox _search = null!;
    private readonly StackPanel _results = new();
    private string _current = "general";

    public SettingsWindow(AppHost host)
    {
        _host = host;
        Title = "VAK Settings";
        Width = 1100; Height = 780; MinWidth = 900; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Background = (Brush)FindResource("Brush.Panel");
        FontFamily = (FontFamily)FindResource("Font.Ui");

        BuildUi();
        _host.ThemeChanged += OnThemeChanged;
        Closed += (_, _) => _host.ThemeChanged -= OnThemeChanged;
        PreviewKeyDown += OnKey;
        Go("general");
    }

    private void OnThemeChanged() => Dispatcher.BeginInvoke(() =>
    {
        var page = _current;
        _pages.Clear(); _navItems.Clear(); _index.Clear();
        Background = (Brush)FindResource("Brush.Panel");
        BuildUi();
        Go(page);
    });

    private void BuildUi()
    {
        _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Padding = new Thickness(46, 34, 46, 0) };
        _search = new TextBox();
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var side = BuildSidebar();
        var main = new Grid();
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        main.Children.Add(BuildTitleBar());
        Grid.SetRow(_scroll, 1); main.Children.Add(_scroll);
        Grid.SetColumn(main, 1);
        root.Children.Add(side); root.Children.Add(main);
        Content = new Border { BorderBrush = (Brush)FindResource("Brush.Divider"), BorderThickness = new Thickness(1), Child = root };

        BuildPages();
    }

    private UIElement BuildTitleBar()
    {
        var bar = new Grid { Background = Brushes.Transparent };
        bar.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 1) DragMove(); };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(TitleButton("minus", "Minimize", () => WindowState = WindowState.Minimized));
        buttons.Children.Add(TitleButton("close", "Close", Close));
        bar.Children.Add(buttons);
        return bar;
    }

    private Button TitleButton(string icon, string name, Action click)
    {
        var b = new Button
        {
            Width = 46, Height = 38, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Content = new TextBlock { Text = Icons.Glyph(icon), Style = (Style)FindResource("Style.Icon"), FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center },
        };
        b.Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        b.Click += (_, _) => click();
        System.Windows.Automation.AutomationProperties.SetName(b, name);
        return b;
    }

    private UIElement BuildSidebar()
    {
        var side = new Grid { Background = (Brush)FindResource("Brush.Card") };
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        side.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var top = new StackPanel { Margin = new Thickness(20, 22, 20, 0) };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
        brand.Children.Add(new Image { Source = (ImageSource)FindResource("Brand.Mark"), Width = 48, Height = 48 });
        var names = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(SettingsKit.Text("VAK", size: 20, weight: FontWeights.Bold));
        names.Children.Add(SettingsKit.Text("Free and open source", "Style.TextDim", 13));
        brand.Children.Add(names);
        top.Children.Add(brand);

        _search.Background = Brushes.Transparent; _search.BorderThickness = new Thickness(0); _search.FontSize = 15;
        _search.Foreground = (Brush)FindResource("Brush.Text"); _search.CaretBrush = (Brush)FindResource("Brush.Text");
        _search.VerticalContentAlignment = VerticalAlignment.Center; _search.Height = 34; _search.Width = 170; _search.ToolTip = "Search settings (Ctrl+K)";
        System.Windows.Automation.AutomationProperties.SetName(_search, "Search settings");
        _search.TextChanged += (_, _) => ShowSearch();
        var searchRow = new Grid();
        searchRow.Children.Add(new TextBlock { Text = Icons.Glyph("search"), Style = (Style)FindResource("Style.Icon"), FontSize = 14, Foreground = (Brush)FindResource("Brush.TextDim"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Left });
        _search.Margin = new Thickness(36, 0, 0, 0); _search.HorizontalAlignment = HorizontalAlignment.Left;
        searchRow.Children.Add(_search);
        searchRow.Children.Add(new Border { CornerRadius = new CornerRadius(6), Background = (Brush)FindResource("Brush.Control"), Padding = new Thickness(6, 2, 6, 2), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), Child = SettingsKit.Text("Ctrl K", "Style.TextDim", 11, wrap: false) });
        top.Children.Add(new Border { CornerRadius = new CornerRadius(12), Background = (Brush)FindResource("Brush.Panel"), Child = searchRow, Height = 40, Margin = new Thickness(0, 0, 0, 12) });
        side.Children.Add(top);

        var nav = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };
        foreach (var (id, title, icon, color) in Sections)
        {
            if (id is "live" or "about-updates") nav.Children.Add(new Border { Height = 1, Background = (Brush)FindResource("Brush.Divider"), Margin = new Thickness(10, 10, 10, 10) });
            nav.Children.Add(NavItem(id, title, icon, color));
        }
        var navScroll = new ScrollViewer { Content = nav, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Margin = new Thickness(0, 90, 0, 0) };
        Grid.SetRow(navScroll, 0); Grid.SetRowSpan(navScroll, 2);
        navScroll.VerticalAlignment = VerticalAlignment.Top;
        // the nav list sits under the brand and search block
        navScroll.Margin = new Thickness(0, 150, 0, 0);
        side.Children.Add(navScroll);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 20, 18) };
        footer.Children.Add(Link("Help", "https://github.com/kartikpagariya25/VAK#readme"));
        footer.Children.Add(SettingsKit.Text("  ·  ", "Style.TextDim", 13, wrap: false));
        footer.Children.Add(Link("Feedback", "https://github.com/kartikpagariya25/VAK/issues"));
        footer.Children.Add(SettingsKit.Text("  ·  v" + Version(), "Style.TextDim", 13, wrap: false));
        Grid.SetRow(footer, 2); side.Children.Add(footer);
        return side;
    }

    public static string Version() => Assembly.GetExecutingAssembly().GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";

    private TextBlock Link(string text, string url)
    {
        var t = SettingsKit.Text(text, "Style.TextDim", 13, wrap: false);
        t.Cursor = Cursors.Hand;
        t.MouseLeftButtonUp += (_, _) => OpenUrl(url);
        t.MouseEnter += (_, _) => t.Foreground = (Brush)FindResource("Brush.Text");
        t.MouseLeave += (_, _) => t.Foreground = (Brush)FindResource("Brush.TextDim");
        return t;
    }

    public static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    private Border NavItem(string id, string title, string icon, string color)
    {
        var tile = new Border
        {
            Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(ThemeService.Parse(color, Colors.Gray)),
            Child = new TextBlock { Text = Icons.Glyph(icon), Style = (Style)FindResource("Style.Icon"), FontSize = 16, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(tile);
        row.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("Style.Text"), FontSize = 16, FontWeight = FontWeights.Medium, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        var item = new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 1, 0, 1), Child = row, Cursor = Cursors.Hand, Background = Brushes.Transparent };
        item.MouseLeftButtonUp += (_, _) => { _search.Text = ""; Go(id); };
        System.Windows.Automation.AutomationProperties.SetName(item, title);
        System.Windows.Automation.AutomationProperties.SetAutomationId(item, "nav-" + id);
        _navItems[id] = item;
        return item;
    }

    // ---------- navigation ----------

    public void Go(string id)
    {
        if (id == "updates") id = "about-updates";
        if (!_pages.ContainsKey(id)) id = "general";
        _current = id;
        foreach (var (key, item) in _navItems) item.Background = key == id ? (Brush)FindResource("Brush.Control") : Brushes.Transparent;
        _scroll.Content = _pages[id];
        _scroll.ScrollToTop();
    }

    private void ShowSearch()
    {
        var q = _search.Text.Trim();
        if (q.Length == 0) { Go(_current); return; }
        _results.Children.Clear();
        var hits = _index.Where(e => e.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || e.Hint.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(12).ToList();
        var heading = SettingsKit.Text(hits.Count == 0 ? $"No settings match \"{q}\"" : "Results", "Style.TextDim", 14);
        heading.Margin = new Thickness(0, 0, 0, 12);
        _results.Children.Add(heading);
        foreach (var hit in hits)
        {
            var h = hit;
            var page = Sections.First(s => s.Id == h.Page).Title;
            var line = new StackPanel();
            line.Children.Add(SettingsKit.Text(h.Title, size: 16, weight: FontWeights.SemiBold));
            line.Children.Add(SettingsKit.Text($"{page} · {h.Hint}", "Style.TextDim", 13));
            var item = new Border { Background = (Brush)FindResource("Brush.Card"), CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 10, 16, 10), Margin = new Thickness(0, 0, 0, 8), Cursor = Cursors.Hand, Child = line };
            item.MouseLeftButtonUp += (_, _) => { _search.Text = ""; Go(h.Page); Dispatcher.BeginInvoke(() => h.Element.BringIntoView(), System.Windows.Threading.DispatcherPriority.Loaded); };
            _results.Children.Add(item);
        }
        _scroll.Content = new StackPanel { Children = { _results } };
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control) { _search.Focus(); _search.SelectAll(); e.Handled = true; return; }
        if (e.Key == Key.Escape && _search.Text.Length > 0 && !_capturing) { _search.Text = ""; e.Handled = true; return; }
        CaptureHotkey(e);
    }

    private void BuildPages()
    {
        void Add(string id, Func<SettingsKit, StackPanel> build)
        {
            var kit = new SettingsKit(id);
            _pages[id] = build(kit);
            _index.AddRange(kit.Index);
        }
        Add("general", BuildGeneral);
        Add("notch", BuildNotch);
        Add("appearance", BuildAppearance);
        Add("modules", BuildModules);
        Add("live", BuildLive);
        Add("privacy", BuildPrivacy);
        Add("about-updates", BuildUpdates);
        Add("about", BuildAbout);
    }

    /// <summary>Saves, then tells the notch to re-apply (theme, size, style and so on).</summary>
    private void Changed() => _host.Apply();
}
