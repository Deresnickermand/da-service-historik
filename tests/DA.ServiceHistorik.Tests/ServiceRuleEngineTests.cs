using DA.ServiceHistorik.Api.Models;
using DA.ServiceHistorik.Api.Services;
using Microsoft.Extensions.Configuration;

namespace DA.ServiceHistorik.Tests;

public class ServiceRuleEngineTests
{
    private static IConfiguration BuildConfig(string json)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();
    }

    private const string RulesJson = """
    {
      "rules": [
        { "make": "Mercedes-Benz", "model": "Vito 639", "intervalMonths": 6, "intervalKm": 5000 },
        { "make": "Mercedes-Benz", "intervalMonths": 12, "intervalKm": 8000 },
        { "make": "DEFAULT", "intervalMonths": 6, "intervalKm": 8000 }
      ]
    }
    """;

    [Fact]
    public void GetRule_ModelSpecificMatch_ReturnsMostSpecificRule()
    {
        var engine = new ServiceRuleEngine(BuildConfig(RulesJson));
        var rule = engine.GetRule("Mercedes-Benz", "Vito 639");
        Assert.Equal(6, rule.IntervalMonths);
        Assert.Equal(5000, rule.IntervalKm);
    }

    [Fact]
    public void GetRule_MakeOnlyMatch_ReturnsMakeRule()
    {
        var engine = new ServiceRuleEngine(BuildConfig(RulesJson));
        var rule = engine.GetRule("Mercedes-Benz", "C300");
        Assert.Equal(12, rule.IntervalMonths);
        Assert.Equal(8000, rule.IntervalKm);
    }

    [Fact]
    public void GetRule_NoMatch_ReturnsDefault()
    {
        var engine = new ServiceRuleEngine(BuildConfig(RulesJson));
        var rule = engine.GetRule("Ukendt Mærke", "Model X");
        Assert.Equal(6, rule.IntervalMonths);
        Assert.Equal(8000, rule.IntervalKm);
    }

    [Fact]
    public void GetRule_CaseInsensitiveMatch()
    {
        var engine = new ServiceRuleEngine(BuildConfig(RulesJson));
        var rule = engine.GetRule("mercedes-benz", "c300");
        Assert.Equal(12, rule.IntervalMonths);
    }

    [Fact]
    public void CalculateNextServiceDate_AddsIntervalMonths()
    {
        var engine = new ServiceRuleEngine(BuildConfig(RulesJson));
        var rule = new ServiceRule { IntervalMonths = 6, IntervalKm = 8000 };
        var lastService = new DateTime(2025, 10, 1);
        var next = engine.CalculateNextServiceDate(lastService, rule);
        Assert.Equal(new DateTime(2026, 4, 1), next);
    }

    [Fact]
    public void DaysUntilService_ReturnsCorrectDays()
    {
        var engine = new ServiceRuleEngine(BuildConfig(RulesJson));
        var nextService = DateTime.Today.AddDays(30);
        var days = engine.DaysUntilService(nextService);
        Assert.Equal(30, days);
    }
}
