using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VAK.App.Controls;
using VAK.App.Theme;
using VAK.Core.ScreenTime;

namespace VAK.App.Pages;

public sealed class ScreenTimePage : UserControl
{
    private readonly AppHost _host;
    private readonly Grid _root = new();

    public ScreenTimePage(AppHost host)
    {
        _host = host;
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Content = _root;
        Loaded += (_, _) => { _host.ScreenTimeTracker!.Updated += OnTick; Render(); };
        Unloaded += (_, _) => { if (_host.ScreenTimeTracker is { } t) t.Updated -= OnTick; };
    }

    private int _tickCount;
    private void OnTick() { if (++_tickCount % 5 == 0) Render(); } // the numbers only change by seconds

    private TextBlock T(string text, string style = "Style.Text", double size = 13, FontWeight? weight = null)
    {
        var t = new TextBlock { Text = text, Style = (Style)FindResource(style), FontSize = size };
        if (weight is { } w) t.FontWeight = w;
        return t;
    }

    private void Render()
    {
        _root.Children.Clear();
        var log = _host.ScreenTime;
        var today = DateTime.Now;

        // left column: today card + last 7 days card
        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        left.Children.Add(TodayCard(log, today));
        var week = WeekCard(log);
        Grid.SetRow(week, 1);
        left.Children.Add(week);
        _root.Children.Add(left);

        var right = ByAppCard(log, today);
        Grid.SetColumn(right, 1);
        _root.Children.Add(right);
    }

    private UIElement TodayCard(ScreenTimeLog log, DateTime today)
    {
        var limit = _host.Settings.ScreenTimeLimitMinutes;
        var total = log.TotalSeconds(today);
        var remaining = log.RemainingSeconds(today, limit);

        var grid = new Grid { Height = 96 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var a = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        a.Children.Add(T(DurationFormatter.Short(total), "Style.Big", 26));
        a.Children.Add(T("Screen time", "Style.TextDim", 11));
        grid.Children.Add(a);

        var ring = new RingGauge { Width = 46, Height = 46, Thickness = 5, Arc = Brushes.White, Value = limit <= 0 ? 0 : 100 * remaining / (limit * 60.0), VerticalAlignment = VerticalAlignment.Center };
        var b = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        b.Children.Add(ring);
        var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(T(DurationFormatter.Short(remaining), "Style.Big", 22));
        var limitLine = new StackPanel { Orientation = Orientation.Horizontal };
        limitLine.Children.Add(T($"left of {DurationFormatter.Short(limit * 60.0)} limit", "Style.TextDim", 11));
        var edit = new TextBlock { Text = Icons.Glyph("edit"), Style = (Style)FindResource("Style.Icon"), FontSize = 12, Margin = new Thickness(6, 0, 0, 0), Cursor = Cursors.Hand, ToolTip = "Change the daily limit", Foreground = (Brush)FindResource("Brush.TextDim") };
        edit.MouseLeftButtonUp += (_, _) => EditLimit(edit);
        limitLine.Children.Add(edit);
        text.Children.Add(limitLine);
        b.Children.Add(text);
        Grid.SetColumn(b, 1);
        grid.Children.Add(b);

        var c = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        c.Children.Add(T(log.AppsUsed(today).ToString(), "Style.Big", 26));
        c.Children.Add(T("Apps used", "Style.TextDim", 11));
        Grid.SetColumn(c, 2);
        grid.Children.Add(c);

        return new Border { Style = (Style)FindResource("Style.Card"), Child = grid, Margin = new Thickness(5, 5, 5, 5), Padding = new Thickness(10) };
    }

    private void EditLimit(FrameworkElement anchor)
    {
        var box = new TextBox
        {
            Text = _host.Settings.ScreenTimeLimitMinutes.ToString(CultureInfo.InvariantCulture), Width = 90, Background = (Brush)FindResource("Brush.Control"), Foreground = Brushes.White,
            CaretBrush = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6), FontSize = 13,
        };
        var popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 4 };
        var panel = new StackPanel();
        panel.Children.Add(T("Daily limit (minutes)", "Style.TextDim", 11));
        panel.Children.Add(box);
        popup.Child = new Border { Padding = new Thickness(10), CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)), BorderBrush = (Brush)FindResource("Brush.Control"), BorderThickness = new Thickness(1), Child = panel };
        box.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            if (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) && m is >= 5 and <= 1440)
            {
                _host.Settings.ScreenTimeLimitMinutes = m;
                _host.Save();
                popup.IsOpen = false;
                Render();
            }
        };
        popup.IsOpen = true;
        box.Focus(); box.SelectAll();
    }

    private UIElement WeekCard(ScreenTimeLog log)
    {
        var days = log.Last7Days();
        var max = Math.Max(days.Max(d => d.Seconds), 60);
        var avg = log.AverageSeconds();

        var cols = new UniformGrid { Columns = 7, VerticalAlignment = VerticalAlignment.Stretch };
        foreach (var d in days)
        {
            var isToday = d.Date == DateTime.Now.Date;
            var col = new Grid();
            col.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            col.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            col.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var value = T(d.Seconds > 0 ? DurationFormatter.Short(d.Seconds) : "—", "Style.TextDim", 10);
            value.HorizontalAlignment = HorizontalAlignment.Center;
            var barHost = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            var bar = new Border
            {
                Width = 20, CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center,
                Height = Math.Max(4, 60 * d.Seconds / max), Background = isToday ? Brushes.White : (Brush)FindResource("Brush.Control"),
            };
            barHost.Children.Add(bar);
            Grid.SetRow(barHost, 1);
            var day = T(d.Date.ToString("ddd", CultureInfo.InvariantCulture), "Style.TextDim", 10);
            day.HorizontalAlignment = HorizontalAlignment.Center;
            if (isToday) day.Foreground = Brushes.White;
            Grid.SetRow(day, 2);
            col.Children.Add(value); col.Children.Add(barHost); col.Children.Add(day);
            cols.Children.Add(col);
        }

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var head = new Grid();
        head.Children.Add(new CardHeader("calendar", "Last 7 days"));
        var avgText = T($"avg {DurationFormatter.Short(avg)}", "Style.TextDim", 11);
        avgText.HorizontalAlignment = HorizontalAlignment.Right;
        head.Children.Add(avgText);
        grid.Children.Add(head);
        Grid.SetRow(cols, 1);
        grid.Children.Add(cols);
        return new Border { Style = (Style)FindResource("Style.Card"), Child = grid, Margin = new Thickness(5) };
    }

    private UIElement ByAppCard(ScreenTimeLog log, DateTime today)
    {
        var apps = log.TopApps(today, 6);
        var list = new StackPanel();
        if (apps.Count == 0)
            list.Children.Add(T("No app time recorded yet", "Style.TextDim", 12));
        foreach (var a in apps)
        {
            var icon = _host.ScreenTimeTracker?.IconFor(a.App);
            UIElement lead = icon is null
                ? new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(6), Background = (Brush)FindResource("Brush.Control") }
                : new Image { Source = icon, Width = 24, Height = 24 };
            var name = T(a.Name, "Style.Text", 13, FontWeights.Medium);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            var time = T(DurationFormatter.Short(a.Seconds), "Style.Text", 12, FontWeights.SemiBold);
            time.HorizontalAlignment = HorizontalAlignment.Right;
            var barGrid = new Grid { Height = 4, Margin = new Thickness(0, 4, 0, 0) };
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(a.Percent, 3), GridUnitType.Star) });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(100 - a.Percent, 0), GridUnitType.Star) });
            barGrid.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = Brushes.White });
            var pct = T(a.Percent + "%", "Style.TextDim", 10);
            pct.HorizontalAlignment = HorizontalAlignment.Right;

            var info = new Grid { Margin = new Thickness(8, 0, 0, 0) };
            info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            info.Children.Add(name); info.Children.Add(time);
            var second = new Grid();
            second.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            second.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            second.Children.Add(barGrid);
            Grid.SetColumn(pct, 1); pct.Margin = new Thickness(8, 0, 0, 0); second.Children.Add(pct);
            Grid.SetRow(second, 1);
            info.Children.Add(second);

            var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(lead);
            Grid.SetColumn(info, 1); row.Children.Add(info);
            list.Children.Add(row);
        }

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new CardHeader("chart", "By app"));
        var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Margin = new Thickness(0, 6, 0, 0) };
        Grid.SetRow(scroll, 1);
        grid.Children.Add(scroll);
        return new Border { Style = (Style)FindResource("Style.Card"), Child = grid, Margin = new Thickness(5) };
    }
}
