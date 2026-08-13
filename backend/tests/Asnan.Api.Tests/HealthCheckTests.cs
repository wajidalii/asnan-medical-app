using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Asnan.Api.Tests;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LivenessEndpoint_ReturnsHealthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PingEndpoint_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
