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
            new WidgetInfo("drop", "VAK Drop", "", "share", new[] { "share", "transfer", "phone", "send", "wifi", "qr", "airdrop" }, false),
            () => Placeholder("Drop"), () => new Pages.DropPage(host), "Send files to and from phones on your network"),
        new WidgetModule(
            new WidgetInfo("aicoding", "AI Coding", "Today", "bot", new[] { "claude", "codex", "tokens", "usage", "cost", "agent" }, false),
            () => Placeholder("AI Coding"), () => new Pages.AiCodingPage(host), "Claude Code and Codex usage from your local logs"),
        new WidgetModule(
            new WidgetInfo("fileshelf", "File Shelf", "Parked files", "folder", new[] { "files", "drag", "drop", "park", "shelf", "stash" }, false),
            () => Placeholder("File Shelf"), () => new Pages.FileShelfPage(host), "Park files and drag them back out"),
        new WidgetModule(
            new WidgetInfo("message", "Message", "Notch banner", "message", new[] { "banner", "flash", "text", "notify", "announce" }, false),
            () => Placeholder("Message"), () => new Pages.MessagePage(host), "Flash a short message on the notch"),
        new WidgetModule(
            new WidgetInfo("screentime", "Screen Time", "", "chart", new[] { "usage", "apps", "limit", "time", "focus" }, true),
            () => new Cards.ScreenTimeCard(host), () => new Pages.ScreenTimePage(host), "How long you spend in each app"),
        new WidgetModule(
            new WidgetInfo("clipboard", "Clipboard", "History", "clipboard", new[] { "copy", "paste", "history", "text", "images" }, true),
            () => new Cards.ClipboardCard(host), () => new Pages.ClipboardPage(host), "Everything you copied, one click away"),
        new WidgetModule(
            new WidgetInfo("notes", "Notes", "", "note", new[] { "note", "write", "memo", "text" }, true),
            () => new Cards.NotesCard(host), () => new Pages.NotesPage(host), "Quick notes that save as you type"),
        new WidgetModule(
            new WidgetInfo("links", "Links", "Quick Bookmarks", "link", new[] { "bookmarks", "search", "web", "google", "url" }, false),
            () => Placeholder("Links"), () => new Pages.LinksPage(host), "Search the web and keep saved links"),
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
