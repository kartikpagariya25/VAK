using VAK.Core.Settings;

namespace VAK.Core.Timers;

public enum PomodoroPhase { Focus, Break, LongBreak }

public class PomodoroEngine
{
    private readonly TimerSettings _cfg;

    public PomodoroEngine(TimerSettings cfg)
    {
        _cfg = cfg;
        Remaining = PhaseLength(PomodoroPhase.Focus);
    }

    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Focus;
    public bool Running { get; private set; }
    public TimeSpan Remaining { get; private set; }
    public int CycleIndex { get; private set; } = 1;
    public int CompletedToday { get; set; }

    public event Action<PomodoroPhase>? PhaseChanged;

    public void Start() => Running = true;
    public void Pause() => Running = false;

    public void Reset()
    {
        Running = false;
        Phase = PomodoroPhase.Focus;
        CycleIndex = 1;
        Remaining = PhaseLength(Phase);
    }

    public void Skip() => Advance(countCompleted: false);

    public void Tick(TimeSpan elapsed)
    {
        if (!Running) return;
        if (elapsed < Remaining) { Remaining -= elapsed; return; }
        Advance(countCompleted: true); // at most one phase per tick, leftover time is dropped
    }

    private void Advance(bool countCompleted)
    {
        if (Phase == PomodoroPhase.Focus)
        {
            if (countCompleted) CompletedToday++;
            Phase = CycleIndex >= _cfg.Cycles ? PomodoroPhase.LongBreak : PomodoroPhase.Break;
        }
        else
        {
            CycleIndex = Phase == PomodoroPhase.LongBreak ? 1 : CycleIndex + 1;
            Phase = PomodoroPhase.Focus;
        }
        Remaining = PhaseLength(Phase);
        if (!_cfg.AutoStartNext) Running = false;
        PhaseChanged?.Invoke(Phase);
    }

    private TimeSpan PhaseLength(PomodoroPhase p) => TimeSpan.FromMinutes(p switch
    {
        PomodoroPhase.Focus => _cfg.FocusMinutes,
        PomodoroPhase.Break => _cfg.BreakMinutes,
        _ => _cfg.LongBreakMinutes,
    });
}
