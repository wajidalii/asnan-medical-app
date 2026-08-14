using Asnan.Application.Auditing;

namespace Asnan.Api.Tests;

/// <summary>
/// Test double for <see cref="IAuditLogger"/> — records calls in-memory
/// instead of touching the database, so tests can assert an audit entry
/// was actually recorded (issue #36's testing requirement) without the
/// real implementation's IHttpContextAccessor dependency.
/// </summary>
public class RecordingAuditLogger : IAuditLogger
{
    public List<(Guid? UserId, string EventType, string? Detail)> Entries { get; } = [];

    public void Record(Guid? userId, string eventType, string? detail = null) => Entries.Add((userId, eventType, detail));
}
