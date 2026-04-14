using System.Net;
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
