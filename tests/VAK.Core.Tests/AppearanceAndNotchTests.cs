using System.Text.Json;
using VAK.Core.Settings;
using Xunit;

namespace VAK.Core.Tests;

public class AppearanceAndNotchTests
{
    // ---- time format ----
    [Fact]
    public void Time_format_12_and_24_are_explicit_and_system_follows_the_culture()
    {
        var s = new AppSettings { TimeFormat = "24" };
        Assert.True(s.Use24Hour);
        s.TimeFormat = "12";
        Assert.False(s.Use24Hour);
        s.TimeFormat = "system";
        Assert.Equal(AppSettings.SystemUses24Hour(), s.Use24Hour);
    }

    [Fact]
    public void Setting_Use24Hour_picks_an_explicit_format()
    {
        var s = new AppSettings { Use24Hour = true };
        Assert.Equal("24", s.TimeFormat);
        s.Use24Hour = false;
        Assert.Equal("12", s.TimeFormat);
    }

    [Fact]
    public void New_sections_have_sensible_defaults()
    {
        var s = new AppSettings();
        Assert.Equal("notch", s.Notch.Style);
        Assert.Equal("hover", s.Notch.OpensOn);
        Assert.Equal("always", s.Notch.Visibility);
        Assert.True(s.Notch.ShowNotch && s.Notch.HideInFullscreen && s.Notch.TabAnimations);
        Assert.Equal("black", s.Appearance.Theme);
        Assert.Equal(3, s.Live.EventSeconds);
        Assert.True(s.Live.Battery && s.Live.Volume && s.Live.Clipboard && s.Live.CalendarAlerts && s.Live.HideCopiedText);
        Assert.Equal("kmh", s.Weather.WindUnit);
    }

    [Fact]
    public void Settings_with_null_new_sections_load_with_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "vak-set-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{\"Notch\":null,\"Appearance\":null,\"Live\":null}");
        var s = new SettingsStore(path).Load();
        Assert.NotNull(s.Notch); Assert.NotNull(s.Appearance); Assert.NotNull(s.Live); Assert.NotNull(s.Appearance.EdgeLight);
    }

    [Fact]
    public void New_settings_round_trip_through_json()
    {
        var s = new AppSettings();
        s.Notch.Style = "island"; s.Appearance.Theme = "ember"; s.Appearance.EdgeLight.Color = "#FF8800"; s.Live.EventSeconds = 5;
        var back = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s))!;
        Assert.Equal("island", back.Notch.Style); Assert.Equal("ember", back.Appearance.Theme);
        Assert.Equal("#FF8800", back.Appearance.EdgeLight.Color); Assert.Equal(5, back.Live.EventSeconds);
    }

    // ---- visibility ----
    [Theory]
    [InlineData("always", false, false, true, NotchMode.Full)]
    [InlineData("always", true, false, true, NotchMode.Hidden)]   // fullscreen app
    [InlineData("always", true, false, false, NotchMode.Full)]    // hide-in-fullscreen switched off
    [InlineData("smart", false, true, true, NotchMode.Minimal)]   // maximized window
    [InlineData("smart", false, false, true, NotchMode.Full)]
    [InlineData("minimal", false, false, true, NotchMode.Minimal)]
    [InlineData("hidden", false, false, true, NotchMode.Hidden)]
    public void Notch_policy_decides_how_much_of_the_notch_to_show(string visibility, bool fullscreen, bool maximized, bool hideInFullscreen, NotchMode expected)
    {
        var n = new NotchSettings { Visibility = visibility, HideInFullscreen = hideInFullscreen };
        Assert.Equal(expected, NotchPolicy.Decide(n, fullscreen, maximized));
    }

    [Fact]
    public void Show_notch_off_hides_it_whatever_the_visibility()
    {
        Assert.Equal(NotchMode.Hidden, NotchPolicy.Decide(new NotchSettings { ShowNotch = false, Visibility = "always" }, false, false));
    }

    [Fact]
    public void Power_off_hides_the_notch_for_this_session_without_touching_the_saved_setting()
    {
        var n = new NotchSettings { ShowNotch = true, Visibility = "always" };
        Assert.Equal(NotchMode.Hidden, NotchPolicy.Decide(n, false, false, poweredOff: true));
        Assert.Equal(NotchMode.Full, NotchPolicy.Decide(n, false, false, poweredOff: false));
        Assert.True(n.ShowNotch);
    }

    [Fact]
    public void Size_scale_is_clamped_and_geometry_follows_style()
    {
        Assert.Equal(1.0, NotchGeometry.Scale(new NotchSettings { SizePercent = 100 }));
        Assert.Equal(0.7, NotchGeometry.Scale(new NotchSettings { SizePercent = 10 }), 3);
        Assert.Equal(1.3, NotchGeometry.Scale(new NotchSettings { SizePercent = 999 }), 3);
        var notch = NotchGeometry.Collapsed(new NotchSettings { Style = "notch", SizePercent = 100 });
        var line = NotchGeometry.Collapsed(new NotchSettings { Style = "line", SizePercent = 100 });
        var island = NotchGeometry.Collapsed(new NotchSettings { Style = "island", SizePercent = 100 });
        Assert.True(line.Height < notch.Height);
        Assert.True(island.TopGap > 0 && notch.TopGap == 0);
        Assert.True(island.TopRadius > 0 && notch.TopRadius == 0);
        Assert.Equal(notch.Width * 0.7, NotchGeometry.Collapsed(new NotchSettings { Style = "notch", SizePercent = 70 }).Width, 0);
    }

    // ---- themes ----
    [Fact]
    public void Every_theme_has_a_palette_and_silver_is_light()
    {
        foreach (var name in new[] { "black", "ember", "vivid", "silver" }) Assert.NotNull(Themes.Get(name));
        Assert.True(Themes.Get("silver").Light);
        Assert.False(Themes.Get("black").Light);
        Assert.Equal("black", Themes.Get("nonsense").Name);
        Assert.NotEqual(Themes.Get("black").Primary, Themes.Get("ember").Primary);
    }

    // ---- edge light ----
    [Theory]
    [InlineData("slow", 6.0)]
    [InlineData("normal", 3.5)]
    [InlineData("fast", 1.8)]
    [InlineData("other", 3.5)]
    public void Edge_light_speed_maps_to_a_loop_length(string speed, double seconds)
        => Assert.Equal(seconds, EdgeLightSpec.LoopSeconds(speed));

    [Fact]
    public void Edge_light_intensity_is_a_clamped_opacity()
    {
        Assert.Equal(0.8, EdgeLightSpec.Opacity(80), 3);
        Assert.Equal(0.1, EdgeLightSpec.Opacity(-5), 3);
        Assert.Equal(1.0, EdgeLightSpec.Opacity(500), 3);
    }

    [Fact]
    public void Edge_light_plays_always_or_only_around_events_and_hover()
    {
        Assert.True(EdgeLightSpec.ShouldPlay("always", false, false));
        Assert.False(EdgeLightSpec.ShouldPlay("events", false, false));
        Assert.True(EdgeLightSpec.ShouldPlay("events", true, false));
        Assert.True(EdgeLightSpec.ShouldPlay("events", false, true));
    }

    // ---- live activities ----
    [Fact]
    public void Live_policy_follows_each_toggle()
    {
        var l = new LiveSettings { Battery = false, Volume = true, Clipboard = false, TimersDone = true, CalendarAlerts = false, NowPlaying = true };
        Assert.False(LiveActivityPolicy.Allows(l, LiveKind.Battery));
        Assert.True(LiveActivityPolicy.Allows(l, LiveKind.Volume));
        Assert.False(LiveActivityPolicy.Allows(l, LiveKind.Clipboard));
        Assert.True(LiveActivityPolicy.Allows(l, LiveKind.TimerDone));
        Assert.False(LiveActivityPolicy.Allows(l, LiveKind.CalendarAlert));
        Assert.True(LiveActivityPolicy.Allows(l, LiveKind.NowPlaying));
    }

    [Fact]
    public void Events_over_apps_only_show_while_tucked_away_if_allowed()
    {
        var n = new NotchSettings { ShowEventsOverApps = false };
        Assert.False(LiveActivityPolicy.MayShowNow(n, NotchMode.Hidden));
        Assert.True(LiveActivityPolicy.MayShowNow(n, NotchMode.Full));
        Assert.True(LiveActivityPolicy.MayShowNow(new NotchSettings { ShowEventsOverApps = true }, NotchMode.Hidden));
    }

    [Theory]
    [InlineData("secret text", true, "Text")]
    [InlineData("secret text", false, "secret text")]
    public void Copied_label_hides_text_when_asked(string text, bool hide, string expected)
        => Assert.Equal(expected, LiveActivityPolicy.CopiedLabel(text, isImage: false, hide));

    [Fact]
    public void Copied_label_for_images_and_long_text()
    {
        Assert.Equal("Image", LiveActivityPolicy.CopiedLabel("", true, false));
        Assert.Equal("Image", LiveActivityPolicy.CopiedLabel("", true, true));
        var label = LiveActivityPolicy.CopiedLabel(new string('x', 100), false, false);
        Assert.True(label.Length <= 30);
        Assert.Equal("a b", LiveActivityPolicy.CopiedLabel("a\n b", false, false));
    }

    [Fact]
    public void Reminders_also_go_to_windows_only_when_asked()
    {
        Assert.False(LiveActivityPolicy.AlsoWindowsNotice(new LiveSettings { RemindersWhere = "notch" }));
        Assert.True(LiveActivityPolicy.AlsoWindowsNotice(new LiveSettings { RemindersWhere = "both" }));
    }

    // ---- dock editing ----
    [Fact]
    public void Dock_editor_adds_removes_and_respects_the_capacity()
    {
        var dock = new List<string> { "a", "b" };
        Assert.True(DockEditor.Set(dock, "c", true, 3));
        Assert.Equal(new[] { "a", "b", "c" }, dock);
        Assert.False(DockEditor.Set(dock, "d", true, 3));   // full
        Assert.True(DockEditor.Set(dock, "a", false, 3));
        Assert.Equal(new[] { "b", "c" }, dock);
        Assert.True(DockEditor.Set(dock, "b", true, 3));    // already there is not a change in membership
        Assert.Equal(2, dock.Count);
    }

    [Fact]
    public void Dock_editor_moves_items_and_stops_at_the_ends()
    {
        var dock = new List<string> { "a", "b", "c" };
        Assert.True(DockEditor.Move(dock, "c", -1));
        Assert.Equal(new[] { "a", "c", "b" }, dock);
        Assert.False(DockEditor.Move(dock, "a", -1));
        Assert.False(DockEditor.Move(dock, "b", 1));
        Assert.False(DockEditor.Move(dock, "zzz", 1));
    }
}
