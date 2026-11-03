using System.Windows;
using System.Windows.Controls;
using VAK.App.Theme;
using VAK.Core.Sys;

namespace VAK.App.Shell;

public partial class HeaderView : UserControl
{
    public event Action? SettingsClicked;
    public event Action? CloseClicked;
    public event Action<bool>? PinToggled;
    public event Action<bool>? EditToggled;

    private bool _pinned;
    private bool _isHome;
    private bool _editing;

    public HeaderView()
    {
        InitializeComponent();
        System.Windows.Automation.AutomationProperties.SetName(PinButton, "Pin open or edit");
        System.Windows.Automation.AutomationProperties.SetName(SettingsButton, "Settings");
        System.Windows.Automation.AutomationProperties.SetName(CloseButton, "Close");
        PinIcon.Text = Icons.Glyph("pin");
        SettingsIcon.Text = Icons.Glyph("settings");
        CloseIcon.Text = Icons.Glyph("close");
        PinButton.Click += (_, _) =>
        {
            if (_isHome)
            {
                SetEditing(!_editing);
                EditToggled?.Invoke(_editing);
                return;
            }
            _pinned = !_pinned;
            PinIcon.Foreground = _pinned ? (System.Windows.Media.Brush)FindResource("Brush.Accent") : (System.Windows.Media.Brush)FindResource("Brush.Text");
            PinToggled?.Invoke(_pinned);
        };
        SettingsButton.Click += (_, _) => SettingsClicked?.Invoke();
        CloseButton.Click += (_, _) => CloseClicked?.Invoke();
    }

    /// <summary>On Home the pin slot becomes the Edit button; everywhere else it keeps the panel open.</summary>
    public void SetHomeMode(bool isHome)
    {
        _isHome = isHome;
        _editing = false;
        PinButton.ToolTip = isHome ? "Edit Home" : "Keep open";
        PinIcon.Text = Icons.Glyph(isHome ? "edit" : "pin");
        PinIcon.Foreground = isHome || !_pinned ? (System.Windows.Media.Brush)FindResource("Brush.Text") : (System.Windows.Media.Brush)FindResource("Brush.Accent");
    }

    private void SetEditing(bool editing)
    {
        _editing = editing;
        PinIcon.Text = Icons.Glyph(editing ? "check" : "edit");
        PinIcon.Foreground = editing ? (System.Windows.Media.Brush)FindResource("Brush.Accent") : (System.Windows.Media.Brush)FindResource("Brush.Text");
    }

    /// <summary>Places a page's own controls (or nothing) to the left of the battery chip.</summary>
    public void SetExtras(UIElement? extras) => Extras.Content = extras;

    public void SetPage(string title, string subtitle)
    {
        Title.Text = title;
        Subtitle.Text = subtitle;
        Divider.Visibility = Subtitle.Visibility = string.IsNullOrEmpty(subtitle) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Shows the battery chip, or hides it when the machine has no battery.</summary>
    public void SetBattery(BatteryInfo? battery)
    {
        BatteryChip.Visibility = battery is null ? Visibility.Collapsed : Visibility.Visible;
        if (battery is null) return;
        BatteryText.Text = battery.Percent + "%";
        BatteryIcon.Text = Icons.Glyph(battery.Charging ? "charge" : "battery");
    }
}
