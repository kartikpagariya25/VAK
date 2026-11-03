using System.Windows;
using System.Windows.Controls;
using VAK.Core.Widgets;

namespace VAK.App.Widgets;

public static class WidgetCatalog
{
    public static IReadOnlyList<WidgetModule> Build(AppHost host) => new[]
    {
        new WidgetModule(
            new WidgetInfo("home", "Home", "", "home", new[] { "widgets", "overview" }, false),
            () => Placeholder("Home"), () => Placeholder("Home"), "Your widgets at a glance"),
        new WidgetModule(
            new WidgetInfo("system", "System", "Power · Network · Audio", "system", new[] { "battery", "cpu", "ram", "network", "volume", "performance" }, true),
            () => Placeholder("System card"), () => Placeholder("System"), "Live performance at a glance"),
        new WidgetModule(
            new WidgetInfo("clock", "Clock", "Local Time & Date", "clock", new[] { "time", "date", "world", "zones" }, true),
            () => Placeholder("Clock card"), () => Placeholder("Clock"), "Local time and world clocks"),
        new WidgetModule(
            new WidgetInfo("timers", "Timers", "Focus & intervals", "timers", new[] { "pomodoro", "focus", "stopwatch", "countdown", "water" }, true),
            () => Placeholder("Timers card"), () => Placeholder("Timers"), "Pomodoro, countdown, stopwatch, water"),
    };

    // Replaced by the real cards and pages in the page tasks.
    private static FrameworkElement Placeholder(string text) => new TextBlock
    {
        Text = text, Foreground = System.Windows.Media.Brushes.Gray,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
    };
}
