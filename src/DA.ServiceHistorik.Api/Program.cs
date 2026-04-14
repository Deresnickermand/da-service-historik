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
{
    var baseUrl = config["DeresAutoApi:BaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
        c.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddHttpClient<IReimundSmsService, ReimundSmsService>(c =>
{
    var baseUrl = config["Reimund:BaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
        c.BaseAddress = new Uri(baseUrl);
});

// Resend
builder.Services.AddOptions<ResendClientOptions>().Configure(opts =>
    opts.ApiToken = config["Resend:ApiToken"] ?? string.Empty);
builder.Services.AddTransient<IResend, ResendClient>();

// Services
builder.Services.AddSingleton<IServiceRuleEngine, ServiceRuleEngine>();
builder.Services.AddTransient<IResendEmailService, ResendEmailService>();
builder.Services.AddTransient<ReminderJob>();

// Razor Pages
builder.Services.AddRazorPages();

var app = builder.Build();

// Hangfire Dashboard with Basic Auth
var dashUser = config["Hangfire:DashboardUser"] ?? "admin";
var dashPass = config["Hangfire:DashboardPassword"] ?? "changeme";
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireBasicAuthFilter(dashUser, dashPass)]
});

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

// Register recurring job: daily at 16:00 Greenland Time (WGT = UTC-3 → 19:00 UTC)
RecurringJob.AddOrUpdate<ReminderJob>(
    "daglig-service-reminder",
    job => job.RunAsync(CancellationToken.None),
    "0 19 * * *");

app.Run();
