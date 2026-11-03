using System.Windows.Threading;
using VAK.Core.Timers;

namespace VAK.App.Services;

/// <summary>Owns the Pomodoro, countdown, stopwatch and water counters for the whole app, so they keep running when no page is open.
/// Time is measured with a wall clock, so a sleep/resume arrives as one large tick (which the engines handle).</summary>
public sealed class TimersService
{
    private readonly AppHost _host;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private TimeSpan _last;

    public PomodoroEngine Pomodoro { get; private set; }
    public CountdownTimer Countdown { get; private set; }
    public StopwatchTimer Stopwatch { get; } = new();
    public WaterCounter Water { get; private set; }

    /// <summary>Raised on every tick while anything is running, and after any change.</summary>
    public event Action? Changed;

    /// <summary>Raised when a phase or countdown finishes; the shell shows it as a notice and plays a sound.</summary>
    public event Action<string>? Notice;

    public TimersService(AppHost host)
    {
        _host = host;
        var s = host.Settings;
        EnsureToday();
        Pomodoro = NewPomodoro();
        Pomodoro.CompletedToday = s.PomodoroCompletedToday;
        Countdown = new CountdownTimer(TimeSpan.FromMinutes(s.Timers.CountdownMinutes));
        Water = new WaterCounter(8, s.WaterToday);

        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private PomodoroEngine NewPomodoro()
    {
        var engine = new PomodoroEngine(_host.Settings.Timers);
        engine.PhaseChanged += OnPhaseChanged;
        return engine;
    }

    private void OnPhaseChanged(PomodoroPhase phase)
    {
        _host.Settings.PomodoroCompletedToday = Pomodoro.CompletedToday;
        _host.Save();
        var t = _host.Settings.Timers;
        Notice?.Invoke(phase switch
        {
            PomodoroPhase.Break => $"Focus complete. Take a {t.BreakMinutes} minute break.",
            PomodoroPhase.LongBreak => $"Great work. Take a {t.LongBreakMinutes} minute break.",
            _ => "Break over. Ready to focus?",
        });
    }

    private void Tick()
    {
        var now = _clock.Elapsed;
        var elapsed = now - _last;
        _last = now;
        EnsureToday();

        var running = Pomodoro.Running || Countdown.Running || Stopwatch.Running;
        if (!running) return;

        if (Pomodoro.Running) Pomodoro.Tick(elapsed);
        if (Countdown.Running)
        {
            Countdown.Tick(elapsed);
            if (Countdown.Finished) Notice?.Invoke("Countdown finished.");
        }
        Stopwatch.Tick(elapsed);
        Changed?.Invoke();
    }

    /// <summary>Daily counters (water, completed focus sessions) start from zero on a new day.</summary>
    private void EnsureToday()
    {
        var s = _host.Settings;
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (s.CountersDate == today) return;
        s.CountersDate = today;
        s.WaterToday = 0;
        s.PomodoroCompletedToday = 0;
        if (Water is not null) Water = new WaterCounter(8, 0);
        if (Pomodoro is not null) Pomodoro.CompletedToday = 0;
    }

    /// <summary>Call after changing <see cref="Core.Settings.TimerSettings"/>: saves, restarts the Pomodoro and rebuilds the countdown.</summary>
    public void SettingsChanged()
    {
        _host.Save();
        var completed = Pomodoro.CompletedToday;
        Pomodoro.PhaseChanged -= OnPhaseChanged;
        Pomodoro = NewPomodoro();
        Pomodoro.CompletedToday = completed;
        Countdown = new CountdownTimer(TimeSpan.FromMinutes(_host.Settings.Timers.CountdownMinutes));
        Changed?.Invoke();
    }

    public void AddWater() { Water.Add(); SaveWater(); }
    public void RemoveWater() { Water.Remove(); SaveWater(); }

    private void SaveWater()
    {
        _host.Settings.WaterToday = Water.Count;
        _host.Save();
        Changed?.Invoke();
    }

    /// <summary>Lets a view force a refresh after it starts, pauses or resets an engine directly.</summary>
    public void Touch() => Changed?.Invoke();
}
