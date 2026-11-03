namespace VAK.Core.Timers;

public class WaterCounter
{
    public WaterCounter(int goal = 8, int count = 0)
    {
        Goal = goal;
        Count = Math.Max(0, count);
    }

    public int Count { get; private set; }
    public int Goal { get; }

    public void Add() => Count++;
    public void Remove() => Count = Math.Max(0, Count - 1);
}
