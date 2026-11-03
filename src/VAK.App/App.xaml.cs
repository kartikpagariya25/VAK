using System.IO;
using System.Windows;
using VAK.App.Services;
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
    private ClipboardMonitor? _clipboard;
    private ScreenTimeTracker? _screenTime;
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            args.Handled = true; // a tray app that runs all day should survive a bad click or tick
        };
        _singleInstance = new Mutex(true, "VAK.SingleInstance", out var first);
        if (!first) { Shutdown(); return; }

        if (e.Args.Contains("--gallery")) { new Theme.DesignGalleryWindow().Show(); return; }

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VAK", "settings.json");
        var host = _host = new AppHost(path);
        var settings = host.Settings;

        host.ClipboardMonitor = _clipboard = new ClipboardMonitor(host.Clipboard, host.DataDir);
        host.ScreenTimeTracker = _screenTime = new ScreenTimeTracker(host.ScreenTime);
        _shell = new ShellWindow(host);
        var leftArg = Array.IndexOf(e.Args, "--left");
        if (leftArg >= 0 && leftArg + 1 < e.Args.Length && double.TryParse(e.Args[leftArg + 1], out var left))
            _shell.LeftOverride = left;
        _shell.PositionTopCenter();
        _shell.Show();

        _tray = new TrayService(settings.StartWithWindows);
        _tray.ShowHideRequested += () => _shell.Toggle();
        _tray.QuitRequested += Shutdown;
        _tray.StartWithWindowsChanged += host.SetStartWithWindows;
        host.StartWithWindowsChanged += _tray.SetStartWithWindows;

        host.Timers.Notice += message =>
        {
            System.Media.SystemSounds.Asterisk.Play();
            _tray.ShowNotice("VAK", message);
        };

        _hotkey = new HotkeyService(_shell);
        _hotkey.Pressed += () => _shell.Toggle();
        host.TryApplyHotkey = hk => _hotkey.Register(hk);
        if (!_hotkey.Register(settings.Hotkey))
            _tray.ShowNotice("VAK", $"The hotkey {settings.Hotkey} is already in use. Hover the top of the screen instead, or change it in Settings.");
    }

    private static void LogError(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VAK");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), $"{DateTime.Now:O}\n{ex}\n\n");
        }
        catch { /* logging must never throw */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _clipboard?.Dispose();
        _screenTime?.Flush();
        _host?.Drop.StopAsync().Wait(TimeSpan.FromSeconds(2));
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
