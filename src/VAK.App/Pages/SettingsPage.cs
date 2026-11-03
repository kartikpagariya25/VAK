using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VAK.App.Controls;
using VAK.Core.Hotkeys;

namespace VAK.App.Pages;

/// <summary>Build 1 settings: start with Windows, the open shortcut, default time format, theme.</summary>
public sealed class SettingsPage : UserControl
{
    private readonly AppHost _host;
    private readonly TextBlock _hotkeyText;
    private readonly TextBlock _hotkeyMessage;
    private readonly TextBlock _changeLink;
    private bool _capturing;

    public SettingsPage(AppHost host)
    {
        _host = host;
        Focusable = true;
        var s = host.Settings;

        var rows = new StackPanel();

        // Start with Windows
        var startToggle = new CheckBox { Style = (Style)FindResource("Style.Toggle"), IsChecked = host.Startup.IsEnabled };
        startToggle.Click += (_, _) => _host.SetStartWithWindows(startToggle.IsChecked == true);
        Action<bool> syncStartup = v => startToggle.IsChecked = v;
        _host.StartWithWindowsChanged += syncStartup;
        Unloaded += (_, _) => _host.StartWithWindowsChanged -= syncStartup;
        rows.Children.Add(Row("Start with Windows", "Open VAK automatically when you sign in", startToggle));

        // Shortcut
        _hotkeyText = new TextBlock { Text = s.Hotkey, Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
        _changeLink = new TextBlock { Text = "Change", Style = (Style)FindResource("Style.Text"), Foreground = (System.Windows.Media.Brush)FindResource("Brush.TextDim"), Margin = new Thickness(14, 0, 0, 0), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        _changeLink.MouseLeftButtonUp += (_, _) => StartCapture();
        var hotkeyPanel = new StackPanel { Orientation = Orientation.Horizontal };
        hotkeyPanel.Children.Add(_hotkeyText);
        hotkeyPanel.Children.Add(_changeLink);
        rows.Children.Add(Row("Open shortcut", "Show or hide VAK from anywhere", hotkeyPanel));
        _hotkeyMessage = new TextBlock { Style = (Style)FindResource("Style.TextDim"), FontSize = 12, Margin = new Thickness(0, -6, 0, 6), Visibility = Visibility.Collapsed };
        rows.Children.Add(_hotkeyMessage);

        // Time format
        var format = new SegmentedControl("12-hour", "24-hour") { SelectedIndex = s.Use24Hour ? 1 : 0 };
        format.SelectionChanged += i => { _host.Settings.Use24Hour = i == 1; _host.Save(); };
        rows.Children.Add(Row("Time format", "Used by the notch and the clock", format));

        // Theme
        var theme = new SegmentedControl("Dark") { SelectedIndex = 0, Opacity = 0.5, IsEnabled = false };
        rows.Children.Add(Row("Theme", "More themes soon", theme));

        var card = new Border { Style = (Style)FindResource("Style.Card"), Child = rows, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(5) };
        Content = card;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private UIElement Row(string title, string hint, UIElement control)
    {
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, FontSize = 14 });
        left.Children.Add(new TextBlock { Text = hint, Style = (Style)FindResource("Style.TextDim"), FontSize = 12 });
        var right = new ContentControl { Content = control, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 10) };
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private void StartCapture()
    {
        _capturing = true;
        _hotkeyText.Text = "Press the new shortcut…";
        _changeLink.Text = "Esc to cancel";
        ShowMessage(null);
        Focus();
    }

    private void EndCapture()
    {
        _capturing = false;
        _hotkeyText.Text = _host.Settings.Hotkey;
        _changeLink.Text = "Change";
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
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
        if (name is null) { ShowMessage("That key can't be used. Try a letter, number, F-key or Space."); return; }
        parts.Add(name);

        var text = string.Join("+", parts);
        if (HotkeyParser.TryParse(text) is null) { ShowMessage("Include Ctrl, Alt, Shift or Win with the key."); return; }

        var old = _host.Settings.Hotkey;
        if (_host.TryApplyHotkey?.Invoke(text) == false)
        {
            _host.TryApplyHotkey?.Invoke(old); // the failed attempt released the old shortcut; put it back
            ShowMessage($"{text} is already in use by another app. Pick a different one.");
            return;
        }
        _host.Settings.Hotkey = text;
        _host.Save();
        ShowMessage(null);
        EndCapture();
    }

    private static string? KeyName(Key key)
    {
        if (key >= Key.A && key <= Key.Z) return key.ToString();
        if (key >= Key.D0 && key <= Key.D9) return ((int)(key - Key.D0)).ToString();
        if (key >= Key.F1 && key <= Key.F12) return key.ToString();
        return key switch { Key.Space => "Space", Key.Enter => "Enter", Key.Tab => "Tab", _ => null };
    }

    private void ShowMessage(string? text)
    {
        _hotkeyMessage.Text = text ?? "";
        _hotkeyMessage.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
