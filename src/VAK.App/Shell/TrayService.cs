using System.Windows.Forms;
using Microsoft.Win32;

namespace VAK.App.Shell;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _startup;

    public event Action? ShowHideRequested;
    public event Action<bool>? StartWithWindowsChanged;
    public event Action? SettingsRequested;
    public event Action? QuitRequested;

    public TrayService(bool startWithWindows)
    {
        _startup = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = startWithWindows };
        _startup.CheckedChanged += (_, _) => StartWithWindowsChanged?.Invoke(_startup.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show / Hide", null, (_, _) => ShowHideRequested?.Invoke());
        menu.Items.Add("Settings…", null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add(_startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit VAK", null, (_, _) => QuitRequested?.Invoke());

        _icon = new NotifyIcon { Text = "VAK", Visible = true, ContextMenuStrip = menu, Icon = LoadIcon() };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowHideRequested?.Invoke(); };
    }

    /// <summary>Hover text of the tray icon (Windows allows 63 characters).</summary>
    public void SetTooltip(string text) => _icon.Text = text.Length > 63 ? text[..63] : text;

    public void SetStartWithWindows(bool value) => _startup.Checked = value;

    public void ShowNotice(string title, string text) =>
        _icon.ShowBalloonTip(4000, title, text, ToolTipIcon.Info);

    private static System.Drawing.Icon LoadIcon()
    {
        var light = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme", 0) is int v && v == 1;
        var name = light ? "tray-light.ico" : "tray-dark.ico";
        var info = System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/Brand/{name}"));
        return info is null ? System.Drawing.SystemIcons.Application : new System.Drawing.Icon(info.Stream);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
