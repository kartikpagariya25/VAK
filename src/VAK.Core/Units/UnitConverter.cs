using System.Globalization;

namespace VAK.Core.Units;

public static class UnitConverter
{
    public static double Convert(double value, UnitDef from, UnitDef to)
    {
        var baseValue = value * from.ToBase + from.Offset;
        return (baseValue - to.Offset) / to.ToBase;
    }

    /// <summary>Rounds to six decimals and drops trailing zeros: 3.280839895 becomes "3.28084".</summary>
    public static string Format(double value)
    {
        var rounded = Math.Round(value, 6);
        if (rounded == 0) return "0"; // also avoids "-0"
        return rounded.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
