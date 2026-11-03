namespace VAK.Core.Sys;

public class SparklineBuffer
{
    private readonly int _capacity;
    private readonly List<double> _values = new();

    public SparklineBuffer(int capacity) => _capacity = capacity;

    public IReadOnlyList<double> Values => _values;

    public void Add(double v)
    {
        _values.Add(v);
        if (_values.Count > _capacity) _values.RemoveAt(0);
    }
}

public static class RateCalculator
{
    public static double BytesPerSecond(long previous, long current, TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero || current < previous) return 0;
        return (current - previous) / elapsed.TotalSeconds;
    }
}

public static class BatteryMath
{
    public static int? HealthPercent(int? fullChargeMwh, int? designMwh)
    {
        if (fullChargeMwh is null or <= 0 || designMwh is null or <= 0) return null;
        var pct = (int)Math.Round(fullChargeMwh.Value * 100.0 / designMwh.Value);
        return Math.Clamp(pct, 0, 100);
    }
}
