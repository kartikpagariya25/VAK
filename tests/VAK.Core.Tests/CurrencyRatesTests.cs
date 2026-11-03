using VAK.Core.Units;
using Xunit;

namespace VAK.Core.Tests;

public class CurrencyRatesTests
{
    private const string Json = "{\"amount\":1.0,\"base\":\"USD\",\"date\":\"2026-11-03\",\"rates\":{\"EUR\":0.8,\"INR\":80}}";

    [Fact]
    public void Parse_ReadsRatesAndAddsBase()
    {
        var rates = CurrencyRates.Parse(Json)!;
        Assert.Equal(1, rates["USD"]);
        Assert.Equal(0.8, rates["EUR"]);
        Assert.Equal(80, rates["INR"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"rates\":null}")]
    [InlineData("{}")]
    public void Parse_BadInput_ReturnsNull(string json) => Assert.Null(CurrencyRates.Parse(json));

    [Fact]
    public void BuildCategory_ConvertsThroughBase()
    {
        var cat = CurrencyRates.BuildCategory(CurrencyRates.Parse(Json)!);
        var usd = cat.Units.Single(u => u.Id == "USD");
        var inr = cat.Units.Single(u => u.Id == "INR");
        Assert.Equal(80, UnitConverter.Convert(1, usd, inr), 6);
        Assert.Equal(1, UnitConverter.Convert(80, inr, usd), 6);
    }
}
