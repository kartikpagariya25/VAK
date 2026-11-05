using System.IO;
using System.Windows;
using VAK.App.Services;
using VAK.App.Shell;
using VAK.Core.Settings;
using VAK.Core.Sys;

namespace VAK.App;

public partial class App : Application
{
    private const string ShowSignalName = "VAK.ShowSignal";
    private Mutex? _singleInstance;
    private EventWaitHandle? _showSignal;
    private volatile bool _exiting;
    private ShellWindow? _shell;
    private HotkeyService? _hotkey;
    private TrayService? _tray;
    private ClipboardMonitor? _clipboard;
    private ScreenTimeTracker? _screenTime;
    private AppHost? _host;
    private System.Windows.Threading.DispatcherTimer? _dropGuard;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            args.Handled = true; // a tray app that runs all day should survive a bad click or tick
        };
        _singleInstance = new Mutex(true, "VAK.SingleInstance", out var first);
        if (!first)
        {
            // VAK is already running (maybe with the notch turned off): ask it to show itself instead of silently doing nothing.
            try { EventWaitHandle.OpenExisting(ShowSignalName).Set(); } catch (Exception ex) when (ex is WaitHandleCannotBeOpenedException or UnauthorizedAccessException or IOException) { }
            Shutdown();
            return;
        }
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);

        if (e.Args.Contains("--gallery")) { new Theme.DesignGalleryWindow().Show(); return; }

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VAK", "settings.json");
        var host = _host = new AppHost(path);
        var settings = host.Settings;

        host.ClipboardMonitor = _clipboard = new ClipboardMonitor(host.Clipboard, host.DataDir);
        host.ScreenTimeTracker = _screenTime = new ScreenTimeTracker(host.ScreenTime);
        _clipboard.Enabled = () => settings.Privacy.KeepClipboardHistory;
        _screenTime.Enabled = () => settings.Privacy.TrackScreenTime;
        host.NowPlaying.Enabled = () => settings.Live.NowPlaying;
        host.NowPlaying.AllowBrowser = () => settings.Live.BrowserMedia;
        host.NowPlaying.Start();
        host.SettingsApplied += host.NowPlaying.Reevaluate;
        host.OpenSettings = OpenSettings;
        _shell = new ShellWindow(host);
        host.LiveActivities = new LiveActivities(host);
        host.LiveActivities.Start(_clipboard);
        var leftArg = Array.IndexOf(e.Args, "--left");
        if (leftArg >= 0 && leftArg + 1 < e.Args.Length && double.TryParse(e.Args[leftArg + 1], out var left))
            _shell.LeftOverride = left;
        _shell.PositionTopCenter();
        _shell.Show();

        _tray = new TrayService(settings.StartWithWindows);
        _tray.ShowHideRequested += () => _shell.Toggle();
        _tray.QuitRequested += Shutdown;
        host.NotchTurnedOff += () => _tray.ShowNotice("VAK is off", $"Press {settings.Hotkey} to bring the notch back. Quit it from the tray icon.");
        _tray.SettingsRequested += () => OpenSettings(null);
        _tray.StartWithWindowsChanged += host.SetStartWithWindows;
        host.StartWithWindowsChanged += _tray.SetStartWithWindows;

        host.Timers.Notice += message =>
        {
            System.Media.SystemSounds.Asterisk.Play();
            _tray.ShowNotice("VAK", message);
            host.LiveActivities.Flash(LiveKind.TimerDone, message, "#FF9F0A");
        };

        // Drop is only allowed while the network is private. This watches for the network changing (a laptop moving to a cafe),
        // switches Drop off when it does, tells the user, and shows in the tray tooltip whenever Drop is on.
        host.Drop.Changed += () => Dispatcher.Invoke(() => _tray.SetTooltip(host.Drop.Running ? "VAK - Drop is on" : "VAK"));
        host.Drop.AutoStopped += () => Dispatcher.Invoke(() => _tray.ShowNotice("VAK Drop", "Drop switched itself off after sitting idle."));
        _dropGuard = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _dropGuard.Tick += async (_, _) =>
        {
            if (host.Drop.Running is false || host.Drop.Url is not { } bound) return;
            var (state, address) = await Task.Run(NetworkCategory.Check);
            if (host.Drop.Running && (state != NetworkState.Private || address?.ToString() != new Uri(bound).Host))
            {
                await host.Drop.StopAsync();
                _tray.ShowNotice("VAK Drop", "Drop was switched off because the network changed.");
            }
        };
        _dropGuard.Start();

        host.Calendar.Reminder += o =>
        {
            var lead = o.Event.ReminderMinutes;
            var when = lead <= 0 ? "starting now" : lead >= 60 ? "in 1 hour" : $"in {lead} min";
            var text = $"{o.Event.Title} · {when}";
            if (!LiveActivityPolicy.Allows(settings.Live, LiveKind.CalendarAlert)) return;
            System.Media.SystemSounds.Asterisk.Play();
            if (LiveActivityPolicy.AlsoWindowsNotice(settings.Live)) _tray.ShowNotice("Calendar", text);
            if (Core.Message.BannerSpec.TryCreate(text, Core.Message.BannerAnimation.Slide, 1, o.Event.Color, Core.Message.BannerFinish.Solid, 10, false) is { } banner)
                host.FlashBanner(banner);
        };

        new Thread(() =>
        {
            while (_showSignal!.WaitOne() && !_exiting) Dispatcher.BeginInvoke(() => _shell?.Reveal());
        }) { IsBackground = true, Name = "VAK show signal" }.Start();

        var settingsArg = Array.IndexOf(e.Args, "--settings");
        if (settingsArg >= 0) OpenSettings(settingsArg + 1 < e.Args.Length && !e.Args[settingsArg + 1].StartsWith("--") ? e.Args[settingsArg + 1] : null);

        _hotkey = new HotkeyService(_shell);
        _hotkey.Pressed += () => _shell.Toggle();
        host.TryApplyHotkey = hk => _hotkey.Register(hk);
        if (!_hotkey.Register(settings.Hotkey))
            _tray.ShowNotice("VAK", $"The hotkey {settings.Hotkey} is already in use. Hover the top of the screen instead, or change it in Settings.");
    }

    private Settings.SettingsWindow? _settingsWindow;

    private void OpenSettings(string? section)
    {
        if (_host is null) return;
        if (_settingsWindow is null)
        {
            _settingsWindow = new Settings.SettingsWindow(_host);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        if (section is not null) _settingsWindow.Go(section);
        if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
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
        _exiting = true;
        try { _showSignal?.Set(); } catch (ObjectDisposedException) { }
        _hotkey?.Dispose();
        _clipboard?.Dispose();
        _screenTime?.Flush();
        _dropGuard?.Stop();
        _host?.Drop.StopAsync().Wait(TimeSpan.FromSeconds(2));
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
