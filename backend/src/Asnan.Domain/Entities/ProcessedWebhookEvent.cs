using Asnan.Domain.Common;

namespace Asnan.Domain.Entities;

/// <summary>
/// Dedupe marker for provider webhook deliveries — ARCHITECTURE.md §8. A
/// unique constraint on <see cref="ProviderEventId"/> is what actually makes
/// retried/duplicate deliveries a no-op; see PaymentService.
/// </summary>
public class ProcessedWebhookEvent : BaseEntity
{
    public string ProviderEventId { get; set; } = null!;

    public DateTime ProcessedAtUtc { get; set; }
}
