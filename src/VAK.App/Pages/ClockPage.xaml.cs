using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VAK.App.Controls;
using VAK.Core.Clock;
using VAK.Core.Settings;

namespace VAK.App.Pages;

public partial class ClockPage : UserControl
{
    private static readonly string[] DateStyles = { "full", "short", "compact" };

    private readonly AppHost _host;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly SegmentedControl _styleControl = new("Digital", "Analog");
    private readonly SegmentedControl _dateStyleControl = new("Full", "Short", "Compact");
    private bool _loading;

    public ClockPage(AppHost host)
    {
        InitializeComponent();
        _host = host;
        ClockTitle.Children.Add(new CardHeader("clock", "Clock"));
        WorldTitle.Children.Add(new CardHeader("globe", "World clocks"));
        StyleHost.Content = _styleControl;
        DateStyleHost.Content = _dateStyleControl;

        _loading = true;
        var clock = _host.Settings.Clock;
        _styleControl.SelectedIndex = clock.Style == "analog" ? 1 : 0;
        _dateStyleControl.SelectedIndex = Math.Max(0, Array.IndexOf(DateStyles, clock.DateStyle));
        SecondsToggle.IsChecked = clock.ShowSeconds;
        DateToggle.IsChecked = clock.ShowDate;
        _loading = false;

        _styleControl.SelectionChanged += i => Change(() => clock.Style = i == 1 ? "analog" : "digital");
        _dateStyleControl.SelectionChanged += i => Change(() => clock.DateStyle = DateStyles[i]);
        SecondsToggle.Click += (_, _) => Change(() => clock.ShowSeconds = SecondsToggle.IsChecked == true);
        DateToggle.Click += (_, _) => Change(() => clock.ShowDate = DateToggle.IsChecked == true);
        FormatChange.MouseLeftButtonUp += (_, _) => Change(() => _host.Settings.Use24Hour = !_host.Settings.Use24Hour);
        AddCity.MouseLeftButtonUp += (_, _) => ShowCityPicker();

        _timer.Tick += (_, _) => Render();
        Loaded += (_, _) => { _timer.Start(); Render(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private void Change(Action apply)
    {
        if (_loading) return;
        apply();
        _host.Save();
        Render();
    }

    private void Render()
    {
        var settings = _host.Settings;
        var clock = settings.Clock;
        var now = DateTimeOffset.Now;
        var local = TimeZoneInfo.Local;
        var analog = clock.Style == "analog";

        DigitalBlock.Visibility = analog ? Visibility.Collapsed : Visibility.Visible;
        AnalogBlock.Visibility = analog ? Visibility.Visible : Visibility.Collapsed;
        BigTime.Text = ClockFormatter.Time(now, local, settings.Use24Hour, clock.ShowSeconds);
        var date = ClockFormatter.Date(now, clock.DateStyle);
        var zone = $"{ClockFormatter.Abbreviation(local, now)} ({ClockFormatter.UtcOffsetLabel(local, now)})";
        DateText.Text = clock.ShowDate ? date : "";
        DateText.Visibility = clock.ShowDate ? Visibility.Visible : Visibility.Collapsed;
        ZoneText.Text = zone;
        Analog.ShowSeconds = clock.ShowSeconds;
        Analog.Time = now.LocalDateTime;
        AnalogDate.Text = clock.ShowDate ? date : "";
        AnalogZone.Text = zone;
        FormatText.Text = settings.Use24Hour ? "24-hour" : "12-hour";

        Rows.Children.Clear();
        Rows.Children.Add(BuildRow("Local", ClockFormatter.Abbreviation(local, now), ClockFormatter.Time(now, local, settings.Use24Hour, false), null));
        foreach (var entry in settings.WorldClocks.ToList())
        {
            var tz = ClockFormatter.TryFind(entry.TimeZoneId);
            if (tz is null) continue; // a zone that no longer exists on this machine is skipped
            var offset = ClockFormatter.OffsetLabel(now, tz, local);
            var sub = ClockFormatter.Abbreviation(tz, now) + (offset.Length > 0 ? " · " + offset : "");
            Rows.Children.Add(BuildRow(entry.Label, sub, ClockFormatter.Time(now, tz, settings.Use24Hour, false), entry));
        }
    }

    private UIElement BuildRow(string label, string sub, string time, WorldClockEntry? removable)
    {
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, FontSize = 14 });
        left.Children.Add(new TextBlock { Text = sub, Style = (Style)FindResource("Style.TextDim"), FontSize = 11 });

        var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        var remove = new TextBlock
        {
            Text = "×", Style = (Style)FindResource("Style.TextDim"), FontSize = 16, Margin = new Thickness(0, 0, 10, 2),
            Cursor = Cursors.Hand, Visibility = Visibility.Collapsed, ToolTip = "Remove",
        };
        if (removable is not null)
            remove.MouseLeftButtonUp += (_, _) => { _host.Settings.WorldClocks.Remove(removable); _host.Save(); Render(); };
        right.Children.Add(remove);
        right.Children.Add(new TextBlock { Text = time, Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.SemiBold, FontSize = 14 });

        var grid = new Grid { Margin = new Thickness(0, 5, 0, 5), Background = Brushes.Transparent };
        grid.Children.Add(left);
        grid.Children.Add(right);
        if (removable is not null)
        {
            grid.MouseEnter += (_, _) => remove.Visibility = Visibility.Visible;
            grid.MouseLeave += (_, _) => remove.Visibility = Visibility.Collapsed;
        }
        return new Border { Child = grid, BorderBrush = (Brush)FindResource("Brush.Divider"), BorderThickness = new Thickness(0, 0, 0, 1) };
    }

    private void ShowCityPicker()
    {
        var results = new StackPanel();
        var search = new TextBox
        {
            Background = (Brush)FindResource("Brush.Control"), Foreground = Brushes.White, CaretBrush = Brushes.White,
            BorderThickness = new Thickness(0), Padding = new Thickness(10, 7, 10, 7), FontSize = 13,
            FontFamily = (FontFamily)FindResource("Font.Ui"),
        };
        var popup = new Popup
        {
            PlacementTarget = AddCity, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true,
            HorizontalOffset = -150, VerticalOffset = 6,
        };

        void Fill()
        {
            results.Children.Clear();
            var q = search.Text.Trim();
            var zones = TimeZoneInfo.GetSystemTimeZones()
                .Where(z => q.Length == 0 || z.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) || z.Id.Contains(q, StringComparison.OrdinalIgnoreCase))
                .Take(40);
            foreach (var z in zones)
            {
                var name = z.DisplayName;
                var cut = name.IndexOf(") ", StringComparison.Ordinal);
                var label = cut >= 0 ? name[(cut + 2)..] : name;
                var row = new TextBlock { Text = z.DisplayName, Style = (Style)FindResource("Style.Text"), Padding = new Thickness(10, 6, 10, 6), Cursor = Cursors.Hand, TextTrimming = TextTrimming.CharacterEllipsis };
                row.MouseEnter += (_, _) => row.Background = (Brush)FindResource("Brush.Control");
                row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
                var id = z.Id;
                row.MouseLeftButtonUp += (_, _) =>
                {
                    popup.IsOpen = false;
                    _host.Settings.WorldClocks.Add(new WorldClockEntry(label, id));
                    _host.Save();
                    Render();
                };
                results.Children.Add(row);
            }
        }
        search.TextChanged += (_, _) => Fill();
        Fill();

        var panel = new StackPanel();
        panel.Children.Add(search);
        panel.Children.Add(new ScrollViewer { MaxHeight = 190, Content = results, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Margin = new Thickness(0, 4, 0, 0) });
        popup.Child = new Border
        {
            Width = 320, Padding = new Thickness(6), CornerRadius = new CornerRadius(12), Child = panel,
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)),
            BorderBrush = (Brush)FindResource("Brush.Control"), BorderThickness = new Thickness(1),
        };
        popup.IsOpen = true;
        search.Focus();
    }
}
