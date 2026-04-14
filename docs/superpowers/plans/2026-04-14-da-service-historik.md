# DA Service Historik Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a .NET 8 Web API that daily at 16:00 fetches car service records from Deres Auto API, calculates due dates via servicerules.json, and sends SMS (Reimund) + email (Resend) reminders 30 and 14 days before service — with a Razor Pages dashboard for searching service history.

**Architecture:** Hangfire scheduled job runs daily at 16:00. It calls Deres Auto API, applies service rules from servicerules.json (make/model → interval months + km), checks Azure SQL to avoid duplicate reminders, and sends notifications. Razor Pages `/search` and `/reminders` serve the dashboard. Azure App Service hosts everything; GitHub Actions deploys on push to `main`.

**Tech Stack:** .NET 8, ASP.NET Core (Razor Pages + minimal API), Hangfire.AspNetCore + Hangfire.SqlServer, Entity Framework Core 8 (Azure SQL), xUnit + Moq (tests), Resend NuGet SDK, HttpClient (Reimund + Deres Auto API)

---

## File Map

```
da-service-historik/
├── DA.ServiceHistorik.sln
├── servicerules.json                               (already exists)
├── src/
│   └── DA.ServiceHistorik.Api/
│       ├── DA.ServiceHistorik.Api.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── Models/
│       │   ├── ServiceRecord.cs                   DTO from Deres Auto API
│       │   ├── ServiceRule.cs                     Rule from servicerules.json
│       │   ├── SentReminder.cs                    EF entity (Azure SQL)
│       │   └── ReminderType.cs                    Enum: ThirtyDay / FourteenDay
│       ├── Data/
│       │   └── AppDbContext.cs                    EF Core DbContext
│       ├── Services/
│       │   ├── IDeresAutoApiClient.cs
│       │   ├── DeresAutoApiClient.cs              HTTP client for their API
│       │   ├── IServiceRuleEngine.cs
│       │   ├── ServiceRuleEngine.cs               Rule matching + date calculation
│       │   ├── IReimundSmsService.cs
│       │   ├── ReimundSmsService.cs               Send SMS via Reimund
│       │   ├── IResendEmailService.cs
│       │   └── ResendEmailService.cs              Send email via Resend SDK
│       ├── Jobs/
│       │   └── ReminderJob.cs                     Hangfire job, daily orchestration
│       └── Pages/
│           ├── _Layout.cshtml
│           ├── Search.cshtml
│           ├── Search.cshtml.cs
│           ├── Reminders.cshtml
│           └── Reminders.cshtml.cs
└── tests/
    └── DA.ServiceHistorik.Tests/
        ├── DA.ServiceHistorik.Tests.csproj
        ├── ServiceRuleEngineTests.cs
        ├── ReminderJobTests.cs
        └── DeresAutoApiClientTests.cs
```

---

## Task 1: Project Scaffold

**Files:**
- Create: `DA.ServiceHistorik.sln`
- Create: `src/DA.ServiceHistorik.Api/DA.ServiceHistorik.Api.csproj`
- Create: `tests/DA.ServiceHistorik.Tests/DA.ServiceHistorik.Tests.csproj`

- [ ] **Step 1: Scaffold solution og projekter**

```bash
cd /Users/deresnickermand/da-service-historik
dotnet new sln -n DA.ServiceHistorik
dotnet new webapi -n DA.ServiceHistorik.Api -o src/DA.ServiceHistorik.Api --no-openapi
dotnet new xunit -n DA.ServiceHistorik.Tests -o tests/DA.ServiceHistorik.Tests
dotnet sln add src/DA.ServiceHistorik.Api/DA.ServiceHistorik.Api.csproj
dotnet sln add tests/DA.ServiceHistorik.Tests/DA.ServiceHistorik.Tests.csproj
dotnet add tests/DA.ServiceHistorik.Tests/DA.ServiceHistorik.Tests.csproj reference src/DA.ServiceHistorik.Api/DA.ServiceHistorik.Api.csproj
```

- [ ] **Step 2: Tilføj NuGet-pakker til API**

```bash
cd src/DA.ServiceHistorik.Api
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Resend
```

- [ ] **Step 3: Tilføj NuGet-pakker til tests**

```bash
cd ../../tests/DA.ServiceHistorik.Tests
dotnet add package Moq
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

- [ ] **Step 4: Verificér at alt bygger**

```bash
cd /Users/deresnickermand/da-service-historik
dotnet build
```
Forventet output: `Build succeeded.`

- [ ] **Step 5: Ryd op i scaffolded boilerplate**

Slet `src/DA.ServiceHistorik.Api/WeatherForecast.cs` og `Controllers/WeatherForecastController.cs` hvis de eksisterer.

```bash
rm -f src/DA.ServiceHistorik.Api/WeatherForecast.cs
rm -f src/DA.ServiceHistorik.Api/Controllers/WeatherForecastController.cs
```

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "feat: scaffold .NET 8 solution med API og test-projekt"
```

---

## Task 2: Models

**Files:**
- Create: `src/DA.ServiceHistorik.Api/Models/ServiceRecord.cs`
- Create: `src/DA.ServiceHistorik.Api/Models/ServiceRule.cs`
- Create: `src/DA.ServiceHistorik.Api/Models/SentReminder.cs`
- Create: `src/DA.ServiceHistorik.Api/Models/ReminderType.cs`

- [ ] **Step 1: Opret ReminderType enum**

`src/DA.ServiceHistorik.Api/Models/ReminderType.cs`:
```csharp
namespace DA.ServiceHistorik.Api.Models;

public enum ReminderType
{
    ThirtyDay,
    FourteenDay
}
```

- [ ] **Step 2: Opret ServiceRecord (DTO fra Deres Auto API)**

`src/DA.ServiceHistorik.Api/Models/ServiceRecord.cs`:
```csharp
namespace DA.ServiceHistorik.Api.Models;

public class ServiceRecord
{
    public string LicensePlate { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public DateTime ServiceDate { get; set; }
    public int KmAtService { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
}
```

- [ ] **Step 3: Opret ServiceRule (fra servicerules.json)**

`src/DA.ServiceHistorik.Api/Models/ServiceRule.cs`:
```csharp
namespace DA.ServiceHistorik.Api.Models;

public class ServiceRule
{
    public string Make { get; set; } = string.Empty;
    public string? Model { get; set; }
    public int IntervalMonths { get; set; }
    public int IntervalKm { get; set; }
}
```

- [ ] **Step 4: Opret SentReminder (EF entity)**

`src/DA.ServiceHistorik.Api/Models/SentReminder.cs`:
```csharp
namespace DA.ServiceHistorik.Api.Models;

public class SentReminder
{
    public int Id { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public ReminderType ReminderType { get; set; }
    public DateTime ServiceDate { get; set; }
    public DateTime SentAt { get; set; }
}
```

- [ ] **Step 5: Byg og verificér**

```bash
dotnet build
```
Forventet: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/
git commit -m "feat: tilføj domæne-modeller (ServiceRecord, ServiceRule, SentReminder, ReminderType)"
```

---

## Task 3: Database (EF Core + Migration)

**Files:**
- Create: `src/DA.ServiceHistorik.Api/Data/AppDbContext.cs`
- Modify: `src/DA.ServiceHistorik.Api/appsettings.json`
- Modify: `src/DA.ServiceHistorik.Api/appsettings.Development.json`

- [ ] **Step 1: Opret AppDbContext**

`src/DA.ServiceHistorik.Api/Data/AppDbContext.cs`:
```csharp
using DA.ServiceHistorik.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DA.ServiceHistorik.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SentReminder> SentReminders => Set<SentReminder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SentReminder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.LicensePlate, e.ReminderType, e.ServiceDate })
                  .IsUnique();
            entity.Property(e => e.LicensePlate).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ReminderType).HasConversion<string>();
        });
    }
}
```

- [ ] **Step 2: Opdater appsettings.json med connection string placeholder**

`src/DA.ServiceHistorik.Api/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "DeresAutoApi": {
    "BaseUrl": "",
    "ApiKey": ""
  },
  "Reimund": {
    "BaseUrl": "",
    "ApiKey": ""
  },
  "Resend": {
    "ApiToken": ""
  },
  "Notifications": {
    "FallbackEmail": "info@deresauto.gl",
    "FromEmail": "service@deresauto.gl",
    "FromName": "Deres Auto"
  },
  "Hangfire": {
    "DashboardUser": "admin",
    "DashboardPassword": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 3: Sæt lokal development connection string**

`src/DA.ServiceHistorik.Api/appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=DA.ServiceHistorik.Dev;Trusted_Connection=True;"
  }
}
```

- [ ] **Step 4: Tilføj EF til Program.cs (minimal, kun for at migration kan køre)**

`src/DA.ServiceHistorik.Api/Program.cs`:
```csharp
using DA.ServiceHistorik.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
```

- [ ] **Step 5: Opret og kør EF migration**

```bash
cd /Users/deresnickermand/da-service-historik
dotnet ef migrations add InitialCreate --project src/DA.ServiceHistorik.Api --startup-project src/DA.ServiceHistorik.Api
dotnet ef database update --project src/DA.ServiceHistorik.Api --startup-project src/DA.ServiceHistorik.Api
```
Forventet: `Done.`

- [ ] **Step 6: Commit**

```bash
git add src/ 
git commit -m "feat: tilføj AppDbContext og EF migration for SentReminders"
```

---

## Task 4: ServiceRuleEngine (TDD)

**Files:**
- Create: `src/DA.ServiceHistorik.Api/Services/IServiceRuleEngine.cs`
- Create: `src/DA.ServiceHistorik.Api/Services/ServiceRuleEngine.cs`
- Create: `tests/DA.ServiceHistorik.Tests/ServiceRuleEngineTests.cs`

- [ ] **Step 1: Skriv failing tests**

`tests/DA.ServiceHistorik.Tests/ServiceRuleEngineTests.cs`:
```csharp
using DA.ServiceHistorik.Api.Models;
using DA.ServiceHistorik.Api.Services;
using Microsoft.Extensions.Configuration;

namespace DA.ServiceHistorik.Tests;

public class ServiceRuleEngineTests
{
    private static IConfiguration BuildConfig(string json)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();
    }

    private const string RulesJson = """
    {
      "rules": [
        { "make": "Mercedes-Benz", "model": "Vito 639", "intervalMonths": 6, "intervalKm": 5000 },
        { "make": "Mercedes-Benz", "intervalMonths": 12, "intervalKm": 8000 },
        { "make": "DEFAULT", "intervalMonths": 6, "intervalKm": 8000 }
      ]
    }
    """;

    [Fact]
    public void GetRule_ModelSpecificMatch_ReturnsMostSpecificRule()
    {
        var engine = new ServiceRuleEngine(BuildConfig(RulesJson));
        var rule = engine.GetRule("Mercedes-Benz", "Vito 639");
        Assert.Equal(6, rule.IntervalMonths);
        Assert.Equal(5000, rule.IntervalKm);
    }

    [Fact]
    public void GetRule_MakeOnlyMatch_ReturnsMakeRule()
    {
        var engine = new ServiceRuleEngine(BuildConfig(RulesJson));
        var rule = engine.GetRule("Mercedes-Benz", "C300");
        Assert.Equal(12, rule.IntervalMonths);
        Assert.Equal(8000, rule.IntervalKm);
    }

    [Fact]
    public void GetRule_NoMatch_ReturnsDefault()
    {
        var engine = new ServiceRuleEngine(BuildConfig(RulesJson));
        var rule = engine.GetRule("Ukendt Mærke", "Model X");
        Assert.Equal(6, rule.IntervalMonths);
        Assert.Equal(8000, rule.IntervalKm);
    }

    [Fact]
    public void GetRule_CaseInsensitiveMatch()
    {
        var engine = new ServiceRuleEngine(BuildConfig(RulesJson));
        var rule = engine.GetRule("mercedes-benz", "c300");
        Assert.Equal(12, rule.IntervalMonths);
    }

    [Fact]
    public void CalculateNextServiceDate_AddsIntervalMonths()
    {
        var engine = new ServiceRuleEngine(BuildConfig(RulesJson));
        var rule = new ServiceRule { IntervalMonths = 6, IntervalKm = 8000 };
        var lastService = new DateTime(2025, 10, 1);
        var next = engine.CalculateNextServiceDate(lastService, rule);
        Assert.Equal(new DateTime(2026, 4, 1), next);
    }

    [Fact]
    public void DaysUntilService_ReturnsCorrectDays()
    {
        var engine = new ServiceRuleEngine(BuildConfig(RulesJson));
        var nextService = DateTime.Today.AddDays(30);
        var days = engine.DaysUntilService(nextService);
        Assert.Equal(30, days);
    }
}
```

- [ ] **Step 2: Kør tests og verificér de fejler**

```bash
cd /Users/deresnickermand/da-service-historik
dotnet test tests/DA.ServiceHistorik.Tests
```
Forventet: FAIL — `ServiceRuleEngine` eksisterer ikke endnu.

- [ ] **Step 3: Opret interface**

`src/DA.ServiceHistorik.Api/Services/IServiceRuleEngine.cs`:
```csharp
using DA.ServiceHistorik.Api.Models;

namespace DA.ServiceHistorik.Api.Services;

public interface IServiceRuleEngine
{
    ServiceRule GetRule(string make, string model);
    DateTime CalculateNextServiceDate(DateTime lastServiceDate, ServiceRule rule);
    int DaysUntilService(DateTime nextServiceDate);
}
```

- [ ] **Step 4: Implementér ServiceRuleEngine**

`src/DA.ServiceHistorik.Api/Services/ServiceRuleEngine.cs`:
```csharp
using DA.ServiceHistorik.Api.Models;
using Microsoft.Extensions.Configuration;

namespace DA.ServiceHistorik.Api.Services;

public class ServiceRuleEngine : IServiceRuleEngine
{
    private readonly List<ServiceRule> _rules;

    public ServiceRuleEngine(IConfiguration configuration)
    {
        _rules = configuration.GetSection("rules").Get<List<ServiceRule>>() ?? [];
    }

    public ServiceRule GetRule(string make, string model)
    {
        // 1. Exact make + model match
        var exact = _rules.FirstOrDefault(r =>
            r.Model != null &&
            r.Make.Equals(make, StringComparison.OrdinalIgnoreCase) &&
            r.Model.Equals(model, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // 2. Make-only match (no model on rule)
        var makeOnly = _rules.FirstOrDefault(r =>
            r.Model == null &&
            r.Make.Equals(make, StringComparison.OrdinalIgnoreCase));
        if (makeOnly != null) return makeOnly;

        // 3. DEFAULT fallback
        return _rules.First(r => r.Make.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase));
    }

    public DateTime CalculateNextServiceDate(DateTime lastServiceDate, ServiceRule rule)
        => lastServiceDate.AddMonths(rule.IntervalMonths);

    public int DaysUntilService(DateTime nextServiceDate)
        => (nextServiceDate.Date - DateTime.Today).Days;
}
```

- [ ] **Step 5: Kør tests og verificér de passerer**

```bash
dotnet test tests/DA.ServiceHistorik.Tests
```
Forventet: `Passed!`

- [ ] **Step 6: Commit**

```bash
git add src/ tests/
git commit -m "feat: ServiceRuleEngine med make/model matching og dato-beregning (TDD)"
```

---

## Task 5: DeresAutoApiClient (TDD)

**Files:**
- Create: `src/DA.ServiceHistorik.Api/Services/IDeresAutoApiClient.cs`
- Create: `src/DA.ServiceHistorik.Api/Services/DeresAutoApiClient.cs`
- Create: `tests/DA.ServiceHistorik.Tests/DeresAutoApiClientTests.cs`

> **Vigtigt:** Vi kender ikke det præcise endpoint-format for Deres Auto API. Implementationen bruger konfiguration og er let at tilpasse.

- [ ] **Step 1: Skriv failing test**

`tests/DA.ServiceHistorik.Tests/DeresAutoApiClientTests.cs`:
```csharp
using System.Net;
using System.Text.Json;
using DA.ServiceHistorik.Api.Models;
using DA.ServiceHistorik.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DA.ServiceHistorik.Tests;

public class DeresAutoApiClientTests
{
    [Fact]
    public async Task GetServiceRecordsAsync_ParsesResponseCorrectly()
    {
        var json = """
        [
          {
            "licensePlate": "GJ12345",
            "make": "Toyota",
            "model": "RAV4 PHEV",
            "serviceType": "Stor service",
            "serviceDate": "2025-10-01T00:00:00",
            "kmAtService": 45000,
            "phoneNumber": "+299123456",
            "email": "kunde@example.com"
          }
        ]
        """;

        var handler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.deresauto.gl") };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["DeresAutoApi:BaseUrl"] = "https://api.deresauto.gl",
                ["DeresAutoApi:ApiKey"] = "test-key"
            })
            .Build();

        var logger = new Mock<ILogger<DeresAutoApiClient>>().Object;
        var client = new DeresAutoApiClient(httpClient, config, logger);

        var records = await client.GetServiceRecordsAsync();

        Assert.Single(records);
        Assert.Equal("GJ12345", records[0].LicensePlate);
        Assert.Equal("Toyota", records[0].Make);
        Assert.Equal("RAV4 PHEV", records[0].Model);
    }
}

public class MockHttpMessageHandler(string response, HttpStatusCode statusCode) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json")
        });
}
```

- [ ] **Step 2: Kør test og verificér den fejler**

```bash
dotnet test tests/DA.ServiceHistorik.Tests --filter DeresAutoApiClientTests
```
Forventet: FAIL.

- [ ] **Step 3: Opret interface**

`src/DA.ServiceHistorik.Api/Services/IDeresAutoApiClient.cs`:
```csharp
using DA.ServiceHistorik.Api.Models;

namespace DA.ServiceHistorik.Api.Services;

public interface IDeresAutoApiClient
{
    Task<List<ServiceRecord>> GetServiceRecordsAsync(CancellationToken ct = default);
}
```

- [ ] **Step 4: Implementér DeresAutoApiClient**

`src/DA.ServiceHistorik.Api/Services/DeresAutoApiClient.cs`:
```csharp
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
```

- [ ] **Step 5: Kør test og verificér den passerer**

```bash
dotnet test tests/DA.ServiceHistorik.Tests --filter DeresAutoApiClientTests
```
Forventet: `Passed!`

- [ ] **Step 6: Commit**

```bash
git add src/ tests/
git commit -m "feat: DeresAutoApiClient med HTTP og JSON-parsing (TDD)"
```

---

## Task 6: ReimundSmsService (TDD)

**Files:**
- Create: `src/DA.ServiceHistorik.Api/Services/IReimundSmsService.cs`
- Create: `src/DA.ServiceHistorik.Api/Services/ReimundSmsService.cs`

> **Vigtigt:** Tilpas endpoint og request-format til Reimunds faktiske API-dokumentation.

- [ ] **Step 1: Opret interface**

`src/DA.ServiceHistorik.Api/Services/IReimundSmsService.cs`:
```csharp
namespace DA.ServiceHistorik.Api.Services;

public interface IReimundSmsService
{
    Task<bool> SendAsync(string toPhoneNumber, string message, CancellationToken ct = default);
}
```

- [ ] **Step 2: Implementér ReimundSmsService**

`src/DA.ServiceHistorik.Api/Services/ReimundSmsService.cs`:
```csharp
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
```

- [ ] **Step 3: Byg og verificér**

```bash
dotnet build
```
Forventet: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/
git commit -m "feat: ReimundSmsService med HTTP-integration"
```

---

## Task 7: ResendEmailService (TDD)

**Files:**
- Create: `src/DA.ServiceHistorik.Api/Services/IResendEmailService.cs`
- Create: `src/DA.ServiceHistorik.Api/Services/ResendEmailService.cs`

- [ ] **Step 1: Opret interface**

`src/DA.ServiceHistorik.Api/Services/IResendEmailService.cs`:
```csharp
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
```

- [ ] **Step 2: Implementér ResendEmailService**

`src/DA.ServiceHistorik.Api/Services/ResendEmailService.cs`:
```csharp
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
```

- [ ] **Step 3: Byg og verificér**

```bash
dotnet build
```
Forventet: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/
git commit -m "feat: ResendEmailService med reminder og manglende-kontakt emails"
```

---

## Task 8: ReminderJob (TDD)

**Files:**
- Create: `src/DA.ServiceHistorik.Api/Jobs/ReminderJob.cs`
- Create: `tests/DA.ServiceHistorik.Tests/ReminderJobTests.cs`

- [ ] **Step 1: Skriv failing tests**

`tests/DA.ServiceHistorik.Tests/ReminderJobTests.cs`:
```csharp
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
```

- [ ] **Step 2: Kør tests og verificér de fejler**

```bash
dotnet test tests/DA.ServiceHistorik.Tests --filter ReminderJobTests
```
Forventet: FAIL — `ReminderJob` eksisterer ikke.

- [ ] **Step 3: Implementér ReminderJob**

`src/DA.ServiceHistorik.Api/Jobs/ReminderJob.cs`:
```csharp
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
```

- [ ] **Step 4: Kør alle tests og verificér de passerer**

```bash
dotnet test tests/DA.ServiceHistorik.Tests
```
Forventet: `Passed!`

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "feat: ReminderJob med duplicate-check, SMS, email og manglende-kontakt (TDD)"
```

---

## Task 9: Razor Pages Dashboard

**Files:**
- Create: `src/DA.ServiceHistorik.Api/Pages/_Layout.cshtml`
- Create: `src/DA.ServiceHistorik.Api/Pages/Search.cshtml`
- Create: `src/DA.ServiceHistorik.Api/Pages/Search.cshtml.cs`
- Create: `src/DA.ServiceHistorik.Api/Pages/Reminders.cshtml`
- Create: `src/DA.ServiceHistorik.Api/Pages/Reminders.cshtml.cs`

- [ ] **Step 1: Opret layout**

`src/DA.ServiceHistorik.Api/Pages/_Layout.cshtml`:
```html
<!DOCTYPE html>
<html lang="da">
<head>
    <meta charset="utf-8"/>
    <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
    <title>@ViewData["Title"] — Deres Auto Service</title>
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css"/>
</head>
<body>
<nav class="navbar navbar-dark bg-dark mb-4">
    <div class="container">
        <span class="navbar-brand">Deres Auto — Service Historik</span>
        <div>
            <a href="/Search" class="btn btn-outline-light btn-sm me-2">Søg nummerplade</a>
            <a href="/hangfire" class="btn btn-outline-warning btn-sm">Job Dashboard</a>
        </div>
    </div>
</nav>
<div class="container">
    @RenderBody()
</div>
</body>
</html>
```

- [ ] **Step 2: Opret Search side**

`src/DA.ServiceHistorik.Api/Pages/Search.cshtml`:
```html
@page
@model DA.ServiceHistorik.Api.Pages.SearchModel
@{
    ViewData["Title"] = "Søg nummerplade";
    Layout = "_Layout";
}
<h2>Søg på nummerplade</h2>
<form method="get" class="mb-4">
    <div class="input-group">
        <input type="text" name="plate" value="@Model.Plate" class="form-control"
               placeholder="Fx GJ12345" style="max-width:200px;text-transform:uppercase"/>
        <button class="btn btn-primary" type="submit">Søg</button>
    </div>
</form>

@if (Model.Plate != null && !Model.Records.Any())
{
    <p class="text-muted">Ingen service-records fundet for <strong>@Model.Plate</strong>.</p>
}

@if (Model.Records.Any())
{
    <h5>Service-historik for <strong>@Model.Plate</strong></h5>
    <table class="table table-striped">
        <thead>
        <tr>
            <th>Dato</th><th>Type</th><th>Mærke / Model</th><th>Km</th>
            <th>Næste service</th><th>Dage til service</th>
        </tr>
        </thead>
        <tbody>
        @foreach (var row in Model.Records)
        {
            <tr>
                <td>@row.ServiceDate.ToString("dd-MM-yyyy")</td>
                <td>@row.ServiceType</td>
                <td>@row.Make @row.Model</td>
                <td>@row.KmAtService.ToString("N0")</td>
                <td>@row.NextServiceDate.ToString("dd-MM-yyyy")</td>
                <td class="@(row.DaysUntilService <= 14 ? "text-danger fw-bold" : row.DaysUntilService <= 30 ? "text-warning" : "")">
                    @row.DaysUntilService dage
                </td>
            </tr>
        }
        </tbody>
    </table>
    <a href="/Reminders?plate=@Model.Plate" class="btn btn-outline-secondary btn-sm">
        Se sendte reminders
    </a>
}
```

`src/DA.ServiceHistorik.Api/Pages/Search.cshtml.cs`:
```csharp
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
```

- [ ] **Step 3: Opret Reminders side**

`src/DA.ServiceHistorik.Api/Pages/Reminders.cshtml`:
```html
@page
@model DA.ServiceHistorik.Api.Pages.RemindersModel
@{
    ViewData["Title"] = "Sendte reminders";
    Layout = "_Layout";
}
<h2>Sendte reminders — <strong>@Model.Plate</strong></h2>
<a href="/Search?plate=@Model.Plate" class="btn btn-outline-secondary btn-sm mb-3">← Tilbage til servicehistorik</a>

@if (!Model.Reminders.Any())
{
    <p class="text-muted">Ingen reminders sendt for denne nummerplade endnu.</p>
}
else
{
    <table class="table table-striped">
        <thead><tr><th>Type</th><th>Service-dato</th><th>Sendt</th></tr></thead>
        <tbody>
        @foreach (var r in Model.Reminders)
        {
            <tr>
                <td>@(r.ReminderType == DA.ServiceHistorik.Api.Models.ReminderType.ThirtyDay ? "30 dage" : "14 dage")</td>
                <td>@r.ServiceDate.ToString("dd-MM-yyyy")</td>
                <td>@r.SentAt.ToString("dd-MM-yyyy HH:mm")</td>
            </tr>
        }
        </tbody>
    </table>
}
```

`src/DA.ServiceHistorik.Api/Pages/Reminders.cshtml.cs`:
```csharp
using DA.ServiceHistorik.Api.Data;
using DA.ServiceHistorik.Api.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DA.ServiceHistorik.Api.Pages;

public class RemindersModel(AppDbContext db) : PageModel
{
    public string? Plate { get; private set; }
    public List<SentReminder> Reminders { get; private set; } = [];

    public async Task OnGetAsync(string? plate)
    {
        Plate = plate?.ToUpper().Trim();
        if (string.IsNullOrWhiteSpace(Plate)) return;

        Reminders = await db.SentReminders
            .Where(r => r.LicensePlate == Plate)
            .OrderByDescending(r => r.SentAt)
            .ToListAsync();
    }
}
```

- [ ] **Step 4: Byg og verificér**

```bash
dotnet build
```
Forventet: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/
git commit -m "feat: Razor Pages dashboard med søgning og reminder-historik"
```

---

## Task 10: Program.cs — Fuld DI og Hangfire

**Files:**
- Modify: `src/DA.ServiceHistorik.Api/Program.cs`

- [ ] **Step 1: Skriv komplet Program.cs**

`src/DA.ServiceHistorik.Api/Program.cs`:
```csharp
using DA.ServiceHistorik.Api.Data;
using DA.ServiceHistorik.Api.Jobs;
using DA.ServiceHistorik.Api.Services;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Resend;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var connectionString = config.GetConnectionString("DefaultConnection")!;

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Hangfire
builder.Services.AddHangfire(c => c
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));
builder.Services.AddHangfireServer();

// HTTP Clients
builder.Services.AddHttpClient<IDeresAutoApiClient, DeresAutoApiClient>(c =>
    c.BaseAddress = new Uri(config["DeresAutoApi:BaseUrl"]!));

builder.Services.AddHttpClient<IReimundSmsService, ReimundSmsService>(c =>
    c.BaseAddress = new Uri(config["Reimund:BaseUrl"]!));

// Resend
builder.Services.AddOptions<ResendClientOptions>().Configure(opts =>
    opts.ApiToken = config["Resend:ApiToken"]!);
builder.Services.AddTransient<IResend, ResendClient>();

// Services
builder.Services.AddSingleton<IServiceRuleEngine, ServiceRuleEngine>();
builder.Services.AddTransient<IResendEmailService, ResendEmailService>();
builder.Services.AddTransient<ReminderJob>();

// Razor Pages
builder.Services.AddRazorPages();

var app = builder.Build();

// Hangfire Dashboard med Basic Auth
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireBasicAuthFilter(
        config["Hangfire:DashboardUser"]!,
        config["Hangfire:DashboardPassword"]!)]
});

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

// Sæt dagligt job kl. 16:00 (Grønlandsk tid = UTC-3 → UTC 19:00)
RecurringJob.AddOrUpdate<ReminderJob>(
    "daglig-service-reminder",
    job => job.RunAsync(CancellationToken.None),
    "0 19 * * *");  // 19:00 UTC = 16:00 WGT

app.Run();
```

- [ ] **Step 2: Tilføj HangfireBasicAuthFilter**

`src/DA.ServiceHistorik.Api/Jobs/HangfireBasicAuthFilter.cs`:
```csharp
using System.Net.Http.Headers;
using System.Text;
using Hangfire.Dashboard;

namespace DA.ServiceHistorik.Api.Jobs;

public class HangfireBasicAuthFilter(string username, string password) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var header = httpContext.Request.Headers["Authorization"].FirstOrDefault();

        if (header == null || !header.StartsWith("Basic "))
        {
            Challenge(httpContext);
            return false;
        }

        var encoded = header["Basic ".Length..].Trim();
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        var parts = decoded.Split(':', 2);

        if (parts.Length != 2 || parts[0] != username || parts[1] != password)
        {
            Challenge(httpContext);
            return false;
        }

        return true;
    }

    private static void Challenge(HttpContext context)
    {
        context.Response.StatusCode = 401;
        context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire Dashboard\"";
    }
}
```

- [ ] **Step 3: Byg og verificér**

```bash
cd /Users/deresnickermand/da-service-historik
dotnet build
```
Forventet: `Build succeeded.`

- [ ] **Step 4: Kør alle tests**

```bash
dotnet test
```
Forventet: `Passed!`

- [ ] **Step 5: Commit**

```bash
git add src/
git commit -m "feat: komplet Program.cs med Hangfire, DI, Razor Pages og daglig job kl 16:00 WGT"
```

---

## Task 11: GitHub Actions — Deploy til Azure

**Files:**
- Create: `.github/workflows/deploy.yml`
- Create: `.gitignore`

- [ ] **Step 1: Opret .gitignore**

`.gitignore`:
```
bin/
obj/
.vs/
*.user
appsettings.Development.json
*.env
.DS_Store
```

- [ ] **Step 2: Opret GitHub Actions workflow**

`.github/workflows/deploy.yml`:
```yaml
name: Build og Deploy til Azure

on:
  push:
    branches: [main]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Kør tests
        run: dotnet test tests/DA.ServiceHistorik.Tests --configuration Release --no-build --verbosity normal
        continue-on-error: false

      - name: Publish
        run: dotnet publish src/DA.ServiceHistorik.Api/DA.ServiceHistorik.Api.csproj \
          --configuration Release \
          --output ./publish

      - name: Deploy til Azure Web App
        uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ secrets.AZURE_WEBAPP_NAME }}
          publish-profile: ${{ secrets.AZURE_PUBLISH_PROFILE }}
          package: ./publish
```

- [ ] **Step 3: Commit**

```bash
git add .github/ .gitignore
git commit -m "feat: GitHub Actions CI/CD til Azure App Service"
git push
```

---

## Task 12: Azure Opsætning (Gøres i Azure Portal)

> Disse trin udføres manuelt i Azure Portal (portal.azure.com). Du skal oprette en gratis konto hvis du ikke har en.

- [ ] **Step 1: Opret Resource Group**
  - Gå til portal.azure.com
  - Søg "Resource groups" → Create
  - Navn: `rg-da-service-historik`
  - Region: `North Europe`

- [ ] **Step 2: Opret Azure SQL Server + Database**
  - Søg "SQL Database" → Create
  - Server: opret ny → `da-servicehistorik-sql`
  - Database: `DA.ServiceHistorik`
  - Pricing tier: **Basic (5 DTU, ~$5/md)** er rigeligt
  - Kopier connection string fra Azure — format:
    `Server=tcp:da-servicehistorik-sql.database.windows.net;Database=DA.ServiceHistorik;User ID=<user>;Password=<pass>;`

- [ ] **Step 3: Kør EF migration mod Azure SQL**

  Indsæt Azure connection string temporært i appsettings.Development.json og kør:
  ```bash
  dotnet ef database update --project src/DA.ServiceHistorik.Api
  ```

- [ ] **Step 4: Opret Azure App Service**
  - Søg "App Service" → Create
  - Navn: `da-service-historik` (det navn du bruger i GitHub secret)
  - Runtime: `.NET 8`
  - OS: Linux
  - Plan: **B1 Basic (~$12/md)**

- [ ] **Step 5: Sæt Environment Variables i App Service**
  - I Azure App Service → Configuration → Application settings:
  ```
  ConnectionStrings__DefaultConnection  = <Azure SQL connection string>
  DeresAutoApi__BaseUrl                 = <URL til Deres Auto API>
  DeresAutoApi__ApiKey                  = <API nøgle>
  Reimund__BaseUrl                      = <Reimund SMS API URL>
  Reimund__ApiKey                       = <Reimund API nøgle>
  Resend__ApiToken                      = <Resend API token fra resend.com>
  Hangfire__DashboardUser               = admin
  Hangfire__DashboardPassword           = <vælg et stærkt kodeord>
  Notifications__FallbackEmail          = info@deresauto.gl
  Notifications__FromEmail              = service@deresauto.gl
  Notifications__FromName               = Deres Auto
  ```

- [ ] **Step 6: Hent GitHub Deploy Publish Profile**
  - I App Service → Overview → Download publish profile
  - I GitHub repo → Settings → Secrets → Actions:
    - `AZURE_WEBAPP_NAME` = `da-service-historik`
    - `AZURE_PUBLISH_PROFILE` = (indsæt hele indholdet af publish profile-filen)

- [ ] **Step 7: Push og verificér deploy**
  ```bash
  git push origin main
  ```
  Gå til GitHub → Actions og se deployet køre. Forventet: grønt ✓

- [ ] **Step 8: Verificér Hangfire dashboard virker**

  Åbn `https://da-service-historik.azurewebsites.net/hangfire` i browser.
  Log ind med admin + dit valgte kodeord.
  Verificér at jobbet `daglig-service-reminder` er registreret under Recurring Jobs.

---

## Spec Coverage Check

| Krav fra spec | Task |
|---|---|
| .NET 8 Web API | Task 1 |
| Hangfire dagligt kl. 16:00 WGT | Task 10 |
| Hent records fra Deres Auto API | Task 5 |
| ServiceRules fra servicerules.json | Task 4 |
| Mercedes 12 mdr, andre 6 mdr | servicerules.json (allerede done) |
| 30-dages og 14-dages reminders | Task 8 |
| SMS via Reimund | Task 6 + 8 |
| Email via Resend | Task 7 + 8 |
| Manglende kontakt → info@deresauto.gl | Task 7 + 8 |
| Ingen duplikater (SentReminders DB) | Task 3 + 8 |
| Hangfire dashboard med login | Task 10 + 11 |
| Søg nummerplade → servicehistorik | Task 9 |
| Se sendte reminders per nummerplade | Task 9 |
| Azure SQL | Task 3 + 12 |
| GitHub Actions auto-deploy | Task 11 |
