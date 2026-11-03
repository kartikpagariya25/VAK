using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using VAK.App.Controls;
using VAK.App.Theme;
using VAK.Core.Settings;

namespace VAK.App.Settings;

public sealed partial class SettingsWindow
{
    private Brush Res(string key) => (Brush)FindResource(key);

    // ---------- small drawings used by the option cards ----------

    private UIElement Outline(double w, double h, CornerRadius radius, bool dashed = false, double top = 14)
    {
        var shape = new Border
        {
            Width = w, Height = h, CornerRadius = radius, BorderBrush = Res("Brush.TextDim"), BorderThickness = new Thickness(1.2),
            Background = dashed ? Brushes.Transparent : Res("Brush.Control"), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, top, 0, 0),
        };
        if (dashed) shape.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 200, 200, 200));
        return shape;
    }

    private UIElement EdgeLine(double top = 14) => new Border { Height = 1, Background = Res("Brush.Divider"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(18, top, 18, 0) };

    private UIElement VisibilityPreview(string kind)
    {
        var g = new Grid();
        g.Children.Add(EdgeLine(8));
        switch (kind)
        {
            case "always": g.Children.Add(Outline(96, 26, new CornerRadius(0, 0, 14, 14), top: 8)); break;
            case "smart":
                g.Children.Add(Outline(60, 18, new CornerRadius(0, 0, 10, 10), top: 8));
                g.Children.Add(new Border { Width = 52, Height = 36, Background = Res("Brush.Control"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 10, 22, 0) });
                break;
            case "minimal": g.Children.Add(new Border { Width = 22, Height = 11, Background = Res("Brush.Control"), CornerRadius = new CornerRadius(0, 0, 6, 6), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 0, 0) }); break;
            default: g.Children.Add(Outline(96, 26, new CornerRadius(0, 0, 14, 14), dashed: true, top: 8)); break;
        }
        return g;
    }

    private UIElement StylePreview(string style)
    {
        var g = new Grid();
        g.Children.Add(EdgeLine(18));
        g.Children.Add(style switch
        {
            "line" => Outline(88, 14, new CornerRadius(4), top: 14),
            "island" => Outline(88, 22, new CornerRadius(11), top: 26),
            _ => Outline(88, 28, new CornerRadius(0, 0, 14, 14), top: 18),
        });
        return g;
    }

    // ---------- the page ----------

    private StackPanel BuildNotch(SettingsKit kit)
    {
        var s = _host.Settings;
        var n = s.Notch;
        var page = kit.NewPage("Notch", "Shape, behavior and live preview");

        var preview = new NotchPreview(() => s);
        page.Children.Add(preview);
        void Refresh() { preview.Refresh(); Changed(); }

        page.Children.Add(kit.Section("Visibility"));
        page.Children.Add(kit.Cards(new[]
        {
            ("always", "Always", "Always on screen. Fullscreen rules below still apply.", VisibilityPreview("always")),
            ("smart", "Smart", "Full on the desktop; tucked away over maximized apps.", VisibilityPreview("smart")),
            ("minimal", "Minimal", "Just a small arrow at the top. Hover to open, or click.", VisibilityPreview("minimal")),
            ("hidden", "Hidden", "Nothing on screen. Open it from the tray icon or the shortcut.", VisibilityPreview("hidden")),
        }, n.Visibility, id => { n.Visibility = id; Refresh(); }, 2));

        page.Children.Add(kit.Section("Style"));
        page.Children.Add(kit.Cards(new[]
        {
            ("line", "Line", "A slim bar along the top edge", StylePreview("line")),
            ("notch", "Notch", "Grows out of the screen edge", StylePreview("notch")),
            ("island", "Island", "A pill floating just below the edge", StylePreview("island")),
        }, n.Style, id => { n.Style = id; Refresh(); }, 3));

        page.Children.Add(new Border { Height = 24 });
        page.Children.Add(kit.Row("Notch shape", "Fine-tune the notch silhouette",
            kit.Dropdown(new[] { ("compact", "Compact"), ("standard", "Standard"), ("wide", "Wide") }, n.Shape, id => { n.Shape = id; Refresh(); })));
        page.Children.Add(kit.Row("Size", "Scales the resting notch", kit.Slider(70, 130, n.SizePercent, v => { n.SizePercent = v; Refresh(); })));
        page.Children.Add(kit.Row("Resting layout", "Time and battery in corners or center",
            kit.Segments(new[] { "Corners", "Center" }, n.RestingLayout == "center" ? 1 : 0, i => { n.RestingLayout = i == 1 ? "center" : "corners"; Refresh(); })));
        page.Children.Add(kit.Row("Opens on", "Hover to open at once, or click to open on purpose",
            kit.Segments(new[] { "Hover", "Click" }, n.OpensOn == "click" ? 1 : 0, i => { n.OpensOn = i == 1 ? "click" : "hover"; Refresh(); })));

        var screens = System.Windows.Forms.Screen.AllScreens;
        var displays = new List<(string, string)> { ("primary", "Primary display") };
        for (var i = 0; i < screens.Length; i++)
            if (!screens[i].Primary) displays.Add((screens[i].DeviceName, $"Display {i + 1} ({screens[i].Bounds.Width}×{screens[i].Bounds.Height})"));
        page.Children.Add(kit.Row("Display", "Which monitor the notch lives on", kit.Dropdown(displays.ToArray(), n.Display, id => { n.Display = id; Refresh(); }, 300)));
        page.Children.Add(kit.Row("Show notch", "Hide it without quitting; the tray icon brings it back", kit.Toggle(n.ShowNotch, v => { n.ShowNotch = v; Refresh(); })));

        page.Children.Add(kit.Section("Behavior"));
        page.Children.Add(kit.Row("Hide in fullscreen", "Apps, videos and games on the notch's display", kit.Toggle(n.HideInFullscreen, v => { n.HideInFullscreen = v; Changed(); })));
        page.Children.Add(kit.Row("Show events over apps", "When the notch is tucked away, grow it briefly for events like volume, timers and a new song", kit.Toggle(n.ShowEventsOverApps, v => { n.ShowEventsOverApps = v; Changed(); })));
        page.Children.Add(kit.Row("Tab animations", "Fade between tabs inside the open notch", kit.Toggle(n.TabAnimations, v => { n.TabAnimations = v; Changed(); })));
        return page;
    }

    // ---------- appearance ----------

    private UIElement ThemePreview(ThemePalette p)
    {
        Color C(string hex) => ThemeService.Parse(hex, Colors.Black);
        var g = new Grid { Margin = new Thickness(14, 10, 14, 10) };
        var panel = new Border { Background = new SolidColorBrush(C(p.Panel)), CornerRadius = new CornerRadius(8), BorderBrush = new SolidColorBrush(C(p.Divider)), BorderThickness = new Thickness(1) };
        var grid = new UniformGrid { Columns = 3, Rows = 2, Margin = new Thickness(6) };
        for (var i = 0; i < 6; i++) grid.Children.Add(new Border { Background = new SolidColorBrush(C(p.Card)), BorderBrush = new SolidColorBrush(C(p.Divider)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Margin = new Thickness(2), Child = new Border { Width = 10, Height = 3, Background = new SolidColorBrush(i == 1 ? C(p.Accent) : C(p.TextDim)), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(4) } });
        panel.Child = grid;
        g.Children.Add(panel);
        return g;
    }

    private UIElement EdgeLightPreview(string style)
    {
        var g = new Grid();
        g.Children.Add(EdgeLine(14));
        var shape = Outline(84, 30, new CornerRadius(0, 0, 15, 15), top: 14);
        var color = ThemeService.Parse(_host.Settings.Appearance.EdgeLight.Color, Colors.White);
        var brush = style switch
        {
            "comet" => (Brush)new LinearGradientBrush(new GradientStopCollection { new(Colors.Transparent, 0.4), new(color, 1) }, 0),
            "breathe" => new SolidColorBrush(color),
            _ => new LinearGradientBrush(new GradientStopCollection { new(color, 0), new(Colors.Transparent, 0.4), new(Colors.Transparent, 0.6), new(color, 1) }, 0),
        };
        ((Border)shape).BorderBrush = brush;
        g.Children.Add(shape);
        return g;
    }

    private StackPanel BuildAppearance(SettingsKit kit)
    {
        var a = _host.Settings.Appearance;
        var e = a.EdgeLight;
        var page = kit.NewPage("Appearance", "Theme, surface and notch effects");

        page.Children.Add(kit.Section("Theme"));
        var names = new Dictionary<string, (string Title, string Desc)>
        {
            ["black"] = ("Pure Black", "Monochrome on true black"), ["ember"] = ("Ember Dark", "Black with an orange accent"),
            ["vivid"] = ("Vivid", "Black with a blue accent"), ["silver"] = ("Silver", "Light panel; the notch stays black"),
        };
        page.Children.Add(kit.Cards(Themes.All.Select(t => (t.Name, names[t.Name].Title, names[t.Name].Desc, ThemePreview(t))), a.Theme, id => { a.Theme = id; Changed(); }, 3));

        page.Children.Add(kit.Section("Surface"));
        var outlineWidth = kit.Segments(new[] { "1 px", "2 px", "3 px" }, Math.Clamp(a.OutlineWidth, 1, 3) - 1, i => { a.OutlineWidth = i + 1; Changed(); });
        outlineWidth.IsEnabled = a.Outline; outlineWidth.Opacity = a.Outline ? 1 : 0.4;
        page.Children.Add(kit.Row("Outline", "A hairline around the notch", kit.Toggle(a.Outline, v => { a.Outline = v; outlineWidth.IsEnabled = v; outlineWidth.Opacity = v ? 1 : 0.4; Changed(); })));
        page.Children.Add(kit.Row("Outline width", "", outlineWidth));
        page.Children.Add(kit.Row("Shadow", "Depth under the open notch",
            kit.Segments(new[] { "None", "Subtle", "Medium", "Deep" }, Array.IndexOf(new[] { "none", "subtle", "medium", "deep" }, a.Shadow), i => { a.Shadow = new[] { "none", "subtle", "medium", "deep" }[i]; Changed(); })));
        page.Children.Add(kit.Row("Soft edge", "Softens the notch edge; not used with the Silver theme",
            kit.Segments(new[] { "None", "Low", "Medium", "High" }, Array.IndexOf(new[] { "none", "low", "medium", "high" }, a.SoftEdge), i => { a.SoftEdge = new[] { "none", "low", "medium", "high" }[i]; Changed(); })));

        page.Children.Add(kit.Section("Edge light"));
        page.Children.Add(kit.Row("Edge light", "Light that runs along the notch outline", kit.Toggle(e.Enabled, v => { e.Enabled = v; Changed(); })));
        page.Children.Add(kit.Cards(new[]
        {
            ("comet", "Comet", "One light travels along the outline", EdgeLightPreview("comet")),
            ("twin", "Twin comets", "Two lights meet at the bottom, then return", EdgeLightPreview("twin")),
            ("breathe", "Breathe", "The whole outline glows softly", EdgeLightPreview("breathe")),
        }, e.Style, id => { e.Style = id; Changed(); }, 3));
        page.Children.Add(new Border { Height = 24 });
        page.Children.Add(kit.Row("Color", "", kit.Swatches(new[] { "#FFFFFF", "#E8B65A", "#FF9F0A", "#30D1A0", "#20B8B0", "#2F9BFF", "#6F6BF0", "#A56BF0", "#F0658A", "#F07A5A", "#C8793A", "#66C8F0" }, e.Color, c => { e.Color = c; Changed(); })));
        page.Children.Add(kit.Row("Intensity", "", kit.Slider(10, 100, e.Intensity, v => { e.Intensity = v; Changed(); })));
        page.Children.Add(kit.Row("Speed", "", kit.Segments(new[] { "Slow", "Normal", "Fast" }, Array.IndexOf(new[] { "slow", "normal", "fast" }, e.Speed), i => { e.Speed = new[] { "slow", "normal", "fast" }[i]; Changed(); })));
        page.Children.Add(kit.Row("When", "On events & hover plays the light for a few seconds, then it fades out",
            kit.Segments(new[] { "Always", "On events & hover" }, e.When == "events" ? 1 : 0, i => { e.When = i == 1 ? "events" : "always"; Changed(); })));
        return page;
    }
}

/// <summary>A small live picture of the notch (resting or hovered) that follows the notch settings.</summary>
public sealed class NotchPreview : Border
{
    private readonly Func<AppSettings> _settings;
    private readonly Grid _stage = new();
    private bool _hover;

    public NotchPreview(Func<AppSettings> settings)
    {
        _settings = settings;
        Height = 190; CornerRadius = new CornerRadius(14); Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0B, 0x0D)); ClipToBounds = true;
        var seg = new SegmentedControl("Resting", "Hover") { SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 12, 12, 0) };
        seg.SelectionChanged += i => { _hover = i == 1; Refresh(); };
        var root = new Grid();
        root.Children.Add(_stage);
        root.Children.Add(seg);
        Child = root;
        Loaded += (_, _) => Refresh();
    }

    public void Refresh()
    {
        var s = _settings();
        var n = s.Notch;
        var shape = NotchGeometry.Collapsed(n);
        _stage.Children.Clear();
        var white = Brushes.White;
        if (_hover)
        {
            var panel = new Border { Width = 320, Height = 140, Background = Brushes.Black, CornerRadius = new CornerRadius(0, 0, 22, 22), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, shape.TopGap, 0, 0), BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2C)), BorderThickness = new Thickness(1) };
            var grid = new UniformGrid { Columns = 3, Rows = 2, Margin = new Thickness(14, 22, 14, 14) };
            for (var i = 0; i < 6; i++) grid.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)), CornerRadius = new CornerRadius(7), Margin = new Thickness(3) });
            panel.Child = grid;
            _stage.Children.Add(panel);
            return;
        }
        var pill = new Border
        {
            Width = shape.Width * 1.2, Height = shape.Height * 1.2, Background = Brushes.Black, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, shape.TopGap * 1.2 + 24, 0, 0), CornerRadius = new CornerRadius(shape.TopRadius * 1.2, shape.TopRadius * 1.2, shape.BottomRadius * 1.2, shape.BottomRadius * 1.2),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3E)), BorderThickness = new Thickness(1),
        };
        if (n.Style != "line")
        {
            var inner = new Grid { Margin = new Thickness(14, 0, 14, 0) };
            var center = n.RestingLayout == "center";
            var time = new TextBlock { Text = "9:21 PM", Foreground = white, FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left, Margin = center ? new Thickness(0, 0, 52, 0) : new Thickness(0) };
            var battery = new TextBlock { Text = "100", Foreground = white, FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Right, Margin = center ? new Thickness(52, 0, 0, 0) : new Thickness(0) };
            inner.Children.Add(time); inner.Children.Add(battery);
            pill.Child = inner;
        }
        // a thin line across the top edge of the "screen"
        _stage.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2C)), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(60, 24, 60, 0) });
        _stage.Children.Add(pill);
    }
}
