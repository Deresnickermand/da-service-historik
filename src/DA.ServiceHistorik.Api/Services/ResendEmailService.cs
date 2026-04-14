using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Resend;

namespace DA.ServiceHistorik.Api.Services;

public class ResendEmailService(
    IResend resend,
    IConfiguration configuration,
    ILogger<ResendEmailService> logger) : IResendEmailService
{
    private string FromEmail => configuration["Notifications:FromEmail"] ?? "service@deresauto.gl";
    private string FromName => configuration["Notifications:FromName"] ?? "Deres Auto";
    private string FallbackEmail => configuration["Notifications:FallbackEmail"] ?? "info@deresauto.gl";

    public async Task<bool> SendReminderAsync(
        string toEmail,
        string toName,
        string licensePlate,
        string make,
        int daysUntilService,
        CancellationToken ct = default)
    {
        try
        {
            var message = new EmailMessage
            {
                From = $"{FromName} <{FromEmail}>",
                To = [toEmail],
                Subject = $"Din {make} skal til service om {daysUntilService} dage",
                HtmlBody = $"""
                    <p>Hej {toName},</p>
                    <p>Din <strong>{make}</strong> med nummerplade <strong>{licensePlate}</strong>
                    skal til service om <strong>{daysUntilService} dage</strong>.</p>
                    <p>Ring til os på <a href="tel:+299314800">+299 31 48 00</a> for at booke tid.</p>
                    <br/>
                    <p>Venlig hilsen<br/>{FromName}</p>
                    """
            };

            await resend.EmailSendAsync(message, ct);
            logger.LogInformation("Email sendt til {Email} for {Plate}", toEmail, licensePlate);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fejl ved afsendelse af email til {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendMissingContactNotificationAsync(
        string licensePlate,
        string make,
        int daysUntilService,
        bool missingPhone,
        bool missingEmail,
        CancellationToken ct = default)
    {
        try
        {
            var missing = new List<string>();
            if (missingPhone) missing.Add("telefonnummer");
            if (missingEmail) missing.Add("email");

            var message = new EmailMessage
            {
                From = $"{FromName} <{FromEmail}>",
                To = [FallbackEmail],
                Subject = $"Manglende kontaktinfo — {licensePlate}",
                HtmlBody = $"""
                    <p>Bilen <strong>{licensePlate}</strong> ({make}) skal til service om
                    <strong>{daysUntilService} dage</strong>, men mangler:
                    <strong>{string.Join(" og ", missing)}</strong>.</p>
                    <p>Opdater venligst i systemet.</p>
                    """
            };

            await resend.EmailSendAsync(message, ct);
            logger.LogInformation("Manglende kontaktinfo sendt til {FallbackEmail} for {Plate}",
                FallbackEmail, licensePlate);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fejl ved afsendelse af manglende-kontakt notifikation for {Plate}", licensePlate);
            return false;
        }
    }
}
