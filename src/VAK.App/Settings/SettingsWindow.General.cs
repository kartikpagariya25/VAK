using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VAK.Core.Hotkeys;

namespace VAK.App.Settings;

public sealed partial class SettingsWindow
{
    private TextBlock _hotkeyText = null!;
    private TextBlock _hotkeyMessage = null!;
    private Button _hotkeyButton = null!;
    private bool _capturing;

    private StackPanel BuildGeneral(SettingsKit kit)
    {
        var s = _host.Settings;
        var page = kit.NewPage("General", "Startup, shortcut, time and units");

        var start = kit.Toggle(_host.Startup.IsEnabled, v => _host.SetStartWithWindows(v));
        Action<bool> sync = v => Dispatcher.Invoke(() => start.IsChecked = v);
        _host.StartWithWindowsChanged += sync;
        Closed += (_, _) => _host.StartWithWindowsChanged -= sync;
        page.Children.Add(kit.Row("Start VAK when Windows starts", "Starts silently each time you sign in. Quit applies only to this session.", start));

        _hotkeyText = SettingsKit.Text(s.Hotkey, size: 14, weight: FontWeights.Medium, wrap: false);
        _hotkeyText.VerticalAlignment = VerticalAlignment.Center;
        _hotkeyButton = kit.PillButton("Change", StartCapture, 80);
        _hotkeyButton.Margin = new Thickness(10, 0, 0, 0);
        var keyBox = new Border { Background = (Brush)FindResource("Brush.Card"), CornerRadius = new CornerRadius(14), Height = 40, Width = 200, Child = _hotkeyText, Padding = new Thickness(16, 0, 0, 0) };
        var keyRow = new StackPanel { Orientation = Orientation.Horizontal };
        keyRow.Children.Add(keyBox); keyRow.Children.Add(_hotkeyButton);
        page.Children.Add(kit.Row("Open the notch", "Opens or closes the notch from anywhere", keyRow));
        _hotkeyMessage = SettingsKit.Text("", "Style.TextDim", 13);
        _hotkeyMessage.Margin = new Thickness(0, -14, 0, 18); _hotkeyMessage.Visibility = Visibility.Collapsed;
        page.Children.Add(_hotkeyMessage);

        var timeIndex = s.TimeFormat switch { "12" => 1, "24" => 2, _ => 0 };
        page.Children.Add(kit.Row("Time format", "Used by the notch, Home and Clock",
            kit.Segments(new[] { "System", "12-hour", "24-hour" }, timeIndex, i => { s.TimeFormat = i switch { 1 => "12", 2 => "24", _ => "system" }; Changed(); })));

        page.Children.Add(kit.Row("Temperature", "Weather, Home and the notch",
            kit.Segments(new[] { "°C", "°F" }, s.Weather.Fahrenheit ? 1 : 0, i => { s.Weather.Fahrenheit = i == 1; Changed(); })));

        page.Children.Add(kit.Row("Wind speed", "Used by Weather and Home",
            kit.Segments(new[] { "km/h", "mph" }, s.Weather.WindUnit == "mph" ? 1 : 0, i => { s.Weather.WindUnit = i == 1 ? "mph" : "kmh"; Changed(); })));

        var language = SettingsKit.Text("English", "Style.TextDim", 15);
        page.Children.Add(kit.Row("Language", "More languages are on the way", language));
        return page;
    }

    // ---------- the open shortcut ----------

    private void StartCapture()
    {
        _capturing = true;
        _hotkeyText.Text = "Press the new shortcut…";
        ((TextBlock)_hotkeyButton.Content).Text = "Esc cancels";
        ShowHotkeyMessage(null);
        Focus();
    }

    private void EndCapture()
    {
        _capturing = false;
        _hotkeyText.Text = _host.Settings.Hotkey;
        ((TextBlock)_hotkeyButton.Content).Text = "Change";
    }

    private void CaptureHotkey(KeyEventArgs e)
    {
        if (!_capturing) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        if (key == Key.Escape) { EndCapture(); return; }

        var name = KeyName(key);
        var parts = new List<string>();
        var mods = Keyboard.Modifiers;
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows) || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) parts.Add("Win");
        if (name is null) { ShowHotkeyMessage("That key can't be used. Try a letter, number, F-key or Space."); return; }
        parts.Add(name);

        var text = string.Join("+", parts);
        if (HotkeyParser.TryParse(text) is null) { ShowHotkeyMessage("Include Ctrl, Alt, Shift or Win with the key."); return; }

        var old = _host.Settings.Hotkey;
        if (_host.TryApplyHotkey?.Invoke(text) == false)
        {
            _host.TryApplyHotkey?.Invoke(old); // the failed attempt released the old shortcut; put it back
            ShowHotkeyMessage($"{text} is already in use by another app. Pick a different one.");
            return;
        }
        _host.Settings.Hotkey = text;
        _host.Save();
        ShowHotkeyMessage(null);
        EndCapture();
    }

    private static string? KeyName(Key key)
    {
        if (key >= Key.A && key <= Key.Z) return key.ToString();
        if (key >= Key.D0 && key <= Key.D9) return ((int)(key - Key.D0)).ToString();
        if (key >= Key.F1 && key <= Key.F12) return key.ToString();
        return key switch { Key.Space => "Space", Key.Enter => "Enter", Key.Tab => "Tab", _ => null };
    }

    private void ShowHotkeyMessage(string? text)
    {
        _hotkeyMessage.Text = text ?? "";
        _hotkeyMessage.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
