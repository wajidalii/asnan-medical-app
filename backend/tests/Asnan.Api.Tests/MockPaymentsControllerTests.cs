using System.Net;
using System.Net.Http.Json;
using Asnan.Application.Payments;
using Asnan.Infrastructure.Payments;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Asnan.Api.Tests;

/// <summary>
/// HTTP-level tests for the dev-only mock-payment confirm endpoint (issue
/// #19) — proves the DI wiring (MockPaymentProvider registered against both
/// IPaymentProvider and IMockPaymentProviderConfirmation) actually resolves
/// through the real host, not just in isolation. Tagged into the "Database"
/// collection (despite not touching the DB itself) purely to serialize
/// against the other WebApplicationFactory&lt;Program&gt;-based test classes —
/// two concurrent host builds otherwise race on HostFactoryResolver's shared
/// static state (same class of issue DatabaseFixture's doc comment covers).
/// </summary>
[Collection("Database")]
public class MockPaymentsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MockPaymentsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Confirm_ForUnknownSession_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/payments/mock/sessions/no-such-session/confirm", new { Succeeded = true, FailureReason = (string?)null });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_ForRealSession_ReturnsOkWithSignedDelivery()
    {
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPaymentProvider>();
        var session = await provider.CreateSessionAsync(new PaymentRequest("idem-http-test", 75m, "USD", new Dictionary<string, string>()));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/v1/payments/mock/sessions/{session.ProviderSessionId}/confirm", new { Succeeded = true, FailureReason = (string?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var delivery = await response.Content.ReadFromJsonAsync<MockWebhookDelivery>();
        Assert.NotNull(delivery);
        Assert.False(string.IsNullOrWhiteSpace(delivery!.SignatureHeaderValue));
    }
}
