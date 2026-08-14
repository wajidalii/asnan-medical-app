namespace Asnan.Application.Auditing;

/// <summary>
/// Security-relevant event trail — ARCHITECTURE.md §13. Adds an
/// <c>AuditLog</c> row to the CURRENT unit of work rather than saving
/// immediately: the caller's own <c>SaveChangesAsync</c> persists it
/// together with whatever business-logic changes it's attached to, so an
/// audit entry never survives a failed operation (or vice versa).
/// </summary>
public interface IAuditLogger
{
    /// <summary><paramref name="detail"/> must never contain a denylisted field — passwords, OTP codes, tokens, payment-provider secrets (ARCHITECTURE.md §13).</summary>
    void Record(Guid? userId, string eventType, string? detail = null);
}
