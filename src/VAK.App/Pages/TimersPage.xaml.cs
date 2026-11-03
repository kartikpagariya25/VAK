using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VAK.App.Theme;
using VAK.Core.Settings;
using VAK.Core.Timers;

namespace VAK.App.Pages;

public partial class TimersPage : UserControl
{
    private enum Mode { Pomodoro, Countdown, Stopwatch, Water }

    private readonly AppHost _host;
    private Mode _mode = Mode.Pomodoro;
    private readonly List<(Mode Mode, Border Row, TextBlock Sub)> _rows = new();
    private readonly List<Action> _stepperRefreshers = new();
    private TextBlock _pomoPlay = null!, _countdownPlay = null!, _stopwatchPlay = null!;

    public TimersPage(AppHost host)
    {
        InitializeComponent();
        _host = host;
        var t = _host.Settings.Timers;

        BuildModeList();
        BuildPomodoroControls(t);
        BuildCountdownControls(t);
        BuildStopwatchControls();
        BuildWaterControls();
        AutoToggle.IsChecked = t.AutoStartNext;
        AutoToggle.Click += (_, _) => { t.AutoStartNext = AutoToggle.IsChecked == true; _host.Save(); };

        Loaded += (_, _) => { _host.Timers.Changed += Render; Render(); };
        Unloaded += (_, _) => _host.Timers.Changed -= Render;
    }

    // ---- building ---------------------------------------------------------------------------

    private void BuildModeList()
    {
        void Add(Mode mode, string icon, string title)
        {
            var sub = new TextBlock { Style = (Style)FindResource("Style.TextDim"), FontSize = 11 };
            var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, FontSize = 14 });
            text.Children.Add(sub);
            var grid = new StackPanel { Orientation = Orientation.Horizontal };
            grid.Children.Add(new TextBlock { Text = Icons.Glyph(icon), Style = (Style)FindResource("Style.Icon"), FontSize = 20, Width = 24 });
            grid.Children.Add(text);
            var row = new Border { Child = grid, Padding = new Thickness(12, 10, 12, 10), CornerRadius = new CornerRadius(14), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 4), Background = Brushes.Transparent };
            row.MouseLeftButtonUp += (_, _) => { _mode = mode; Render(); };
            ModeList.Children.Add(row);
            _rows.Add((mode, row, sub));
        }
        Add(Mode.Pomodoro, "timers", "Pomodoro");
        Add(Mode.Countdown, "hourglass", "Countdown");
        Add(Mode.Stopwatch, "clock", "Stopwatch");
        Add(Mode.Water, "water", "Water");
    }

    private Button CircleButton(string icon, double size, Brush? background, out TextBlock iconBlock, string tip)
    {
        iconBlock = new TextBlock { Text = Icons.Glyph(icon), Style = (Style)FindResource("Style.Icon"), FontSize = size > 40 ? 22 : 16 };
        return new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = size, Height = size, Margin = new Thickness(6, 0, 6, 0),
            Background = background ?? (Brush)FindResource("Brush.Control"), Content = iconBlock, ToolTip = tip,
        };
    }

    private void BuildPomodoroControls(TimerSettings t)
    {
        var timers = _host.Timers;
        var reset = CircleButton("reset", 38, null, out _, "Reset");
        reset.Click += (_, _) => { timers.Pomodoro.Reset(); timers.Touch(); };
        var play = CircleButton("play", 54, (Brush)FindResource("Brush.Primary"), out _pomoPlay, "Start or pause");
        play.Click += (_, _) => { var p = timers.Pomodoro; if (p.Running) p.Pause(); else p.Start(); timers.Touch(); };
        var skip = CircleButton("next", 38, null, out _, "Skip to next phase");
        skip.Click += (_, _) => { timers.Pomodoro.Skip(); timers.Touch(); };
        PomoButtons.Children.Add(reset); PomoButtons.Children.Add(play); PomoButtons.Children.Add(skip);

        Steppers.Children.Add(Stepper(() => $"Focus {t.FocusMinutes}m", () => t.FocusMinutes = Math.Max(5, t.FocusMinutes - 5), () => t.FocusMinutes = Math.Min(180, t.FocusMinutes + 5), _host.Timers.PomodoroSettingsChanged));
        Steppers.Children.Add(Stepper(() => $"Break {t.BreakMinutes}m", () => t.BreakMinutes = Math.Max(1, t.BreakMinutes - 1), () => t.BreakMinutes = Math.Min(60, t.BreakMinutes + 1), _host.Timers.PomodoroSettingsChanged));
        Steppers.Children.Add(Stepper(() => $"Long {t.LongBreakMinutes}m", () => t.LongBreakMinutes = Math.Max(5, t.LongBreakMinutes - 5), () => t.LongBreakMinutes = Math.Min(60, t.LongBreakMinutes + 5), _host.Timers.PomodoroSettingsChanged));
        Steppers.Children.Add(Stepper(() => $"Cycles {t.Cycles}", () => t.Cycles = Math.Max(1, t.Cycles - 1), () => t.Cycles = Math.Min(12, t.Cycles + 1), _host.Timers.PomodoroSettingsChanged));
    }

    private void BuildCountdownControls(TimerSettings t)
    {
        var timers = _host.Timers;
        var reset = CircleButton("reset", 38, null, out _, "Reset");
        reset.Click += (_, _) => { timers.Countdown.Reset(); timers.Touch(); };
        var play = CircleButton("play", 54, (Brush)FindResource("Brush.Primary"), out _countdownPlay, "Start or pause");
        play.Click += (_, _) => { var c = timers.Countdown; if (c.Running) c.Pause(); else c.Start(); timers.Touch(); };
        CountdownButtons.Children.Add(reset); CountdownButtons.Children.Add(play);
        CountdownStepper.Content = Stepper(() => $"Duration {t.CountdownMinutes}m",
            () => t.CountdownMinutes = Math.Max(1, t.CountdownMinutes - 1), () => t.CountdownMinutes = Math.Min(180, t.CountdownMinutes + 1), _host.Timers.CountdownSettingsChanged);
    }

    private void BuildStopwatchControls()
    {
        var timers = _host.Timers;
        var reset = CircleButton("reset", 38, null, out _, "Reset");
        reset.Click += (_, _) => { timers.Stopwatch.Reset(); timers.Touch(); };
        var play = CircleButton("play", 54, (Brush)FindResource("Brush.Primary"), out _stopwatchPlay, "Start or pause");
        play.Click += (_, _) => { var s = timers.Stopwatch; if (s.Running) s.Pause(); else s.Start(); timers.Touch(); };
        StopwatchButtons.Children.Add(reset); StopwatchButtons.Children.Add(play);
    }

    private void BuildWaterControls()
    {
        var remove = CircleButton("minus", 38, null, out _, "Remove a glass");
        remove.Click += (_, _) => _host.Timers.RemoveWater();
        var add = CircleButton("add", 54, (Brush)FindResource("Brush.Primary"), out _, "Add a glass");
        add.Click += (_, _) => _host.Timers.AddWater();
        WaterButtons.Children.Add(remove); WaterButtons.Children.Add(add);
    }

    /// <summary>A pill with minus and plus buttons around a label. Each change is saved and restarts the timers that depend on it.</summary>
    private UIElement Stepper(Func<string> label, Action decrease, Action increase, Action applied)
    {
        var text = new TextBlock { Text = label(), Style = (Style)FindResource("Style.Text"), FontSize = 12, FontWeight = FontWeights.Medium, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Button Side(string icon, Action action)
        {
            var b = new Button
            {
                Style = (Style)FindResource("Style.RoundButton"), Width = 26, Height = 26,
                Content = new TextBlock { Text = Icons.Glyph(icon), Style = (Style)FindResource("Style.Icon"), FontSize = 13 },
            };
            b.Click += (_, _) => { action(); applied(); foreach (var r in _stepperRefreshers) r(); Render(); };
            return b;
        }
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var minus = Side("minus", decrease); var plus = Side("add", increase);
        Grid.SetColumn(text, 1); Grid.SetColumn(plus, 2);
        grid.Children.Add(minus); grid.Children.Add(text); grid.Children.Add(plus);
        _stepperRefreshers.Add(() => text.Text = label());
        return new Border { Child = grid, Background = (Brush)FindResource("Brush.Control"), CornerRadius = new CornerRadius(17), Height = 32, Margin = new Thickness(2), Padding = new Thickness(4, 0, 4, 0) };
    }

    // ---- rendering --------------------------------------------------------------------------

    private void Render()
    {
        var timers = _host.Timers;
        var t = _host.Settings.Timers;
        var p = timers.Pomodoro;
        var cd = timers.Countdown;
        var sw = timers.Stopwatch;
        var water = timers.Water;

        // Mode list
        foreach (var (mode, row, sub) in _rows)
        {
            row.Background = mode == _mode ? (Brush)FindResource("Brush.Control") : Brushes.Transparent;
            sub.Text = mode switch
            {
                Mode.Pomodoro => $"{TimerFormatter.Mmss(p.Remaining)}  {PhaseName(p.Phase)} · {p.CycleIndex} of {t.Cycles}",
                Mode.Countdown => TimerFormatter.Mmss(cd.Remaining),
                Mode.Stopwatch => sw.Format(),
                _ => $"{water.Count} / {water.Goal}",
            };
        }

        PomodoroPanel.Visibility = _mode == Mode.Pomodoro ? Visibility.Visible : Visibility.Collapsed;
        CountdownPanel.Visibility = _mode == Mode.Countdown ? Visibility.Visible : Visibility.Collapsed;
        StopwatchPanel.Visibility = _mode == Mode.Stopwatch ? Visibility.Visible : Visibility.Collapsed;
        WaterPanel.Visibility = _mode == Mode.Water ? Visibility.Visible : Visibility.Collapsed;

        _pomoPlay.Text = Icons.Glyph(p.Running ? "pause" : "play");
        _countdownPlay.Text = Icons.Glyph(cd.Running ? "pause" : "play");
        _stopwatchPlay.Text = Icons.Glyph(sw.Running ? "pause" : "play");

        switch (_mode)
        {
            case Mode.Pomodoro:
                var length = TimeSpan.FromMinutes(p.Phase switch { PomodoroPhase.Focus => t.FocusMinutes, PomodoroPhase.Break => t.BreakMinutes, _ => t.LongBreakMinutes });
                Ring.Value = 100 * (1 - p.Remaining.TotalSeconds / length.TotalSeconds);
                RingTime.FontSize = 44;
                RingTime.Text = TimerFormatter.Mmss(p.Remaining);
                RingLabel.Text = PhaseName(p.Phase);
                PomoTitle.Text = p.Phase == PomodoroPhase.Focus ? $"Focus {p.CycleIndex} of {t.Cycles}" : PhaseName(p.Phase);
                var (nextName, nextMinutes) = p.Phase switch
                {
                    PomodoroPhase.Focus => p.CycleIndex >= t.Cycles ? ("Long break", t.LongBreakMinutes) : ("Break", t.BreakMinutes),
                    _ => ("Focus", t.FocusMinutes),
                };
                PomoNext.Text = $"{nextName} {TimerFormatter.Mmss(TimeSpan.FromMinutes(nextMinutes))} next";
                RenderDots(t.Cycles, p);
                CompletedText.Text = $"{p.CompletedToday} completed today";
                break;
            case Mode.Countdown:
                Ring.Value = 100 * (1 - cd.Remaining.TotalSeconds / TimeSpan.FromMinutes(t.CountdownMinutes).TotalSeconds);
                RingTime.FontSize = 44;
                RingTime.Text = TimerFormatter.Mmss(cd.Remaining);
                RingLabel.Text = cd.Finished ? "Done" : "Countdown";
                break;
            case Mode.Stopwatch:
                Ring.Value = sw.Elapsed.TotalSeconds % 60 / 60 * 100;
                RingTime.FontSize = 28;
                RingTime.Text = sw.Format();
                RingLabel.Text = "Elapsed";
                break;
            default:
                Ring.Value = 100.0 * water.Count / water.Goal;
                RingTime.FontSize = 44;
                RingTime.Text = $"{water.Count}/{water.Goal}";
                RingLabel.Text = "glasses today";
                WaterNote.Text = water.Count >= water.Goal ? "Goal reached" : $"{water.Goal - water.Count} to go";
                break;
        }
    }

    private void RenderDots(int cycles, PomodoroEngine p)
    {
        if (Dots.Children.Count != cycles)
        {
            Dots.Children.Clear();
            for (var i = 0; i < cycles; i++)
                Dots.Children.Add(new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Margin = new Thickness(3, 0, 3, 0) });
        }
        for (var i = 0; i < cycles; i++)
            ((System.Windows.Shapes.Ellipse)Dots.Children[i]).Fill = i < p.CycleIndex ? (Brush)FindResource("Brush.Accent") : (Brush)FindResource("Brush.Control");
    }

    private static string PhaseName(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.Focus => "Focus",
        PomodoroPhase.Break => "Break",
        _ => "Long break",
    };
}
