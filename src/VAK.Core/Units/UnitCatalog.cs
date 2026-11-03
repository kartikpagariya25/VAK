namespace VAK.Core.Units;

/// <summary>A unit. A value converts to the category's base unit as <c>value * ToBase + Offset</c>.</summary>
public record UnitDef(string Id, string Name, string Symbol, double ToBase, double Offset = 0);

public record UnitCategory(string Id, string Name, string IconKey, IReadOnlyList<UnitDef> Units);

public static class UnitCatalog
{
    public static IReadOnlyList<UnitCategory> All { get; } = new[]
    {
        new UnitCategory("length", "Length", "ruler", new UnitDef[]
        {
            new("mm", "Millimeters", "mm", 0.001), new("cm", "Centimeters", "cm", 0.01), new("m", "Meters", "m", 1),
            new("km", "Kilometers", "km", 1000), new("in", "Inches", "in", 0.0254), new("ft", "Feet", "ft", 0.3048),
            new("yd", "Yards", "yd", 0.9144), new("mi", "Miles", "mi", 1609.344),
        }),
        new UnitCategory("mass", "Mass", "scale", new UnitDef[]
        {
            new("mg", "Milligrams", "mg", 1e-6), new("g", "Grams", "g", 0.001), new("kg", "Kilograms", "kg", 1),
            new("t", "Tonnes", "t", 1000), new("oz", "Ounces", "oz", 0.028349523125), new("lb", "Pounds", "lb", 0.45359237),
            new("st", "Stone", "st", 6.35029318),
        }),
        new UnitCategory("temperature", "Temperature", "temperature", new UnitDef[]
        {
            new("C", "Celsius", "°C", 1), new("F", "Fahrenheit", "°F", 5.0 / 9, -160.0 / 9), new("K", "Kelvin", "K", 1, -273.15),
        }),
        new UnitCategory("data", "Data", "database", new UnitDef[]
        {
            new("bit", "Bits", "bit", 0.125), new("B", "Bytes", "B", 1), new("KB", "Kilobytes", "KB", 1024),
            new("MB", "Megabytes", "MB", 1024.0 * 1024), new("GB", "Gigabytes", "GB", 1024.0 * 1024 * 1024),
            new("TB", "Terabytes", "TB", 1024.0 * 1024 * 1024 * 1024),
        }),
        new UnitCategory("volume", "Volume", "drop", new UnitDef[]
        {
            new("ml", "Milliliters", "ml", 0.001), new("l", "Liters", "l", 1), new("m3", "Cubic meters", "m³", 1000),
            new("tsp", "Teaspoons", "tsp", 0.00492892159375), new("tbsp", "Tablespoons", "tbsp", 0.01478676478125),
            new("floz", "Fluid ounces", "fl oz", 0.0295735295625), new("cup", "Cups", "cup", 0.2365882365),
            new("pt", "Pints", "pt", 0.473176473), new("qt", "Quarts", "qt", 0.946352946), new("gal", "Gallons", "gal", 3.785411784),
        }),
        new UnitCategory("speed", "Speed", "speed", new UnitDef[]
        {
            new("ms", "Meters/second", "m/s", 1), new("kmh", "Kilometers/hour", "km/h", 1 / 3.6),
            new("mph", "Miles/hour", "mph", 0.44704), new("kn", "Knots", "kn", 0.514444), new("fts", "Feet/second", "ft/s", 0.3048),
        }),
        new UnitCategory("time", "Time", "clock", new UnitDef[]
        {
            new("ms", "Milliseconds", "ms", 0.001), new("s", "Seconds", "s", 1), new("min", "Minutes", "min", 60),
            new("h", "Hours", "h", 3600), new("d", "Days", "d", 86400), new("wk", "Weeks", "wk", 604800),
        }),
    };

    public static UnitCategory? Find(string id) => All.FirstOrDefault(c => c.Id == id);
}
