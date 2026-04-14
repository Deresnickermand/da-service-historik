using System.Text.Json;
using DA.ServiceHistorik.Api.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DA.ServiceHistorik.Api.Services;

public class DeresAutoApiClient(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<DeresAutoApiClient> logger) : IDeresAutoApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<ServiceRecord>> GetServiceRecordsAsync(CancellationToken ct = default)
    {
        // TODO: Tilpas endpoint-sti til det faktiske API
        var endpoint = "/api/servicehistorik";
        var apiKey = configuration["DeresAutoApi:ApiKey"];

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.Add("X-Api-Key", apiKey);

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<ServiceRecord>>(json, JsonOptions) ?? [];
    }
}
