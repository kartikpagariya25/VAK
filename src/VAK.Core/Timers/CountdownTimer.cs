namespace VAK.Core.Timers;

public class CountdownTimer
{
    private readonly TimeSpan _initial;

    public CountdownTimer(TimeSpan initial)
    {
        _initial = initial;
        Remaining = initial;
    }

    public TimeSpan Remaining { get; private set; }
    public bool Running { get; private set; }
    public bool Finished { get; private set; }

    public void Start()
    {
        if (Finished) return;
        Running = true;
    }

    public void Pause() => Running = false;

    public void Reset()
    {
        Running = false;
        Finished = false;
        Remaining = _initial;
    }

    public void Tick(TimeSpan elapsed)
    {
        if (!Running) return;
        Remaining -= elapsed;
        if (Remaining <= TimeSpan.Zero)
        {
            Remaining = TimeSpan.Zero;
            Running = false;
            Finished = true;
        }
    }
}
