namespace Asnan.Application.Payments;

public class PaymentOptions
{
    public const string SectionName = "Payment";

    /// <summary>Mirrors the OtpProvider config pattern — only "Mock" exists today; see issue #60.</summary>
    public string Provider { get; set; } = "Mock";

    /// <summary>HMAC-SHA256 key the mock provider signs/verifies its simulated webhook payloads with. Dev/staging only — never set in Production.</summary>
    public string MockWebhookSigningKey { get; set; } = string.Empty;
}
