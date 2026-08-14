using Asnan.Api.Extensions;
using Asnan.Application.Payments;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Asnan.Api.Controllers;

/// <summary>
/// Checkout + provider webhook — ARCHITECTURE.md §8. The webhook action is
/// deliberately anonymous (a payment provider has no JWT); it is protected
/// exclusively by <see cref="IPaymentService.ProcessWebhookAsync"/>'s
/// signature verification, never by ASP.NET Core auth.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("checkout")]
    [Authorize]
    public async Task<IActionResult> Checkout(CreateCheckoutDto dto, CancellationToken cancellationToken)
    {
        var result = await _paymentService.CreateCheckoutAsync(User.GetUserId(), dto, cancellationToken);

        return result.Status switch
        {
            CreateCheckoutStatus.Success => Ok(result.Checkout),
            CreateCheckoutStatus.HoldNotFound => NotFound(),
            CreateCheckoutStatus.HoldExpired => Problem(statusCode: StatusCodes.Status409Conflict, title: "This hold has expired."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(CreateCheckoutStatus)}: {result.Status}"),
        };
    }

    [HttpPost("webhook")]
    [EnableRateLimiting("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
        var result = await _paymentService.ProcessWebhookAsync(new WebhookRequest(rawBody, headers), cancellationToken);

        return result.Status switch
        {
            ProcessWebhookStatus.InvalidSignature => Unauthorized(),
            // Processed/AlreadyProcessed/UnknownTransaction all ack with 200 — a provider retries
            // on anything but 2xx, and none of these are something retrying would fix.
            _ => Ok(),
        };
    }
}
