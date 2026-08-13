using Asnan.Domain.Common;

namespace Asnan.Domain.Entities;

/// <summary>
/// Append-only (no update/delete path exposed by design). Records
/// security-relevant events: login, password change, refund, admin actions.
/// <see cref="Detail"/> must never contain the denylisted fields from
/// ARCHITECTURE.md §13 (passwords, OTP codes, tokens, payment secrets).
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>Null for system/unauthenticated events (e.g. a failed login for an unknown email).</summary>
    public Guid? UserId { get; set; }

    public string EventType { get; set; } = null!;

    public string? Detail { get; set; }

    public string? IpAddress { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
