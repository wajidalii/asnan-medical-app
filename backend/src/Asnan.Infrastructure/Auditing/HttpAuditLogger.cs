using Asnan.Application.Auditing;
using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace Asnan.Infrastructure.Auditing;

/// <summary>
/// Captures the caller's IP from the current request — the one piece
/// AuditLog needs that Application-layer services don't otherwise have
/// access to, without threading an IP parameter through every audited
/// method signature.
/// </summary>
public class HttpAuditLogger : IAuditLogger
{
    private readonly IApplicationDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpAuditLogger(IApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Record(Guid? userId, string eventType, string? detail = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            EventType = eventType,
            Detail = detail,
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
        });
    }
}
