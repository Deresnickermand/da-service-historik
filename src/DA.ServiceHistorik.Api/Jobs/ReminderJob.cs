using DA.ServiceHistorik.Api.Data;
using DA.ServiceHistorik.Api.Models;
using DA.ServiceHistorik.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DA.ServiceHistorik.Api.Jobs;

public class ReminderJob(
    IDeresAutoApiClient apiClient,
    IServiceRuleEngine ruleEngine,
    IReimundSmsService smsService,
    IResendEmailService emailService,
    AppDbContext db,
    ILogger<ReminderJob> logger)
{
    private static readonly int[] ReminderDays = [30, 14];

    public async Task RunAsync(CancellationToken ct = default)
    {
        logger.LogInformation("ReminderJob starter — {Time}", DateTime.Now);

        List<ServiceRecord> records;
        try
        {
            records = await apiClient.GetServiceRecordsAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kunne ikke hente service-records fra API");
            throw;
        }

        int sent = 0, skipped = 0;

        foreach (var record in records)
        {
            var rule = ruleEngine.GetRule(record.Make, record.Model);
            var nextServiceDate = ruleEngine.CalculateNextServiceDate(record.ServiceDate, rule);
            var daysUntil = ruleEngine.DaysUntilService(nextServiceDate);

            if (!ReminderDays.Contains(daysUntil))
            {
                skipped++;
                continue;
            }

            var reminderType = daysUntil == 30 ? ReminderType.ThirtyDay : ReminderType.FourteenDay;

            var alreadySent = await db.SentReminders.AnyAsync(r =>
                r.LicensePlate == record.LicensePlate &&
                r.ReminderType == reminderType &&
                r.ServiceDate.Date == nextServiceDate.Date, ct);

            if (alreadySent)
            {
                skipped++;
                continue;
            }

            var hasMissingContact = string.IsNullOrWhiteSpace(record.PhoneNumber) ||
                                    string.IsNullOrWhiteSpace(record.Email);

            if (hasMissingContact)
            {
                await emailService.SendMissingContactNotificationAsync(
                    record.LicensePlate, record.Make, daysUntil,
                    string.IsNullOrWhiteSpace(record.PhoneNumber),
                    string.IsNullOrWhiteSpace(record.Email), ct);
            }
            else
            {
                var smsText = $"Hej! Din {record.Make} ({record.LicensePlate}) skal til service om {daysUntil} dage. Ring til os på +299 31 48 00. Mvh Deres Auto, Grønland";

                await smsService.SendAsync(record.PhoneNumber!, smsText, ct);
                await emailService.SendReminderAsync(
                    record.Email!, record.LicensePlate, record.LicensePlate,
                    record.Make, daysUntil, ct);
            }

            db.SentReminders.Add(new SentReminder
            {
                LicensePlate = record.LicensePlate,
                ReminderType = reminderType,
                ServiceDate = nextServiceDate,
                SentAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            sent++;
        }

        logger.LogInformation("ReminderJob færdig — {Sent} sendt, {Skipped} skippet", sent, skipped);
    }
}
