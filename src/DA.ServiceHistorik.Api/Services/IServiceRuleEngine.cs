using DA.ServiceHistorik.Api.Models;

namespace DA.ServiceHistorik.Api.Services;

public interface IServiceRuleEngine
{
    ServiceRule GetRule(string make, string model);
    DateTime CalculateNextServiceDate(DateTime lastServiceDate, ServiceRule rule);
    int DaysUntilService(DateTime nextServiceDate);
}
