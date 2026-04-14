namespace DA.ServiceHistorik.Api.Services;

public interface IResendEmailService
{
    Task<bool> SendReminderAsync(
        string toEmail,
        string toName,
        string licensePlate,
        string make,
        int daysUntilService,
        CancellationToken ct = default);

    Task<bool> SendMissingContactNotificationAsync(
        string licensePlate,
        string make,
        int daysUntilService,
        bool missingPhone,
        bool missingEmail,
        CancellationToken ct = default);
}
