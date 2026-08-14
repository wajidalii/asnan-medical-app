using Asnan.Infrastructure.Payments;
using Asp.Versioning;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Asnan.Api.Controllers;

/// <summary>
/// Dev/staging-only surface for simulating a payment provider settling a
/// mock checkout session (issue #19) — never reachable in Production,
/// regardless of which <c>Payment:Provider</c> is configured, so a
/// misconfiguration can't accidentally expose a payment-forcing endpoint
/// in production.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments/mock/sessions")]
public class MockPaymentsController : ControllerBase
{
    private readonly IMockPaymentProviderConfirmation _confirmation;
    private readonly IHostEnvironment _environment;

    public MockPaymentsController(IMockPaymentProviderConfirmation confirmation, IHostEnvironment environment)
    {
        _confirmation = confirmation;
        _environment = environment;
    }

    [HttpPost("{providerSessionId}/confirm")]
    public IActionResult Confirm(string providerSessionId, ConfirmMockPaymentDto dto)
    {
        if (_environment.IsProduction())
        {
            return NotFound();
        }

        var delivery = _confirmation.Confirm(providerSessionId, dto.Succeeded, dto.FailureReason);

        return delivery is null ? NotFound() : Ok(delivery);
    }
}

public record ConfirmMockPaymentDto(bool Succeeded, string? FailureReason);
