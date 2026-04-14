using DA.ServiceHistorik.Api.Models;

namespace DA.ServiceHistorik.Api.Services;

public interface IDeresAutoApiClient
{
    Task<List<ServiceRecord>> GetServiceRecordsAsync(CancellationToken ct = default);
}
