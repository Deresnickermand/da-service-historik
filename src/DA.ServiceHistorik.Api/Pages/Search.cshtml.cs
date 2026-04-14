using DA.ServiceHistorik.Api.Models;
using DA.ServiceHistorik.Api.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DA.ServiceHistorik.Api.Pages;

public class SearchModel(IDeresAutoApiClient apiClient, IServiceRuleEngine ruleEngine) : PageModel
{
    public string? Plate { get; private set; }
    public List<SearchResultRow> Records { get; private set; } = [];

    public async Task OnGetAsync(string? plate)
    {
        Plate = plate?.ToUpper().Trim();
        if (string.IsNullOrWhiteSpace(Plate)) return;

        var all = await apiClient.GetServiceRecordsAsync();
        var matching = all.Where(r =>
            r.LicensePlate.Equals(Plate, StringComparison.OrdinalIgnoreCase)).ToList();

        Records = matching.Select(r =>
        {
            var rule = ruleEngine.GetRule(r.Make, r.Model);
            var next = ruleEngine.CalculateNextServiceDate(r.ServiceDate, rule);
            return new SearchResultRow(r, next, ruleEngine.DaysUntilService(next));
        }).OrderByDescending(r => r.ServiceDate).ToList();
    }
}

public record SearchResultRow(ServiceRecord Record, DateTime NextServiceDate, int DaysUntilService)
{
    public DateTime ServiceDate => Record.ServiceDate;
    public string ServiceType => Record.ServiceType;
    public string Make => Record.Make;
    public string Model => Record.Model;
    public int KmAtService => Record.KmAtService;
    public string LicensePlate => Record.LicensePlate;
}
