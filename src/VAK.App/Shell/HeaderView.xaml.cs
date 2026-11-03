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

    private bool _pinned;

    public HeaderView()
    {
        InitializeComponent();
        PinIcon.Text = Icons.Glyph("pin");
        SettingsIcon.Text = Icons.Glyph("settings");
        CloseIcon.Text = Icons.Glyph("close");
        PinButton.Click += (_, _) =>
        {
            _pinned = !_pinned;
            PinIcon.Foreground = _pinned ? (System.Windows.Media.Brush)FindResource("Brush.Accent") : (System.Windows.Media.Brush)FindResource("Brush.Text");
            PinToggled?.Invoke(_pinned);
        };
        SettingsButton.Click += (_, _) => SettingsClicked?.Invoke();
        CloseButton.Click += (_, _) => CloseClicked?.Invoke();
    }

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
