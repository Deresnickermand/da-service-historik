using DA.ServiceHistorik.Api.Data;
using DA.ServiceHistorik.Api.Jobs;
using DA.ServiceHistorik.Api.Models;
using DA.ServiceHistorik.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DA.ServiceHistorik.Tests;

public class ReminderJobTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["rules:0:make"] = "Toyota",
                ["rules:0:intervalMonths"] = "6",
                ["rules:0:intervalKm"] = "7500",
                ["rules:1:make"] = "DEFAULT",
                ["rules:1:intervalMonths"] = "6",
                ["rules:1:intervalKm"] = "8000"
            })
            .Build();

    [Fact]
    public async Task RunAsync_SendsReminderWhen30DaysAway()
    {
        var db = CreateInMemoryDb();
        var apiClient = new Mock<IDeresAutoApiClient>();
        var ruleEngine = new ServiceRuleEngine(CreateConfig());
        var sms = new Mock<IReimundSmsService>();
        var email = new Mock<IResendEmailService>();
        var logger = new Mock<ILogger<ReminderJob>>().Object;

        var nextServiceDate = DateTime.Today.AddDays(30);
        var lastServiceDate = nextServiceDate.AddMonths(-6);

        apiClient.Setup(x => x.GetServiceRecordsAsync(default))
            .ReturnsAsync([new ServiceRecord
            {
                LicensePlate = "GJ12345",
                Make = "Toyota",
                Model = "RAV4 PHEV",
                ServiceDate = lastServiceDate,
                KmAtService = 40000,
                PhoneNumber = "+299123456",
                Email = "kunde@example.com"
            }]);

        sms.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(true);
        email.Setup(x => x.SendReminderAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<int>(), default))
            .ReturnsAsync(true);

        var job = new ReminderJob(apiClient.Object, ruleEngine, sms.Object, email.Object, db, logger);
        await job.RunAsync();

        sms.Verify(x => x.SendAsync("+299123456", It.IsAny<string>(), default), Times.Once);
        email.Verify(x => x.SendReminderAsync(
            "kunde@example.com", It.IsAny<string>(), "GJ12345",
            "Toyota", 30, default), Times.Once);

        Assert.Single(db.SentReminders.ToList());
    }

    [Fact]
    public async Task RunAsync_DoesNotSendDuplicateReminder()
    {
        var db = CreateInMemoryDb();
        var nextServiceDate = DateTime.Today.AddDays(30);
        var lastServiceDate = nextServiceDate.AddMonths(-6);

        db.SentReminders.Add(new SentReminder
        {
            LicensePlate = "GJ12345",
            ReminderType = ReminderType.ThirtyDay,
            ServiceDate = nextServiceDate,
            SentAt = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var apiClient = new Mock<IDeresAutoApiClient>();
        var ruleEngine = new ServiceRuleEngine(CreateConfig());
        var sms = new Mock<IReimundSmsService>();
        var email = new Mock<IResendEmailService>();
        var logger = new Mock<ILogger<ReminderJob>>().Object;

        apiClient.Setup(x => x.GetServiceRecordsAsync(default))
            .ReturnsAsync([new ServiceRecord
            {
                LicensePlate = "GJ12345",
                Make = "Toyota",
                Model = "RAV4 PHEV",
                ServiceDate = lastServiceDate,
                KmAtService = 40000,
                PhoneNumber = "+299123456",
                Email = "kunde@example.com"
            }]);

        var job = new ReminderJob(apiClient.Object, ruleEngine, sms.Object, email.Object, db, logger);
        await job.RunAsync();

        sms.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task RunAsync_SendsMissingContactNotificationWhenNoPhone()
    {
        var db = CreateInMemoryDb();
        var nextServiceDate = DateTime.Today.AddDays(30);
        var lastServiceDate = nextServiceDate.AddMonths(-6);

        var apiClient = new Mock<IDeresAutoApiClient>();
        var ruleEngine = new ServiceRuleEngine(CreateConfig());
        var sms = new Mock<IReimundSmsService>();
        var email = new Mock<IResendEmailService>();
        var logger = new Mock<ILogger<ReminderJob>>().Object;

        apiClient.Setup(x => x.GetServiceRecordsAsync(default))
            .ReturnsAsync([new ServiceRecord
            {
                LicensePlate = "GJ99999",
                Make = "Toyota",
                Model = "RAV4 PHEV",
                ServiceDate = lastServiceDate,
                KmAtService = 40000,
                PhoneNumber = null,
                Email = null
            }]);

        email.Setup(x => x.SendMissingContactNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<bool>(), It.IsAny<bool>(), default))
            .ReturnsAsync(true);

        var job = new ReminderJob(apiClient.Object, ruleEngine, sms.Object, email.Object, db, logger);
        await job.RunAsync();

        email.Verify(x => x.SendMissingContactNotificationAsync(
            "GJ99999", "Toyota", 30, true, true, default), Times.Once);
    }
}
