using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using VAK.App.Theme;
using VAK.Core.Clock;
using VAK.Core.Settings;
using VAK.Core.Sys;
using VAK.Core.Timers;

namespace VAK.App.Shell;

public partial class ShellWindow : Window
{
    private const double ExpandedWidth = 800, DefaultExpandedHeight = 388, MaxExpandedHeight = 580, ExpandedRadius = 28;
    private const double MinimalWidth = 44, MinimalHeight = 16;
    private double _expandedHeight = DefaultExpandedHeight;

    private NotchShape _shape = NotchGeometry.Collapsed(new NotchSettings());
    private NotchMode _mode = NotchMode.Full;
    private bool _fullscreen;
    private readonly SolidColorBrush _islandBrush = new(Colors.Black);

    public static readonly DependencyProperty IslandRadiusProperty = DependencyProperty.Register(
        nameof(IslandRadius), typeof(double), typeof(ShellWindow), new PropertyMetadata(18.0, (d, _) => ((ShellWindow)d).ApplyRadii()));
    public static readonly DependencyProperty IslandTopRadiusProperty = DependencyProperty.Register(
        nameof(IslandTopRadius), typeof(double), typeof(ShellWindow), new PropertyMetadata(0.0, (d, _) => ((ShellWindow)d).ApplyRadii()));

    private readonly AppHost _host;
    private AppSettings _settings => _host.Settings;
    private IBatteryService _battery => _host.Battery;
    private readonly HoverWatcher _hover;
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _modeTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _batteryAt = DateTime.MinValue;

    private Core.Message.BannerSpec? _pendingBanner, _currentBanner;
    private bool _showingBanner;
    private string _edgeKey = "";

    public bool IsExpanded { get; private set; }
    public double IslandRadius { get => (double)GetValue(IslandRadiusProperty); set => SetValue(IslandRadiusProperty, value); }
    public double IslandTopRadius { get => (double)GetValue(IslandTopRadiusProperty); set => SetValue(IslandTopRadiusProperty, value); }
    public event Action<bool>? ExpandedChanged;

    private double CollapsedWidth => _mode == NotchMode.Minimal ? MinimalWidth : _shape.Width;
    private double CollapsedHeight => _mode == NotchMode.Minimal ? MinimalHeight : _shape.Height;
    private double CollapsedGap => _mode == NotchMode.Minimal ? 0 : _shape.TopGap;
    private double CollapsedBottomRadius => _mode == NotchMode.Minimal ? 8 : _shape.BottomRadius;
    private double CollapsedTopRadius => _mode == NotchMode.Minimal ? 0 : _shape.TopRadius;

    public ShellWindow(AppHost host)
    {
        InitializeComponent();
        _host = host;
        Island.Background = _islandBrush;
        var content = new ShellContent(host);
        content.CloseRequested += Collapse;
        content.PanelHeightRequested += SetPanelHeight;
        content.PinToggled += pinned => _hover!.KeepOpen = pinned;
        PageHost.Content = content;
        MinimalArrow.Text = Icons.Glyph("chevron");

        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowLong(h, NativeMethods.GWL_EXSTYLE,
                NativeMethods.GetWindowLong(h, NativeMethods.GWL_EXSTYLE) | NativeMethods.WS_EX_TOOLWINDOW);
        };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.Invoke(PositionTopCenter);

        _hover = new HoverWatcher(() => IsExpanded, Regions, Expand, Collapse);
        host.BannerRequested += ShowBanner;
        host.BannerCleared += ClearBanner;
        host.NowPlaying.Changed += _ => RefreshPill();
        host.SettingsApplied += () => Dispatcher.Invoke(() => ApplySettings(true));
        Island.MouseLeftButtonUp += (_, _) => OnPillClicked();

        _clockTimer.Tick += (_, _) => RefreshPill();
        _clockTimer.Start();
        _modeTimer.Tick += (_, _) => RefreshMode();
        _modeTimer.Start();
        ApplySettings(false);
        PositionTopCenter();
        RefreshPill();
    }

    // ---------- settings ----------

    /// <summary>Re-reads the notch, appearance and behaviour settings and applies them to the window.</summary>
    public void ApplySettings(bool animate)
    {
        var s = _settings;
        if (!s.Notch.ShowNotch && IsExpanded) Collapse();
        var themeChanged = ThemeService.Apply(s.Appearance.Theme);
        _shape = NotchGeometry.Collapsed(s.Notch);
        ApplyEffects();
        ApplyPillLayout();
        _hover.OpenOnClick = s.Notch.OpensOn == "click" && _mode != NotchMode.Minimal;
        PositionTopCenter();
        RefreshMode();
        if (!IsExpanded && !_showingBanner) SetCollapsedGeometry(animate);
        else if (IsExpanded) _islandBrush.Color = Themes.Get(s.Appearance.Theme).Light ? ThemeService.Parse(Themes.Get(s.Appearance.Theme).Panel, Colors.Black) : Colors.Black;
        UpdatePillVisibility();
        UpdateEdgeLight();
        RefreshPill();
        if (themeChanged && animate) _host.RaiseThemeChanged();
    }

    private void ApplyEffects()
    {
        var a = _settings.Appearance;
        var theme = Themes.Get(a.Theme);
        EffectHost.Effect = a.Shadow switch
        {
            "subtle" => new DropShadowEffect { BlurRadius = 14, ShadowDepth = 4, Direction = 270, Opacity = 0.35, Color = Colors.Black },
            "medium" => new DropShadowEffect { BlurRadius = 26, ShadowDepth = 8, Direction = 270, Opacity = 0.5, Color = Colors.Black },
            "deep" => new DropShadowEffect { BlurRadius = 42, ShadowDepth = 14, Direction = 270, Opacity = 0.65, Color = Colors.Black },
            _ => null,
        };
        var soft = theme.Light ? 0 : a.SoftEdge switch { "low" => 4, "medium" => 8, "high" => 14, _ => 0 };
        Island.Effect = soft == 0 ? null : new DropShadowEffect { BlurRadius = soft, ShadowDepth = 0, Opacity = 0.9, Color = Colors.Black };
        Island.BorderThickness = new Thickness(a.Outline ? Math.Clamp(a.OutlineWidth, 1, 3) : 0);
        Island.BorderBrush = (Brush)Application.Current.FindResource("Brush.TextFaint");
    }

    private void ApplyPillLayout()
    {
        if (_settings.Notch.RestingLayout == "center")
        {
            PillTime.HorizontalAlignment = HorizontalAlignment.Center; PillTime.Margin = new Thickness(0, 0, 64, 0);
            PillBattery.HorizontalAlignment = HorizontalAlignment.Center; PillBattery.Margin = new Thickness(64, 0, 0, 0);
            PillMusic.HorizontalAlignment = HorizontalAlignment.Center; PillMusic.Margin = new Thickness(64, 0, 0, 0);
        }
        else
        {
            PillTime.HorizontalAlignment = HorizontalAlignment.Left; PillTime.Margin = new Thickness(0);
            PillBattery.HorizontalAlignment = HorizontalAlignment.Right; PillBattery.Margin = new Thickness(0);
            PillMusic.HorizontalAlignment = HorizontalAlignment.Right; PillMusic.Margin = new Thickness(0);
        }
    }

    private void ApplyRadii()
    {
        var r = new CornerRadius(IslandTopRadius, IslandTopRadius, IslandRadius, IslandRadius);
        Island.CornerRadius = r;
        EdgeLight.CornerRadius = r;
    }

    private void UpdatePillVisibility()
    {
        var showPill = _mode == NotchMode.Full && _settings.Notch.Style != "line";
        PillView.Visibility = showPill && !_showingBanner ? Visibility.Visible : Visibility.Collapsed;
        MinimalArrow.Visibility = _mode == NotchMode.Minimal && !IsExpanded && !_showingBanner ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetCollapsedGeometry(bool animate)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        void To(DependencyProperty p, double v)
        {
            if (animate) Island.BeginAnimation(p, new DoubleAnimation(v, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
            else { Island.BeginAnimation(p, null); Island.SetValue(p, v); }
        }
        To(WidthProperty, CollapsedWidth);
        To(HeightProperty, CollapsedHeight);
        Island.Margin = new Thickness(0, CollapsedGap, 0, 0);
        BeginAnimation(IslandRadiusProperty, null); IslandRadius = CollapsedBottomRadius;
        BeginAnimation(IslandTopRadiusProperty, null); IslandTopRadius = CollapsedTopRadius;
        _islandBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        _islandBrush.Color = Colors.Black;
    }

    // ---------- edge light ----------

    private bool EdgeActive => EdgeLightSpec.ShouldPlay(_settings.Appearance.EdgeLight.When, _showingBanner, IsExpanded);

    private void UpdateEdgeLight()
    {
        var e = _settings.Appearance.EdgeLight;
        if (!e.Enabled || !EdgeActive || _mode == NotchMode.Hidden)
        {
            EdgeLight.Visibility = Visibility.Collapsed;
            EdgeLight.BorderBrush = null;
            _edgeKey = "";
            return;
        }
        var key = $"{e.Style}|{e.Color}|{e.Intensity}|{e.Speed}";
        EdgeLight.Visibility = Visibility.Visible;
        if (key == _edgeKey) return;
        _edgeKey = key;

        var color = ThemeService.Parse(e.Color, Colors.White);
        var clear = Color.FromArgb(0, color.R, color.G, color.B);
        var seconds = EdgeLightSpec.LoopSeconds(e.Speed);
        EdgeLight.BeginAnimation(OpacityProperty, null);
        EdgeLight.Opacity = EdgeLightSpec.Opacity(e.Intensity);
        if (e.Style == "breathe")
        {
            EdgeLight.BorderBrush = new SolidColorBrush(color);
            EdgeLight.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2 * EdgeLight.Opacity, EdgeLight.Opacity, TimeSpan.FromSeconds(seconds / 2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
            return;
        }
        var rotate = new RotateTransform(0, 0.5, 0.5);
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5), RelativeTransform = rotate };
        if (e.Style == "comet")
        {
            brush.GradientStops.Add(new GradientStop(clear, 0)); brush.GradientStops.Add(new GradientStop(clear, 0.72));
            brush.GradientStops.Add(new GradientStop(color, 0.93)); brush.GradientStops.Add(new GradientStop(clear, 1));
        }
        else
        {
            brush.GradientStops.Add(new GradientStop(clear, 0)); brush.GradientStops.Add(new GradientStop(color, 0.1));
            brush.GradientStops.Add(new GradientStop(clear, 0.28)); brush.GradientStops.Add(new GradientStop(clear, 0.72));
            brush.GradientStops.Add(new GradientStop(color, 0.9)); brush.GradientStops.Add(new GradientStop(clear, 1));
        }
        EdgeLight.BorderBrush = brush;
        rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(seconds)) { RepeatBehavior = RepeatBehavior.Forever });
    }

    // ---------- showing and hiding ----------

    /// <summary>A page may ask for a taller panel (the AI chat does). The island grows or shrinks to it; collapsed it stays a pill.</summary>
    public void SetPanelHeight(double height)
    {
        height = Math.Clamp(height, DefaultExpandedHeight, MaxExpandedHeight);
        if (Math.Abs(height - _expandedHeight) < 0.5) return;
        _expandedHeight = height;
        PageHost.Height = height;
        if (!IsExpanded) return;
        Island.BeginAnimation(HeightProperty, new DoubleAnimation(height, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    /// <summary>The open shortcut. If the notch was turned off with its power button, this turns it back on and opens it.</summary>
    public void Toggle()
    {
        if (!_settings.Notch.ShowNotch) { _settings.Notch.ShowNotch = true; _host.Apply(); Expand(); return; }
        if (IsExpanded) Collapse(); else Expand();
    }

    private void OnPillClicked()
    {
        if (IsExpanded) return;
        var timersShowing = PillTimer.Visibility == Visibility.Visible;
        Expand();
        if (timersShowing) _host.Nav.GoTo("timers");
    }

    /// <summary>Flashes a banner on the collapsed notch. If the panel is open, it waits until the panel closes.</summary>
    public void ShowBanner(Core.Message.BannerSpec spec)
    {
        if (IsExpanded) { _pendingBanner = spec; return; }
        if (!LiveActivityPolicy.MayShowNow(_settings.Notch, _mode)) return;
        _pendingBanner = null;
        _currentBanner = spec;
        _showingBanner = true;
        if (Visibility != Visibility.Visible) Visibility = Visibility.Visible;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        PillView.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(120)));
        Island.BeginAnimation(WidthProperty, new DoubleAnimation(Controls.BannerView.PreferredWidth(spec), TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        Island.BeginAnimation(HeightProperty, new DoubleAnimation(Math.Max(CollapsedHeight, 30), TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        MinimalArrow.Visibility = Visibility.Collapsed;
        BannerLayer.Visibility = Visibility.Visible;
        UpdateEdgeLight();
        BannerLayer.Play(spec, EndBanner);
    }

    private void EndBanner()
    {
        if (!_showingBanner) return;
        _showingBanner = false;
        _currentBanner = null;
        BannerLayer.Visibility = Visibility.Collapsed;
        SetCollapsedGeometry(true);
        PillView.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
        UpdatePillVisibility();
        UpdateEdgeLight();
        RefreshMode();
    }

    public void ClearBanner()
    {
        _pendingBanner = null;
        if (!_showingBanner) return;
        BannerLayer.Stop();
        EndBanner();
    }

    public void Expand()
    {
        if (IsExpanded) return;
        if (_showingBanner)
        {
            if (_currentBanner is { Repeat: true } keep) _pendingBanner = keep; // a "stay until cleared" banner returns after the panel closes
            _showingBanner = false;
            BannerLayer.Stop();
            BannerLayer.Visibility = Visibility.Collapsed;
        }
        if (Visibility != Visibility.Visible) Visibility = Visibility.Visible;
        IsExpanded = true;
        _hover.NotifyExpanded();
        UpdatePillVisibility();
        Animate(true);
        UpdateEdgeLight();
        ExpandedChanged?.Invoke(true);
    }

    public void Collapse()
    {
        if (!IsExpanded) return;
        IsExpanded = false;
        Animate(false);
        UpdatePillVisibility();
        UpdateEdgeLight();
        RefreshMode();
        ExpandedChanged?.Invoke(false);
        if (_pendingBanner is { } pending)
        {
            var wait = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            wait.Tick += (_, _) => { wait.Stop(); if (!IsExpanded) ShowBanner(pending); };
            wait.Start();
        }
    }

    private void Animate(bool expand)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        DoubleAnimation Da(double to, int ms = 250, int delay = 0) =>
            new(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(delay) };

        var panelColor = ThemeService.Parse(Themes.Get(_settings.Appearance.Theme).Panel, Colors.Black);
        Island.BeginAnimation(WidthProperty, Da(expand ? ExpandedWidth : CollapsedWidth));
        Island.BeginAnimation(HeightProperty, Da(expand ? _expandedHeight : CollapsedHeight));
        Island.BeginAnimation(MarginProperty, new ThicknessAnimation(new Thickness(0, expand ? 0 : CollapsedGap, 0, 0), TimeSpan.FromMilliseconds(250)) { EasingFunction = ease });
        BeginAnimation(IslandRadiusProperty, Da(expand ? ExpandedRadius : CollapsedBottomRadius));
        BeginAnimation(IslandTopRadiusProperty, Da(expand ? 0 : CollapsedTopRadius));
        _islandBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(expand ? panelColor : Colors.Black, TimeSpan.FromMilliseconds(250)));
        PillView.BeginAnimation(OpacityProperty, Da(expand ? 0 : 1, 120, expand ? 0 : 130));
        PageHost.BeginAnimation(OpacityProperty, Da(expand ? 1 : 0, 150, expand ? 100 : 0));
        PageHost.IsHitTestVisible = expand;
    }

    /// <summary>Centres the notch on the chosen display. Works in DIPs so any display scale lands in the same place.</summary>
    public void PositionTopCenter()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var want = _settings.Notch.Display;
        var screen = (want != "primary" ? screens.FirstOrDefault(s => s.DeviceName == want) : null) ?? System.Windows.Forms.Screen.PrimaryScreen ?? screens[0];
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        Left = LeftOverride ?? (screen.Bounds.X + screen.Bounds.Width / 2.0) / scale - Width / 2;
        Top = screen.Bounds.Y / scale;
    }

    /// <summary>Dev-only (--left N): pins the window's left edge so VAK can run beside another notch app.</summary>
    public double? LeftOverride { get; set; }

    private (Rect Trigger, Rect Panel) Regions()
    {
        var s = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var origin = PointToScreen(new Point(0, 0)); // physical px of the window's top-left
        var centerX = origin.X + Width * s / 2;

        // The notch itself plus a small margin, not a wide strip: reaching for a title bar or tab near the top must not pop the panel.
        var w = Math.Max(CollapsedWidth, 60);
        var trigger = new Rect(centerX - (w / 2 + 8) * s, origin.Y - 5, (w + 16) * s, (CollapsedGap + CollapsedHeight + 8) * s);
        var panel = new Rect(centerX - (ExpandedWidth / 2 + 6) * s, origin.Y, (ExpandedWidth + 12) * s, (_expandedHeight + 6) * s);
        return (trigger, panel);
    }

    // ---------- the resting pill ----------

    private void RefreshPill()
    {
        PillTime.Text = ClockFormatter.Time(DateTimeOffset.Now, TimeZoneInfo.Local, _settings.Use24Hour, false);
        RefreshTimerPill();
        RefreshMusicPill();
        if (DateTime.UtcNow - _batteryAt < TimeSpan.FromSeconds(30)) return;
        _batteryAt = DateTime.UtcNow;
        var b = _battery.Read();
        PillBattery.Visibility = b is null ? Visibility.Collapsed : Visibility.Visible;
        if (b is null) return;
        PillBatteryIcon.Text = Icons.Glyph(b.Charging ? "charge" : "battery");
        PillBatteryText.Text = b.Percent.ToString();
    }

    /// <summary>Beat bars on the closed notch while music plays, in the album colour or the colour chosen in Live Activities.</summary>
    private void RefreshMusicPill()
    {
        var live = _settings.Live;
        var track = _host.NowPlaying.Current;
        var music = live.NowPlaying && track is { Playing: true };
        PillMusic.Visibility = music ? Visibility.Visible : Visibility.Collapsed;
        PillMusic.Run(music);
        if (!music) return;
        PillMusic.SetColor(live.MusicColor == "auto" ? track!.Color : ThemeService.Parse(live.MusicColor, Colors.White));
        if (_settings.Notch.RestingLayout != "center")
            PillMusic.Margin = new Thickness(0, 0, PillBattery.Visibility == Visibility.Visible ? 50 : 0, 0);
    }

    /// <summary>A running Pomodoro, countdown or stopwatch shows in the middle of the closed notch; clicking it opens Timers.</summary>
    private void RefreshTimerPill()
    {
        var t = _host.Timers;
        string? text = null;
        if (_settings.Live.RunningTimers)
        {
            if (t.Pomodoro.Running) text = TimerFormatter.Mmss(t.Pomodoro.Remaining);
            else if (t.Countdown.Running) text = TimerFormatter.Mmss(t.Countdown.Remaining);
            else if (t.Stopwatch.Running) text = TimerFormatter.Mmss(t.Stopwatch.Elapsed);
        }
        PillTimer.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
        if (text is null) return;
        PillTimerIcon.Text = Icons.Glyph("timers");
        PillTimerText.Text = text;
    }

    // ---------- visibility ----------

    private void RefreshMode()
    {
        // Windows reports "busy" (2) for many things, including screenshot tools; only trust it when a window really covers the display.
        var hide = NativeMethods.SHQueryUserNotificationState(out var state) == 0 && (state is 3 or 4 || state == 2 && ForegroundCoversNotchDisplay());
        _fullscreen = hide;
        var mode = NotchPolicy.Decide(_settings.Notch, hide, ForegroundMaximizedOnNotchDisplay());
        if (IsExpanded || _showingBanner) mode = NotchMode.Full;
        var changed = mode != _mode;
        _mode = mode;
        _hover.Enabled = mode != NotchMode.Hidden;
        _hover.OpenOnClick = _settings.Notch.OpensOn == "click" && mode != NotchMode.Minimal;
        var visibility = mode == NotchMode.Hidden ? Visibility.Hidden : Visibility.Visible;
        if (Visibility != visibility) Visibility = visibility;
        if (!changed) return;
        UpdatePillVisibility();
        if (!IsExpanded && !_showingBanner) SetCollapsedGeometry(true);
        UpdateEdgeLight();
    }

    private System.Windows.Forms.Screen NotchScreen()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var want = _settings.Notch.Display;
        return (want != "primary" ? screens.FirstOrDefault(s => s.DeviceName == want) : null) ?? System.Windows.Forms.Screen.PrimaryScreen ?? screens[0];
    }

    /// <summary>True when the front window (not the desktop, not VAK) covers the whole notch display: a real fullscreen app or video.</summary>
    private bool ForegroundCoversNotchDisplay()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == Environment.ProcessId) return false;
        var name = new System.Text.StringBuilder(64);
        NativeMethods.GetClassName(hwnd, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        if (!NativeMethods.GetWindowRect(hwnd, out var r)) return false;
        var b = NotchScreen().Bounds;
        return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
    }

    private bool ForegroundMaximizedOnNotchDisplay()
    {
        if (_settings.Notch.Visibility != "smart") return false;
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || !NativeMethods.IsZoomed(hwnd)) return false;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == Environment.ProcessId) return false;
        if (!NativeMethods.GetWindowRect(hwnd, out var r)) return false;
        var screens = System.Windows.Forms.Screen.AllScreens;
        var want = _settings.Notch.Display;
        var screen = (want != "primary" ? screens.FirstOrDefault(s => s.DeviceName == want) : null) ?? System.Windows.Forms.Screen.PrimaryScreen ?? screens[0];
        return screen.Bounds.Contains((r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2);
    }
}
