using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DA.ServiceHistorik.Api.Services;

public class ReimundSmsService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<ReimundSmsService> logger) : IReimundSmsService
{
    public async Task<bool> SendAsync(string toPhoneNumber, string message, CancellationToken ct = default)
    {
        try
        {
            // TODO: Tilpas body-format til Reimunds API-spec
            var payload = new
            {
                to = toPhoneNumber,
                message = message,
                from = "DeresAuto"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            // TODO: Tilpas endpoint-sti
            var response = await httpClient.PostAsync("/sms/send", content, ct);
            response.EnsureSuccessStatusCode();

            logger.LogInformation("SMS sendt til {Phone}", toPhoneNumber);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fejl ved afsendelse af SMS til {Phone}", toPhoneNumber);
            return false;
        }
    }
}
