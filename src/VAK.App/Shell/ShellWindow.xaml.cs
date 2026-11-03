using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using VAK.App.Theme;
using VAK.Core.Clock;
using VAK.Core.Settings;
using VAK.Core.Sys;

namespace VAK.App.Shell;

public partial class ShellWindow : Window
{
    private const double CollapsedWidth = 200, CollapsedHeight = 34, CollapsedRadius = 18;
    private const double ExpandedWidth = 800, ExpandedHeight = 388, ExpandedRadius = 28;

    public static readonly DependencyProperty IslandRadiusProperty = DependencyProperty.Register(
        nameof(IslandRadius), typeof(double), typeof(ShellWindow),
        new PropertyMetadata(CollapsedRadius, (d, e) =>
            ((ShellWindow)d).Island.CornerRadius = new CornerRadius(0, 0, (double)e.NewValue, (double)e.NewValue)));

    private readonly AppHost _host;
    private AppSettings _settings => _host.Settings;
    private IBatteryService _battery => _host.Battery;
    private readonly HoverWatcher _hover;
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _fullscreenTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _batteryAt = DateTime.MinValue;

    public bool IsExpanded { get; private set; }
    public double IslandRadius { get => (double)GetValue(IslandRadiusProperty); set => SetValue(IslandRadiusProperty, value); }
    public event Action<bool>? ExpandedChanged;

    public ShellWindow(AppHost host)
    {
        InitializeComponent();
        _host = host;
        var content = new ShellContent(host);
        content.CloseRequested += Collapse;
        content.PinToggled += pinned => _hover!.KeepOpen = pinned;
        PageHost.Content = content;
        PositionTopCenter();

        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowLong(h, NativeMethods.GWL_EXSTYLE,
                NativeMethods.GetWindowLong(h, NativeMethods.GWL_EXSTYLE) | NativeMethods.WS_EX_TOOLWINDOW);
        };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.Invoke(PositionTopCenter);

        _hover = new HoverWatcher(() => IsExpanded, Regions, Expand, Collapse);

        _clockTimer.Tick += (_, _) => RefreshPill();
        _clockTimer.Start();
        _fullscreenTimer.Tick += (_, _) => CheckFullscreen();
        _fullscreenTimer.Start();
        RefreshPill();
    }

    public void Toggle() { if (IsExpanded) Collapse(); else Expand(); }

    public void Expand()
    {
        if (IsExpanded) return;
        IsExpanded = true;
        _hover.NotifyExpanded();
        Animate(true);
        ExpandedChanged?.Invoke(true);
    }

    public void Collapse()
    {
        if (!IsExpanded) return;
        IsExpanded = false;
        Animate(false);
        ExpandedChanged?.Invoke(false);
    }

    private void Animate(bool expand)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        DoubleAnimation Da(double to, int ms = 250, int delay = 0) =>
            new(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(delay) };

        Island.BeginAnimation(WidthProperty, Da(expand ? ExpandedWidth : CollapsedWidth));
        Island.BeginAnimation(HeightProperty, Da(expand ? ExpandedHeight : CollapsedHeight));
        BeginAnimation(IslandRadiusProperty, Da(expand ? ExpandedRadius : CollapsedRadius));
        PillView.BeginAnimation(OpacityProperty, Da(expand ? 0 : 1, 120, expand ? 0 : 130));
        PageHost.BeginAnimation(OpacityProperty, Da(expand ? 1 : 0, 150, expand ? 100 : 0));
        PageHost.IsHitTestVisible = expand;
    }

    /// <summary>Re-centers on the primary display. Works in DIPs so any display scale lands in the same place.</summary>
    public void PositionTopCenter()
    {
        Left = LeftOverride ?? (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;
    }

    /// <summary>Dev-only (--left N): pins the window's left edge so VAK can run beside another notch app.</summary>
    public double? LeftOverride { get; set; }

    private (Rect Trigger, Rect Panel) Regions()
    {
        var s = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var origin = PointToScreen(new Point(0, 0)); // physical px of the window's top-left
        var centerX = origin.X + Width * s / 2;

        // The notch itself plus a small margin, not a wide strip: reaching for a title bar or tab near the top must not pop the panel.
        var trigger = new Rect(centerX - (CollapsedWidth / 2 + 8) * s, origin.Y - 5, (CollapsedWidth + 16) * s, (CollapsedHeight + 8) * s);
        var panel = new Rect(centerX - (ExpandedWidth / 2 + 6) * s, origin.Y, (ExpandedWidth + 12) * s, (ExpandedHeight + 6) * s);
        return (trigger, panel);
    }

    private void RefreshPill()
    {
        PillTime.Text = ClockFormatter.Time(DateTimeOffset.Now, TimeZoneInfo.Local, _settings.Use24Hour, false);
        if (DateTime.UtcNow - _batteryAt < TimeSpan.FromSeconds(30)) return;
        _batteryAt = DateTime.UtcNow;
        var b = _battery.Read();
        PillBattery.Visibility = b is null ? Visibility.Collapsed : Visibility.Visible;
        if (b is null) return;
        PillBatteryIcon.Text = Icons.Glyph(b.Charging ? "charge" : "battery");
        PillBatteryText.Text = b.Percent.ToString();
    }

    private void CheckFullscreen()
    {
        var hide = NativeMethods.SHQueryUserNotificationState(out var state) == 0 && state is 2 or 3 or 4;
        Visibility = hide ? Visibility.Hidden : Visibility.Visible;
        _hover.Enabled = !hide;
        if (hide && IsExpanded) Collapse();
    }
}
