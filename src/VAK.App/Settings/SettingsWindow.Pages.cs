using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VAK.App.Shell;
using VAK.App.Theme;
using VAK.Core.Settings;
using VAK.Core.Widgets;

namespace VAK.App.Settings;

public sealed partial class SettingsWindow
{
    // ---------- modules ----------

    private static readonly (string Title, string[] Ids)[] Groups =
    {
        ("Essentials", new[] { "system", "timers", "weather", "clock", "calendar", "nowplaying" }),
        ("Productivity", new[] { "clipboard", "notes", "fileshelf", "drop", "units", "converter", "links", "message" }),
        ("Insights", new[] { "screentime" }),
        ("AI", new[] { "aicoding" }),
    };

    private static readonly Dictionary<string, string> Tiles = new()
    {
        ["system"] = "#1FA58B", ["timers"] = "#F07A2A", ["weather"] = "#32ADE6", ["clock"] = "#E8A81C", ["calendar"] = "#F0486A", ["nowplaying"] = "#E5484D",
        ["clipboard"] = "#6B6CF0", ["notes"] = "#E8B71C", ["fileshelf"] = "#1CB4D0", ["drop"] = "#8C7CF0", ["units"] = "#8B5CF0", ["converter"] = "#D84CE0",
        ["links"] = "#E8504A", ["message"] = "#E84C98", ["screentime"] = "#9B5CF0", ["aicoding"] = "#6A6A70",
    };

    private StackPanel? _dockList, _moduleList, _homeList;

    private StackPanel BuildModules(SettingsKit kit)
    {
        var page = kit.NewPage("Modules", "Choose what lives in your notch. Reorder with the arrows, or Alt+Up and Alt+Down.");
        page.Children.Add(kit.Section("Dock"));
        _dockList = new StackPanel();
        page.Children.Add(_dockList);
        page.Children.Add(kit.Section("All modules"));
        _moduleList = new StackPanel();
        page.Children.Add(_moduleList);
        var reset = kit.PillButton("Reset order", ResetDock, 110);
        reset.HorizontalAlignment = HorizontalAlignment.Left; reset.Margin = new Thickness(0, 6, 0, 8);
        page.Children.Add(reset);
        page.Children.Add(kit.Section("Home widgets"));
        page.Children.Add(SettingsKit.Text("Home shows up to six widgets. Remove one to make room for another.", "Style.TextDim", 13));
        _homeList = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        page.Children.Add(_homeList);
        kit.Index.Add(new SettingEntry("modules", "Dock modules", "Turn modules on or off and reorder the dock", _dockList));
        kit.Index.Add(new SettingEntry("modules", "Home widgets", "Which widgets Home shows", _homeList));
        RebuildModules();
        return page;
    }

    private UIElement ModuleTile(string id, WidgetInfo info)
    {
        var tile = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(ThemeService.Parse(Tiles.GetValueOrDefault(id, "#5A5A60"), Colors.Gray)) };
        tile.Child = new TextBlock { Text = Icons.Glyph(info.IconKey), Style = (Style)FindResource("Style.Icon"), FontSize = 22, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        return tile;
    }

    private Grid ModuleRow(WidgetInfo info, UIElement right, UIElement? extra = null)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(ModuleTile(info.Id, info));
        var text = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(SettingsKit.Text(info.Title, size: 16, weight: FontWeights.SemiBold));
        var blurb = _host.Modules.TryGetValue(info.Id, out var m) ? m.Blurb : info.Subtitle;
        text.Children.Add(SettingsKit.Text(blurb, "Style.TextDim", 14));
        Grid.SetColumn(text, 1); grid.Children.Add(text);
        var right2 = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (extra is not null) right2.Children.Add(extra);
        right2.Children.Add(right);
        Grid.SetColumn(right2, 2); grid.Children.Add(right2);
        System.Windows.Automation.AutomationProperties.SetName(grid, info.Title);
        return grid;
    }

    private void ModulesChanged()
    {
        _host.Apply();
        _host.RaiseModulesChanged();
        RebuildModules();
    }

    private void RebuildModules()
    {
        if (_dockList is null || _moduleList is null || _homeList is null) return;
        var s = _host.Settings;
        _dockList.Children.Clear(); _moduleList.Children.Clear(); _homeList.Children.Clear();

        // the dock, in order, with arrows
        foreach (var id in s.Dock.ToList())
        {
            if (_host.Registry.Find(id) is not { } info) continue;
            var moduleId = id;
            var arrows = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 0) };
            arrows.Children.Add(Arrow("up", "Move up", () => { if (DockEditor.Move(s.Dock, moduleId, -1)) ModulesChanged(); }, s.Dock.IndexOf(id) > 0));
            arrows.Children.Add(Arrow("down", "Move down", () => { if (DockEditor.Move(s.Dock, moduleId, 1)) ModulesChanged(); }, s.Dock.IndexOf(id) < s.Dock.Count - 1));
            var row = ModuleRow(info, new TextBlock(), arrows);
            row.Focusable = true;
            row.PreviewKeyDown += (_, e) =>
            {
                if (Keyboard.Modifiers != ModifierKeys.Alt) return;
                var key = e.Key == Key.System ? e.SystemKey : e.Key;
                if (key is Key.Up or Key.Down && DockEditor.Move(s.Dock, moduleId, key == Key.Up ? -1 : 1)) { e.Handled = true; ModulesChanged(); }
            };
            _dockList.Children.Add(row);
        }
        if (s.Dock.Count == 0) _dockList.Children.Add(SettingsKit.Text("The dock is empty. Turn a module on below.", "Style.TextDim", 14));

        // every module with an on/off switch
        var kit = new SettingsKit("modules");
        foreach (var (title, ids) in Groups)
        {
            var present = ids.Select(i => _host.Registry.Find(i)).OfType<WidgetInfo>().ToList();
            if (present.Count == 0) continue;
            _moduleList.Children.Add(kit.Section(title));
            foreach (var info in present)
            {
                var id = info.Id;
                var toggle = kit.Toggle(s.Dock.Contains(id), on =>
                {
                    if (!DockEditor.Set(s.Dock, id, on, DockView.Capacity - 1)) { RebuildModules(); return; }
                    ModulesChanged();
                });
                _moduleList.Children.Add(ModuleRow(info, toggle));
            }
        }

        // home widgets
        var slots = s.HomeSlots;
        var full = slots.All(x => x is not null);
        foreach (var info in _host.Registry.All.Where(w => w.HasHomeCard && w.Id != "home"))
        {
            var id = info.Id;
            var on = slots.Contains(id);
            var toggle = kit.Toggle(on, v =>
            {
                if (v) { var free = Array.IndexOf(slots, null); if (free >= 0) slots[free] = id; }
                else { var at = Array.IndexOf(slots, id); if (at >= 0) slots[at] = null; }
                ModulesChanged();
            });
            toggle.IsEnabled = on || !full;
            var row = ModuleRow(info, toggle);
            if (!on && full) ((TextBlock)((StackPanel)row.Children[1]).Children[1]).Text = "Remove a widget to make room";
            _homeList.Children.Add(row);
        }
    }

    private Button Arrow(string icon, string name, Action click, bool enabled)
    {
        var b = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = 34, Height = 34, Background = (Brush)FindResource("Brush.Control"), Margin = new Thickness(0, 0, 6, 0), IsEnabled = enabled, Opacity = enabled ? 1 : 0.35,
            Content = new TextBlock { Text = Icons.Glyph(icon), Style = (Style)FindResource("Style.Icon"), FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center },
        };
        b.Click += (_, _) => click();
        System.Windows.Automation.AutomationProperties.SetName(b, name);
        return b;
    }

    private void ResetDock()
    {
        var s = _host.Settings;
        var defaults = new AppSettings().Dock;
        var ordered = defaults.Where(s.Dock.Contains).ToList();
        ordered.AddRange(s.Dock.Where(id => !defaults.Contains(id)));
        s.Dock.Clear(); s.Dock.AddRange(ordered);
        ModulesChanged();
    }

    // ---------- live activities ----------

    private StackPanel BuildLive(SettingsKit kit)
    {
        var l = _host.Settings.Live;
        var page = kit.NewPage("Live Activities", "Which events may expand the notch");

        page.Children.Add(kit.Section("Music"));
        page.Children.Add(kit.Row("Now playing", "Music from your player", kit.Toggle(l.NowPlaying, v => { l.NowPlaying = v; Changed(); })));
        page.Children.Add(kit.Row("Show browser media in notch", "Includes videos and ads reported by your browser", kit.Toggle(l.BrowserMedia, v => { l.BrowserMedia = v; Changed(); })));
        page.Children.Add(kit.Row("Music color", "Beat color for now playing",
            kit.Swatches(new[] { "#FFFFFF", "#FF453A", "#FF375F", "#BF5AF2", "#0A84FF", "#64D2FF", "#30D158", "#FFD60A" }, l.MusicColor, c => { l.MusicColor = c; Changed(); }, allowAuto: true)));

        page.Children.Add(kit.Section("Charging"));
        page.Children.Add(kit.Row("Battery charging", "Plugged in, unplugged and fully charged", kit.Toggle(l.Battery, v => { l.Battery = v; Changed(); })));
        page.Children.Add(kit.Section("Volume"));
        page.Children.Add(kit.Row("Volume", "When you change the volume", kit.Toggle(l.Volume, v => { l.Volume = v; Changed(); })));
        page.Children.Add(kit.Section("Copied"));
        page.Children.Add(kit.Row("Clipboard copied", "A short confirmation when you copy something", kit.Toggle(l.Clipboard, v => { l.Clipboard = v; Changed(); })));
        page.Children.Add(kit.Row("Hide copied text", "Show \"Text\" or \"Image\" instead of what you copied", kit.Toggle(l.HideCopiedText, v => { l.HideCopiedText = v; Changed(); })));

        page.Children.Add(kit.Section("Reminders"));
        page.Children.Add(kit.Row("Running timers", "Show a running Pomodoro, countdown or stopwatch on the closed notch; click it to open Timers", kit.Toggle(l.RunningTimers, v => { l.RunningTimers = v; Changed(); })));
        page.Children.Add(kit.Row("Timers", "Pomodoro, countdown and water reminders when they finish", kit.Toggle(l.TimersDone, v => { l.TimersDone = v; Changed(); })));
        page.Children.Add(kit.Row("Calendar alerts", "Upcoming events and reminders that are due", kit.Toggle(l.CalendarAlerts, v => { l.CalendarAlerts = v; Changed(); })));
        page.Children.Add(kit.Row("Reminders", "Where upcoming reminders appear", kit.Segments(new[] { "Notch only", "Notch + Windows" }, l.RemindersWhere == "both" ? 1 : 0, i => { l.RemindersWhere = i == 1 ? "both" : "notch"; Changed(); })));

        page.Children.Add(kit.Section("Timing"));
        page.Children.Add(kit.Row("Event duration", "How long an event stays before the notch settles",
            kit.Segments(new[] { "2 s", "3 s", "5 s" }, l.EventSeconds switch { 2 => 0, 5 => 2, _ => 1 }, i => { l.EventSeconds = i switch { 0 => 2, 2 => 5, _ => 3 }; Changed(); })));
        return page;
    }

    // ---------- privacy ----------

    private StackPanel BuildPrivacy(SettingsKit kit)
    {
        var p = _host.Settings.Privacy;
        var page = kit.NewPage("Privacy", "What VAK records and what it sends");

        page.Children.Add(kit.Section("Kept on this PC"));
        page.Children.Add(kit.Row("Track screen time", "Notes which app is in front, only on this PC. Turn off to stop recording.", kit.Toggle(p.TrackScreenTime, v => { p.TrackScreenTime = v; Changed(); })));
        page.Children.Add(kit.Row("Keep clipboard history", "Remembers what you copy so you can search and paste it again. Password managers' private copies are never recorded.", kit.Toggle(p.KeepClipboardHistory, v => { p.KeepClipboardHistory = v; Changed(); })));
        var cleared = SettingsKit.Text("", "Style.TextDim", 13); cleared.Margin = new Thickness(12, 0, 0, 0); cleared.VerticalAlignment = VerticalAlignment.Center;
        var clearRow = new StackPanel { Orientation = Orientation.Horizontal };
        clearRow.Children.Add(kit.PillButton("Clear history", () => { _host.Clipboard.Clear(); cleared.Text = "Clipboard history cleared."; }, 120));
        clearRow.Children.Add(cleared);
        page.Children.Add(kit.Row("Clear clipboard history", "Deletes every saved clip, including images, now", clearRow));

        page.Children.Add(kit.Section("What goes over the internet"));
        var list = new StackPanel();
        void Line(string who, string what) { var t = SettingsKit.Text($"{who}: {what}", "Style.TextDim", 14); t.Margin = new Thickness(0, 0, 0, 8); list.Children.Add(t); }
        Line("Weather", "the city you choose is sent to open-meteo.com to get the forecast");
        Line("Currency (Units)", "a request to frankfurter.app for exchange rates; nothing about you");
        Line("AI chat, website mode", "claude.ai or chatgpt.com load in an embedded browser; you sign in there and VAK never sees your password");
        Line("AI chat, API mode", "your messages and attachments go to api.anthropic.com or api.openai.com with your own key, which stays encrypted on this PC");
        Line("Updates", "only when you press Check for updates, a request to github.com");
        Line("Drop", "stays on your private network; it is switched off on public networks");
        Line("Everything else", "notes, links, calendar, clipboard and screen time never leave this PC");
        page.Children.Add(list);
        kit.Index.Add(new SettingEntry("privacy", "What goes over the internet", "Weather, currency, AI chat and updates", list));
        return page;
    }

    // ---------- license and updates ----------

    private StackPanel BuildUpdates(SettingsKit kit)
    {
        var page = kit.NewPage("License & Updates", "VAK is free; no account or key is needed");
        page.Children.Add(kit.Row("Version", "The version of VAK you are running", SettingsKit.Text("v" + Version(), size: 15, weight: FontWeights.Medium)));
        var status = SettingsKit.Text("", "Style.TextDim", 14);
        status.Margin = new Thickness(12, 0, 0, 0); status.VerticalAlignment = VerticalAlignment.Center;
        var button = kit.PillButton("Check for updates", () => { }, 150);
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false; status.Text = "Checking…";
            status.Text = await CheckForUpdateAsync();
            button.IsEnabled = true;
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(button); row.Children.Add(status);
        page.Children.Add(kit.Row("Updates", "Looks for a newer release on GitHub when you press the button; VAK never checks by itself", row));
        page.Children.Add(kit.Row("License", "VAK's own code is yours to read, build and change", kit.PillButton("Open on GitHub", () => OpenUrl("https://github.com/kartikpagariya25/VAK"), 140)));

        page.Children.Add(kit.Section("Open-source parts"));
        var credits = new StackPanel();
        foreach (var line in new[]
        {
            "Inter and Geist fonts, SIL Open Font License 1.1",
            "Fluent System Icons, MIT License (Microsoft)",
            "NAudio, MIT License",
            "QRCoder, MIT License",
            "Microsoft Edge WebView2, Microsoft Software License",
            "CommunityToolkit.Mvvm, MIT License (.NET Foundation)",
        }) { var t = SettingsKit.Text(line, "Style.TextDim", 14); t.Margin = new Thickness(0, 0, 0, 6); credits.Children.Add(t); }
        page.Children.Add(credits);
        return page;
    }

    private static async Task<string> CheckForUpdateAsync()
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("VAK-update-check");
            using var response = await http.GetAsync("https://api.github.com/repos/kartikpagariya25/VAK/releases/latest");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return "No release has been published yet.";
            if (!response.IsSuccessStatusCode) return "Could not check right now. Try again later.";
            using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var latest = System.Version.TryParse(tag.TrimStart('v', 'V'), out var v) ? v : null;
            var mine = System.Version.TryParse(SettingsWindow.Version(), out var m) ? m : new System.Version(0, 0, 0);
            return latest is null ? $"Latest release: {tag}" : latest > mine ? $"Version {tag} is available on GitHub." : "You are up to date.";
        }
        catch (Exception e) when (e is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            return "Could not reach GitHub. Check your connection.";
        }
    }

    // ---------- about ----------

    private StackPanel BuildAbout(SettingsKit kit)
    {
        var page = kit.NewPage("About", "A notch for your Windows desktop");
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 24) };
        head.Children.Add(new Image { Source = (ImageSource)FindResource("Brand.Mark"), Width = 84, Height = 84 });
        var names = new StackPanel { Margin = new Thickness(18, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(SettingsKit.Text("VAK", size: 28, weight: FontWeights.Bold));
        names.Children.Add(SettingsKit.Text("Version " + Version(), "Style.TextDim", 15));
        head.Children.Add(names);
        page.Children.Add(head);
        var blurb = SettingsKit.Text("VAK puts the things you reach for all day (clock, timers, clipboard, notes, files, calendar, weather, music and AI chat) in one place that grows out of the top of your screen.", "Style.TextDim", 15);
        blurb.Margin = new Thickness(0, 0, 0, 22);
        page.Children.Add(blurb);
        page.Children.Add(kit.Row("Source code", "github.com/kartikpagariya25/VAK", kit.PillButton("Open", () => OpenUrl("https://github.com/kartikpagariya25/VAK"), 90)));
        page.Children.Add(kit.Row("Feedback", "Report a problem or suggest something", kit.PillButton("Open issues", () => OpenUrl("https://github.com/kartikpagariya25/VAK/issues"), 120)));
        page.Children.Add(kit.Row("Your data folder", "Settings, notes and history live here", kit.PillButton("Open folder", () => OpenUrl(_host.DataDir), 120)));
        return page;
    }
}
