using Asnan.Application.Reminders;
using Asnan.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Asnan.Infrastructure.Reminders;

/// <summary>
/// Stub delivery — logs instead of pushing (real push delivery is
/// Milestone 8). Deliberately does not log any medical/appointment detail
/// beyond ids, matching the "no sensitive info in notification text" stance
/// applied elsewhere (ARCHITECTURE.md §10).
/// </summary>
public class LoggingReminderSender : IReminderSender
{
    private readonly ILogger<LoggingReminderSender> _logger;

    public LoggingReminderSender(ILogger<LoggingReminderSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(Appointment appointment, int offsetMinutes, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Reminder due for appointment {AppointmentId} ({OffsetMinutes} minutes before slot start) — no real push provider configured yet (Milestone 8).",
            appointment.Id,
            offsetMinutes);
        return Task.CompletedTask;
    }
}
