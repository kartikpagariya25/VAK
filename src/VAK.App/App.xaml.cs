using System.IO;
using System.Windows;
using VAK.App.Shell;
using VAK.Core.Settings;
using VAK.Core.Sys;

namespace VAK.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private ShellWindow? _shell;
    private HotkeyService? _hotkey;
    private TrayService? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new Mutex(true, "VAK.SingleInstance", out var first);
        if (!first) { Shutdown(); return; }

        if (e.Args.Contains("--gallery")) { new Theme.DesignGalleryWindow().Show(); return; }

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VAK", "settings.json");
        var store = new SettingsStore(path);
        var settings = store.Load();

        _shell = new ShellWindow(settings, new WindowsBatteryService());
        var leftArg = Array.IndexOf(e.Args, "--left");
        if (leftArg >= 0 && leftArg + 1 < e.Args.Length && double.TryParse(e.Args[leftArg + 1], out var left))
            _shell.LeftOverride = left;
        _shell.PositionTopCenter();
        _shell.Show();

        _tray = new TrayService(settings.StartWithWindows);
        _tray.ShowHideRequested += () => _shell.Toggle();
        _tray.QuitRequested += Shutdown;

        _hotkey = new HotkeyService(_shell);
        _hotkey.Pressed += () => _shell.Toggle();
        if (!_hotkey.Register(settings.Hotkey))
            _tray.ShowNotice("VAK", $"The hotkey {settings.Hotkey} is already in use. Hover the top of the screen instead, or change it in Settings.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
