namespace VAK.Core.Timers;

public class StopwatchTimer
{
    public TimeSpan Elapsed { get; private set; }
    public bool Running { get; private set; }

    public void Start() => Running = true;
    public void Pause() => Running = false;

    public void Reset()
    {
        Running = false;
        Elapsed = TimeSpan.Zero;
    }

    public void Tick(TimeSpan elapsed)
    {
        if (Running) Elapsed += elapsed;
    }

    public string Format() =>
        $"{(int)Elapsed.TotalHours:00}:{Elapsed.Minutes:00}:{Elapsed.Seconds:00}.{Elapsed.Milliseconds / 10:00}";
}
