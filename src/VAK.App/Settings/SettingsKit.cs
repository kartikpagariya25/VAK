using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VAK.App.Controls;
using VAK.App.Theme;

namespace VAK.App.Settings;

/// <summary>A row in a settings page. The search box matches against Title and Hint.</summary>
public record SettingEntry(string Page, string Title, string Hint, FrameworkElement Element);

/// <summary>Builds the rows and controls used by every Settings page, in the same look as the rest of VAK.</summary>
public sealed class SettingsKit
{
    public List<SettingEntry> Index { get; } = new();
    private readonly string _page;

    public SettingsKit(string page) { _page = page; }

    private static Brush B(string key) => (Brush)Application.Current.FindResource(key);
    private static Style S(string key) => (Style)Application.Current.FindResource(key);

    public static TextBlock Text(string text, string style = "Style.Text", double size = 14, FontWeight? weight = null, bool wrap = true)
    {
        var t = new TextBlock { Text = text, Style = S(style), FontSize = size, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap };
        if (weight is { } w) t.FontWeight = w;
        return t;
    }

    public StackPanel NewPage(string title, string subtitle)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 40) };
        if (title.Length > 0)
        {
            panel.Children.Add(new TextBlock { Text = title, Style = S("Style.Text"), FontSize = 32, FontWeight = FontWeights.Bold, FontFamily = (FontFamily)Application.Current.FindResource("Font.Display") });
            panel.Children.Add(new TextBlock { Text = subtitle, Style = S("Style.TextDim"), FontSize = 16, Margin = new Thickness(0, 4, 0, 18) });
        }
        return panel;
    }

    public UIElement Section(string label) => new TextBlock
    {
        Text = label.ToUpperInvariant(), Style = S("Style.TextDim"), FontSize = 12, Margin = new Thickness(0, 22, 0, 8),
    };

    /// <summary>A setting: bold title and dim hint on the left, the control on the right.</summary>
    public FrameworkElement Row(string title, string hint, UIElement control, bool register = true)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 26), MinHeight = 40 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 24, 0) };
        left.Children.Add(Text(title, size: 16, weight: FontWeights.SemiBold));
        if (hint.Length > 0) left.Children.Add(new TextBlock { Text = hint, Style = S("Style.TextDim"), FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
        grid.Children.Add(left);
        if (control is FrameworkElement fe) { fe.HorizontalAlignment = HorizontalAlignment.Left; fe.VerticalAlignment = VerticalAlignment.Center; }
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        System.Windows.Automation.AutomationProperties.SetName(grid, title);
        if (register) Index.Add(new SettingEntry(_page, title, hint, grid));
        return grid;
    }

    public CheckBox Toggle(bool value, Action<bool> set)
    {
        var box = new CheckBox { Style = S("Style.Toggle"), IsChecked = value };
        box.Click += (_, _) => set(box.IsChecked == true);
        return box;
    }

    public SegmentedControl Segments(string[] labels, int selected, Action<int> set)
    {
        var seg = new SegmentedControl(labels) { SelectedIndex = Math.Clamp(selected, 0, labels.Length - 1) };
        seg.SelectionChanged += set;
        return seg;
    }

    public Button PillButton(string text, Action click, double width = 90, Brush? background = null)
    {
        var b = new Button
        {
            Style = S("Style.RoundButton"), Height = 34, MinWidth = width, Padding = new Thickness(14, 0, 14, 0), Background = background ?? B("Brush.Control"),
            Content = Text(text, size: 13, weight: FontWeights.Medium, wrap: false),
        };
        b.Click += (_, _) => click();
        System.Windows.Automation.AutomationProperties.SetName(b, text);
        return b;
    }

    // ---------- dropdown ----------
    public FrameworkElement Dropdown((string Id, string Label)[] items, string current, Action<string> set, double width = 300)
    {
        var label = Text(items.FirstOrDefault(i => i.Id == current).Label ?? items[0].Label, size: 14, wrap: false);
        var chevron = new TextBlock { Text = Icons.Glyph("chevron"), Style = S("Style.Icon"), FontSize = 12, Foreground = B("Brush.TextDim"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        var inner = new Grid { Width = width - 36 };
        inner.Children.Add(label); inner.Children.Add(chevron);
        var button = new Button { Style = S("Style.RoundButton"), Height = 40, Width = width, Background = B("Brush.Control"), Content = inner, Padding = new Thickness(16, 0, 16, 0) };
        var list = new StackPanel();
        var popup = new Popup { PlacementTarget = button, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 4, HorizontalOffset = 0 };
        foreach (var (id, text) in items)
        {
            var itemId = id; var itemText = text;
            var row = new Button { Style = S("Style.RoundButton"), Height = 36, Width = width - 12, Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 0, 12, 0), Content = Text(text, size: 13, wrap: false) };
            row.Click += (_, _) => { label.Text = itemText; popup.IsOpen = false; set(itemId); };
            list.Children.Add(row);
        }
        popup.Child = new Border { Background = B("Brush.Card"), BorderBrush = B("Brush.Divider"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(6), Width = width, Child = list, Margin = new Thickness(8) };
        button.Click += (_, _) => popup.IsOpen = !popup.IsOpen;
        var host = new Grid();
        host.Children.Add(button); host.Children.Add(popup);
        host.HorizontalAlignment = HorizontalAlignment.Left;
        System.Windows.Automation.AutomationProperties.SetName(button, "Choose option");
        return host;
    }

    // ---------- slider ----------
    public FrameworkElement Slider(int min, int max, int value, Action<int> set, string suffix = "%")
    {
        var valueText = Text($"{value}{suffix}", "Style.TextDim", 14, wrap: false);
        valueText.Width = 54; valueText.TextAlignment = TextAlignment.Right; valueText.Margin = new Thickness(0, 0, 12, 0); valueText.VerticalAlignment = VerticalAlignment.Center;
        var track = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = B("Brush.Control"), VerticalAlignment = VerticalAlignment.Center };
        var fill = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = B("Brush.Text"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        var thumb = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = B("Brush.Text"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        var area = new Grid { Width = 210, Height = 28, Background = Brushes.Transparent, Cursor = Cursors.Hand, Focusable = true };
        area.Children.Add(track); area.Children.Add(fill); area.Children.Add(thumb);
        var current = Math.Clamp(value, min, max);

        void Place()
        {
            var x = (area.Width - 16) * (current - min) / (max - min);
            thumb.Margin = new Thickness(x, 0, 0, 0);
            fill.Width = x + 8;
            valueText.Text = $"{current}{suffix}";
        }
        void FromMouse(MouseEventArgs e)
        {
            var x = Math.Clamp(e.GetPosition(area).X - 8, 0, area.Width - 16);
            var v = min + (int)Math.Round(x / (area.Width - 16) * (max - min));
            if (v == current) return;
            current = v; Place(); set(v);
        }
        area.MouseLeftButtonDown += (_, e) => { area.CaptureMouse(); FromMouse(e); };
        area.MouseMove += (_, e) => { if (area.IsMouseCaptured) FromMouse(e); };
        area.MouseLeftButtonUp += (_, _) => area.ReleaseMouseCapture();
        area.KeyDown += (_, e) =>
        {
            var d = e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0;
            if (d == 0) return;
            current = Math.Clamp(current + d, min, max); Place(); set(current); e.Handled = true;
        };
        Place();
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(valueText); row.Children.Add(area);
        return row;
    }

    // ---------- option cards ----------
    public FrameworkElement Cards(IEnumerable<(string Id, string Title, string Desc, UIElement Preview)> items, string current, Action<string> set, int columns)
    {
        var grid = new UniformGrid { Columns = columns, Margin = new Thickness(-6, 0, -6, 0) };
        var borders = new List<(string Id, Border Border)>();
        void Mark(string id) { foreach (var (cid, b) in borders) b.BorderBrush = cid == id ? B("Brush.Text") : Brushes.Transparent; }
        foreach (var (id, title, desc, preview) in items)
        {
            var itemId = id;
            var stack = new StackPanel();
            var previewBox = new Border { Height = 96, CornerRadius = new CornerRadius(10), Background = B("Brush.Panel"), Child = preview, Margin = new Thickness(0, 0, 0, 12) };
            stack.Children.Add(previewBox);
            stack.Children.Add(Text(title, size: 16, weight: FontWeights.SemiBold));
            if (desc.Length > 0) stack.Children.Add(new TextBlock { Text = desc, Style = S("Style.TextDim"), FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            var card = new Border { Background = B("Brush.Card"), CornerRadius = new CornerRadius(14), Padding = new Thickness(14), Margin = new Thickness(6), BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent, Child = stack, Cursor = Cursors.Hand };
            card.MouseLeftButtonUp += (_, _) => { Mark(itemId); set(itemId); };
            System.Windows.Automation.AutomationProperties.SetName(card, title);
            borders.Add((id, card));
            grid.Children.Add(card);
        }
        Mark(current);
        return grid;
    }

    // ---------- colour swatches ----------
    public FrameworkElement Swatches(string[] colors, string current, Action<string> set, bool allowAuto = false)
    {
        var wrap = new WrapPanel { Width = 360 };
        var dots = new List<(string Value, Border Ring)>();
        void Mark(string v) { foreach (var (val, ring) in dots) ring.Background = string.Equals(val, v, StringComparison.OrdinalIgnoreCase) ? B("Brush.Control") : Brushes.Transparent; }
        void AddDot(string value, Brush fill, string name)
        {
            var dot = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = fill };
            var ring = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 6, 6), Child = dot, Cursor = Cursors.Hand, Background = Brushes.Transparent };
            ring.MouseLeftButtonUp += (_, _) => { Mark(value); set(value); };
            System.Windows.Automation.AutomationProperties.SetName(ring, name);
            dots.Add((value, ring));
            wrap.Children.Add(ring);
        }
        if (allowAuto) AddDot("auto", new LinearGradientBrush(Colors.DeepPink, Colors.DodgerBlue, 45), "Automatic colour");
        foreach (var c in colors) AddDot(c, new SolidColorBrush(ThemeService.Parse(c, Colors.White)), "Colour " + c);
        var custom = PillButton("Custom…", () =>
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.ColorTranslator.FromHtml(current is "auto" or null ? "#FFFFFF" : current) };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            Mark(hex); set(hex);
        }, 100);
        custom.Margin = new Thickness(0, 4, 0, 0);
        var stack = new StackPanel();
        stack.Children.Add(wrap); stack.Children.Add(custom);
        Mark(current);
        return stack;
    }
}
