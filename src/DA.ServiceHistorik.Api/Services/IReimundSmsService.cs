namespace DA.ServiceHistorik.Api.Services;

public interface IReimundSmsService
{
    Task<bool> SendAsync(string toPhoneNumber, string message, CancellationToken ct = default);
}
