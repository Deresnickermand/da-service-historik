using DA.ServiceHistorik.Api.Models;
using Microsoft.Extensions.Configuration;

namespace DA.ServiceHistorik.Api.Services;

public class ServiceRuleEngine : IServiceRuleEngine
{
    private readonly List<ServiceRule> _rules;

    public ServiceRuleEngine(IConfiguration configuration)
    {
        _rules = configuration.GetSection("rules").Get<List<ServiceRule>>() ?? [];
    }

    public ServiceRule GetRule(string make, string model)
    {
        // 1. Exact make + model match
        var exact = _rules.FirstOrDefault(r =>
            r.Model != null &&
            r.Make.Equals(make, StringComparison.OrdinalIgnoreCase) &&
            r.Model.Equals(model, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // 2. Make-only match (no model on rule)
        var makeOnly = _rules.FirstOrDefault(r =>
            r.Model == null &&
            r.Make.Equals(make, StringComparison.OrdinalIgnoreCase));
        if (makeOnly != null) return makeOnly;

        // 3. DEFAULT fallback
        return _rules.First(r => r.Make.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase));
    }

    public DateTime CalculateNextServiceDate(DateTime lastServiceDate, ServiceRule rule)
        => lastServiceDate.AddMonths(rule.IntervalMonths);

    public int DaysUntilService(DateTime nextServiceDate)
        => (nextServiceDate.Date - DateTime.Today).Days;
}
