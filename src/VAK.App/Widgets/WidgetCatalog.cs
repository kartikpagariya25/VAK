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
            () => Placeholder("Home"), () => new Pages.HomePage(host), "Your widgets at a glance"),
        new WidgetModule(
            new WidgetInfo("system", "System", "Power · Network · Audio", "system", new[] { "battery", "cpu", "ram", "network", "volume", "performance" }, true),
            () => new Cards.SystemCard(host), () => new Pages.SystemPage(host), "Live performance at a glance"),
        new WidgetModule(
            new WidgetInfo("clock", "Clock", "Local Time & Date", "clock", new[] { "time", "date", "world", "zones" }, true),
            () => new Cards.ClockCard(host), () => new Pages.ClockPage(host), "Local time and world clocks"),
        new WidgetModule(
            new WidgetInfo("timers", "Timers", "Focus & intervals", "timers", new[] { "pomodoro", "focus", "stopwatch", "countdown", "water" }, true),
            () => new Cards.FocusCard(host), () => new Pages.TimersPage(host), "Pomodoro, countdown, stopwatch, water"),
    }.Concat(BatchTwo(host)).ToArray();

    private static IEnumerable<WidgetModule> BatchTwo(AppHost host) => new[]
    {
        new WidgetModule(
            new WidgetInfo("weather", "Weather", "", "partly-day", new[] { "forecast", "temperature", "rain", "city", "hourly" }, true),
            () => new Cards.WeatherCard(host), () => new Pages.WeatherPage(host), "Current conditions and forecast"),
        new WidgetModule(
            new WidgetInfo("units", "Units", "Conversion", "ruler", new[] { "convert", "length", "mass", "temperature", "currency", "data", "speed" }, false),
            () => Placeholder("Units"), () => new Pages.UnitsPage(host), "Length, mass, temperature, currency and more"),
    };

    // Replaced by the real cards and pages in the page tasks.
    private static FrameworkElement Placeholder(string text) => new TextBlock
    {
        Text = text, Foreground = System.Windows.Media.Brushes.Gray,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
    };
}
