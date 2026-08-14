using Asnan.Application.Common;
using Asnan.Application.Notifications;
using Asnan.Application.Reminders;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Infrastructure.Reminders;

/// <summary>
/// Real push delivery for ReminderSchedulingService's due reminders — issue
/// #31 (replaces the LoggingReminderSender stub from #25). "Appointment
/// reminder" and "appointment starting soon" from ARCHITECTURE.md §10 are
/// the same underlying mechanism here, distinguished only by copy: the
/// smallest configured offset (typically 15 minutes) reads as "starting
/// soon", anything larger reads as a standard reminder — there's no
/// separate trigger for "starting soon" anywhere else in the domain.
/// </summary>
public class NotificationReminderSender : IReminderSender
{
    /// <summary>Offsets at or below this are worded as "starting soon" rather than a generic reminder.</summary>
    private const int StartingSoonThresholdMinutes = 15;

    private readonly IApplicationDbContext _db;
    private readonly INotificationDispatchService _notificationDispatch;

    public NotificationReminderSender(IApplicationDbContext db, INotificationDispatchService notificationDispatch)
    {
        _db = db;
        _notificationDispatch = notificationDispatch;
    }

    public async Task SendAsync(Appointment appointment, int offsetMinutes, CancellationToken cancellationToken = default)
    {
        var doctor = await _db.DoctorProfiles.FirstAsync(d => d.Id == appointment.DoctorProfileId, cancellationToken);

        var (title, body) = offsetMinutes <= StartingSoonThresholdMinutes
            ? ("Appointment starting soon", $"Your appointment with Dr. {doctor.FullName} is starting soon.")
            : ("Appointment reminder", $"Reminder: your appointment with Dr. {doctor.FullName} is coming up.");

        await _notificationDispatch.DispatchAsync(
            appointment.PatientUserId,
            NotificationCategory.Reminders,
            new PushNotification(title, body, $"asnan://appointments/{appointment.Id}"),
            cancellationToken);
    }
}
