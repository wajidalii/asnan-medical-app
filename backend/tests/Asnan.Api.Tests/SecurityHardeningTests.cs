using System.Net;
using System.Net.Http.Json;
using Asnan.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Asnan.Api.Tests;

/// <summary>
/// HTTP-level coverage for issue #36's security hardening pass: security
/// response headers and the new IP-agnostic rate limiter policies on
/// /otp/* and the payment webhook. Tagged into the "Database" collection
/// purely to serialize against the other WebApplicationFactory&lt;Program&gt;-based
/// test classes (see MockPaymentsControllerTests's doc comment).
/// </summary>
[Collection("Database")]
public class SecurityHardeningTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityHardeningTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnyResponse_IncludesStandardSecurityHeaders()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(response.Headers.Contains("Permissions-Policy"));
    }

    [Fact]
    public async Task OtpRequest_ExceedingGlobalBudget_ReturnsTooManyRequests()
    {
        var client = _factory.CreateClient();
        HttpResponseMessage? last = null;

        // Distinct destinations so each call bypasses OtpService's own
        // per-destination cooldown/hourly cap — this exercises only the
        // ASP.NET Core "otp" rate limiter policy (budget: 5/minute).
        for (var i = 0; i < 6; i++)
        {
            var dto = new { Destination = $"rate-limit-{Guid.NewGuid()}@test.local", Channel = OtpChannel.Email, Purpose = OtpPurpose.SignupVerification };
            last = await client.PostAsJsonAsync("/api/v1/otp/request", dto);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    [Fact]
    public async Task PaymentWebhook_WithoutValidSignature_ReturnsUnauthorized_NotRateLimited()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/v1/payments/webhook", JsonContent.Create(new { }));

        // Sanity check that the new "webhook" rate-limit policy doesn't
        // interfere with a single legitimate-volume request.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
