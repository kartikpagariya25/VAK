using System.Text.Json;

namespace VAK.Core.Units;

public static class CurrencyRates
{
    /// <summary>Parses a Frankfurter-style response ({"base":"USD","rates":{"EUR":0.9}}). Returns null for anything else.
    /// The base currency is added with rate 1.</summary>
    public static Dictionary<string, double>? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("rates", out var rates) || rates.ValueKind != JsonValueKind.Object) return null;
            var baseCode = doc.RootElement.TryGetProperty("base", out var b) && b.GetString() is { Length: > 0 } s ? s : "USD";
            var result = new Dictionary<string, double> { [baseCode] = 1 };
            foreach (var p in rates.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Number && p.Value.GetDouble() > 0) result[p.Name] = p.Value.GetDouble();
            return result;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Builds a Currency category. Rates are per one unit of the base currency, so ToBase is 1 / rate.</summary>
    public static UnitCategory BuildCategory(Dictionary<string, double> rates) =>
        new("currency", "Currency", "globe",
            rates.OrderBy(r => r.Key).Select(r => new UnitDef(r.Key, r.Key, r.Key, 1 / r.Value)).ToList());
}
