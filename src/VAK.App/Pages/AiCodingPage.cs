using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VAK.App.Controls;
using VAK.App.Services;
using VAK.App.Theme;
using VAK.App.Widgets;
using VAK.Core.AiUsage;

namespace VAK.App.Pages;

public sealed class AiCodingPage : UserControl, IHeaderExtras
{
    private readonly AppHost _host;
    private readonly Grid _root = new();
    private IDisposable? _subscription;
    private AiAgent? _filter;

    public AiCodingPage(AppHost host)
    {
        _host = host;
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Content = _root;
        Loaded += (_, _) => { _subscription = _host.AiUsage.Subscribe(); _host.AiUsage.Changed += Render; Render(); };
        Unloaded += (_, _) => { _host.AiUsage.Changed -= Render; _subscription?.Dispose(); };
    }

    public UIElement CreateHeaderExtras()
    {
        var seg = new SegmentedControl("All", "Claude Code", "Codex") { SelectedIndex = 0 };
        seg.SelectionChanged += i => { _filter = i switch { 1 => AiAgent.Claude, 2 => AiAgent.Codex, _ => null }; Render(); };
        return seg;
    }

    private TextBlock T(string text, string style = "Style.Text", double size = 13, FontWeight? weight = null)
    {
        var t = new TextBlock { Text = text, Style = (Style)FindResource(style), FontSize = size };
        if (weight is { } w) t.FontWeight = w;
        return t;
    }

    private static string Money(double v) => "$" + v.ToString("0.00", CultureInfo.InvariantCulture);

    private void Render()
    {
        _root.Children.Clear();
        var events = _host.AiUsage.Events;
        var today = DateTime.Now;
        var day = AiUsageSummary.ForDay(events, today, _filter);

        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.25, GridUnitType.Star) });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        left.Children.Add(TodayCard(day, AiUsageSummary.Streak(events, today, _filter)));
        var hours = HoursCard(day);
        Grid.SetRow(hours, 1);
        left.Children.Add(hours);
        _root.Children.Add(left);

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.Children.Add(MonthCard(AiUsageSummary.MonthCost(events, today, _filter)));
        var agents = AgentsCard(events, today);
        Grid.SetRow(agents, 1);
        right.Children.Add(agents);
        Grid.SetColumn(right, 1);
        _root.Children.Add(right);
    }

    private UIElement TodayCard(DaySummary day, int streak)
    {
        var stack = new StackPanel();
        stack.Children.Add(new CardHeader("database", "Today"));

        var main = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
        var tokens = new StackPanel();
        var big = new StackPanel { Orientation = Orientation.Horizontal };
        big.Children.Add(T(AiUsageSummary.FormatTokens(day.Tokens), "Style.Big", 34));
        big.Children.Add(T("tokens", "Style.TextDim", 12));
        ((TextBlock)big.Children[1]).Margin = new Thickness(6, 0, 0, 5);
        ((TextBlock)big.Children[1]).VerticalAlignment = VerticalAlignment.Bottom;
        tokens.Children.Add(big);
        tokens.Children.Add(T($"≈ {Money(day.Cost)} API value today", "Style.TextDim", 11));
        main.Children.Add(tokens);

        var counts = new UniformGrid { Columns = 3 };
        counts.Children.Add(Count(day.Messages, "Messages"));
        counts.Children.Add(Count(day.ToolCalls, "Tool calls"));
        counts.Children.Add(Count(day.Sessions, "Sessions"));
        Grid.SetColumn(counts, 1);
        main.Children.Add(counts);
        stack.Children.Add(main);

        var foot = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        foot.Children.Add(new TextBlock { Text = Icons.Glyph("folder"), Style = (Style)FindResource("Style.Icon"), FontSize = 13, Foreground = (Brush)FindResource("Brush.TextDim"), Margin = new Thickness(0, 0, 6, 0) });
        foot.Children.Add(T(day.TopProject ?? "No project yet", "Style.TextDim", 12));
        foot.Children.Add(T("  ·  ", "Style.TextDim", 12));
        foot.Children.Add(T(streak == 0 ? "No streak yet" : $"{streak}-day streak", "Style.TextDim", 12));
        stack.Children.Add(foot);
        return new Border { Style = (Style)FindResource("Style.Card"), Child = stack, Margin = new Thickness(5, 5, 5, 5) };
    }

    private UIElement Count(int n, string label)
    {
        var s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var v = T(n.ToString(CultureInfo.InvariantCulture), "Style.Big", 22);
        v.HorizontalAlignment = HorizontalAlignment.Center;
        var l = T(label, "Style.TextDim", 11);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        s.Children.Add(v); s.Children.Add(l);
        return s;
    }

    private UIElement HoursCard(DaySummary day)
    {
        var any = day.ActiveHours.Any(h => h > 0);
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(new CardHeader("chart", "Active hours"));

        var dots = new UniformGrid { Columns = 24, VerticalAlignment = VerticalAlignment.Center };
        for (var h = 0; h < 24; h++)
        {
            var active = day.ActiveHours[h] > 0;
            var isNow = h == DateTime.Now.Hour;
            dots.Children.Add(new Border
            {
                Width = 6, Height = active ? 24 : 4, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center,
                Background = active ? (Brush)FindResource("Brush.Accent") : isNow ? Brushes.White : (Brush)FindResource("Brush.Control"),
            });
        }
        var middle = new Grid();
        middle.Children.Add(dots);
        if (!any) middle.Children.Add(new TextBlock { Text = "No activity yet today", Style = (Style)FindResource("Style.TextDim"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0) });
        Grid.SetRow(middle, 1);
        grid.Children.Add(middle);

        var labels = new Grid();
        labels.Children.Add(T("12 AM", "Style.TextDim", 11));
        var six = T("6 AM", "Style.TextDim", 11); six.HorizontalAlignment = HorizontalAlignment.Center; six.Margin = new Thickness(0, 0, 90, 0);
        labels.Children.Add(six);
        var noon = T("12 PM", "Style.TextDim", 11); noon.HorizontalAlignment = HorizontalAlignment.Center; noon.Margin = new Thickness(40, 0, 0, 0);
        labels.Children.Add(noon);
        var evening = T("6 PM", "Style.TextDim", 11); evening.HorizontalAlignment = HorizontalAlignment.Right; evening.Margin = new Thickness(0, 0, 40, 0);
        labels.Children.Add(evening);
        Grid.SetRow(labels, 2);
        grid.Children.Add(labels);
        return new Border { Style = (Style)FindResource("Style.Card"), Child = grid, Margin = new Thickness(5) };
    }

    private UIElement MonthCard(double cost)
    {
        var budget = _host.Settings.AiBudgetMonthly;
        var stack = new StackPanel();
        var head = new Grid();
        head.Children.Add(new CardHeader("calendar", "This month"));
        var plan = T("View plan usage ↗", "Style.TextDim", 11);
        plan.HorizontalAlignment = HorizontalAlignment.Right; plan.Cursor = Cursors.Hand;
        plan.MouseLeftButtonUp += (_, _) => Launcher.OpenUrl("https://claude.ai/settings/usage");
        head.Children.Add(plan);
        stack.Children.Add(head);

        var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        line.Children.Add(T("≈ " + Money(cost), "Style.Big", 28));
        var set = T(budget > 0 ? $"of {Money(budget)} budget" : "Set budget", "Style.Text", 12, FontWeights.Medium);
        set.Margin = new Thickness(10, 0, 0, 4); set.VerticalAlignment = VerticalAlignment.Bottom; set.Cursor = Cursors.Hand;
        set.Foreground = (Brush)FindResource("Brush.Accent");
        set.MouseLeftButtonUp += (_, _) => EditBudget(set);
        line.Children.Add(set);
        stack.Children.Add(line);

        if (budget > 0)
        {
            var share = Math.Clamp(cost / budget, 0, 1);
            var bar = new Grid { Height = 5, Margin = new Thickness(0, 6, 0, 0) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(share, 0.01), GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1 - share, 0), GridUnitType.Star) });
            bar.Children.Add(new Border { CornerRadius = new CornerRadius(3), Background = (Brush)FindResource("Brush.Control") });
            var fill = new Border { CornerRadius = new CornerRadius(3), Background = share >= 1 ? (Brush)FindResource("Brush.Good") : (Brush)FindResource("Brush.Accent") };
            bar.Children.Add(fill);
            Grid.SetColumnSpan(bar.Children[0], 2);
            stack.Children.Add(bar);
        }
        stack.Children.Add(T("Estimated at API prices", "Style.TextDim", 11));
        ((TextBlock)stack.Children[^1]).Margin = new Thickness(0, 4, 0, 0);
        return new Border { Style = (Style)FindResource("Style.Card"), Child = stack, Margin = new Thickness(5, 5, 5, 5) };
    }

    private void EditBudget(FrameworkElement anchor)
    {
        var box = new TextBox
        {
            Text = _host.Settings.AiBudgetMonthly > 0 ? _host.Settings.AiBudgetMonthly.ToString("0.##", CultureInfo.InvariantCulture) : "", Width = 100,
            Background = (Brush)FindResource("Brush.Control"), Foreground = Brushes.White, CaretBrush = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6),
        };
        var popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 4 };
        var panel = new StackPanel();
        panel.Children.Add(T("Monthly budget in dollars (empty to clear)", "Style.TextDim", 11));
        panel.Children.Add(box);
        popup.Child = new Border { Padding = new Thickness(10), CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)), BorderBrush = (Brush)FindResource("Brush.Control"), BorderThickness = new Thickness(1), Child = panel };
        box.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            if (string.IsNullOrWhiteSpace(box.Text)) _host.Settings.AiBudgetMonthly = 0;
            else if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v is > 0 and < 1_000_000) _host.Settings.AiBudgetMonthly = v;
            else return;
            _host.Save(); popup.IsOpen = false; Render();
        };
        popup.IsOpen = true;
        box.Focus(); box.SelectAll();
    }

    private UIElement AgentsCard(IReadOnlyList<UsageEvent> events, DateTime today)
    {
        var stack = new StackPanel();
        stack.Children.Add(new CardHeader("person", "By agent"));
        var claude = AiUsageSummary.ForDay(events, today, AiAgent.Claude);
        var codex = AiUsageSummary.ForDay(events, today, AiAgent.Codex);
        var max = Math.Max(Math.Max(claude.Tokens, codex.Tokens), 1);
        stack.Children.Add(AgentRow("Claude Code", "bot", claude, max, (Brush)FindResource("Brush.Accent")));
        stack.Children.Add(AgentRow("Codex", "bot", codex, max, Brushes.White));
        return new Border { Style = (Style)FindResource("Style.Card"), Child = stack, Margin = new Thickness(5) };
    }

    private UIElement AgentRow(string name, string icon, DaySummary day, long max, Brush color)
    {
        var row = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(new TextBlock { Text = Icons.Glyph(icon), Style = (Style)FindResource("Style.Icon"), FontSize = 20, Foreground = color, Margin = new Thickness(0, 0, 10, 0) });
        var info = new Grid();
        info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var n = T(name, "Style.Text", 13, FontWeights.Medium);
        var value = T(day.Tokens > 0 ? AiUsageSummary.FormatTokens(day.Tokens) : "—", "Style.Text", 12, FontWeights.SemiBold);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        info.Children.Add(n); info.Children.Add(value);
        var bar = new Grid { Height = 4, Margin = new Thickness(0, 4, 0, 0) };
        var share = (double)day.Tokens / max;
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(share, 0.001), GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1 - share, 0), GridUnitType.Star) });
        var track = new Border { CornerRadius = new CornerRadius(2), Background = (Brush)FindResource("Brush.Control") };
        Grid.SetColumnSpan(track, 2);
        bar.Children.Add(track);
        if (day.Tokens > 0) bar.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = color });
        Grid.SetRow(bar, 1);
        info.Children.Add(bar);
        if (day.Tokens == 0)
        {
            var none = T("No usage today", "Style.TextDim", 11);
            none.HorizontalAlignment = HorizontalAlignment.Right;
            none.Margin = new Thickness(0, 16, 0, 0);
            info.Children.Add(none);
        }
        Grid.SetColumn(info, 1);
        row.Children.Add(info);
        return row;
    }
}
