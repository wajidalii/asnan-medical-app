using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asnan.Application.Reminders;

/// <summary>
/// Scans for due reminders — issue #25. Only considers appointments still
/// <see cref="AppointmentStatus.Scheduled"/>: a cancelled (or otherwise no
/// longer Scheduled) appointment simply stops matching this query going
/// forward, which is a complete implementation of "cancelled appointments
/// have pending reminders suppressed" (the AC) — a reminder that hasn't
/// been created yet never gets created for a cancelled appointment, and one
/// already <see cref="ReminderStatus.Sent"/> can't be un-sent regardless.
/// No separate suppression flag/step is needed.
/// </summary>
public class ReminderSchedulingService : IReminderSchedulingService
{
    private readonly IApplicationDbContext _db;
    private readonly IReminderSender _sender;
    private readonly ReminderOptions _options;

    public ReminderSchedulingService(IApplicationDbContext db, IReminderSender sender, IOptions<ReminderOptions> options)
    {
        _db = db;
        _sender = sender;
        _options = options.Value;
    }

    public async Task<int> ScanAndSendDueRemindersAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var sentCount = 0;

        foreach (var offsetMinutes in _options.OffsetsMinutes)
        {
            var dueThreshold = nowUtc.AddMinutes(offsetMinutes);

            // Excludes only already-Sent reminders — a still-Pending one (a prior send attempt that
            // failed) must stay eligible so TrySendAsync below can retry it, not skip it forever.
            var dueAppointments = await _db.Appointments
                .Where(a => a.Status == AppointmentStatus.Scheduled)
                .Where(a => a.SlotStartUtc > nowUtc && a.SlotStartUtc <= dueThreshold)
                .Where(a => !_db.Reminders.Any(r => r.AppointmentId == a.Id && r.OffsetMinutes == offsetMinutes && r.Status == ReminderStatus.Sent))
                .ToListAsync(cancellationToken);

            foreach (var appointment in dueAppointments)
            {
                if (await TrySendAsync(appointment, offsetMinutes, nowUtc, cancellationToken))
                {
                    sentCount++;
                }
            }
        }

        return sentCount;
    }

    private async Task<bool> TrySendAsync(Appointment appointment, int offsetMinutes, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Reuse an existing Pending row (a previously-failed attempt being retried) rather than
        // inserting a second one — the unique index on (AppointmentId, OffsetMinutes) would reject that anyway.
        var reminder = await _db.Reminders.FirstOrDefaultAsync(r => r.AppointmentId == appointment.Id && r.OffsetMinutes == offsetMinutes, cancellationToken);
        if (reminder is null)
        {
            reminder = new Reminder { AppointmentId = appointment.Id, OffsetMinutes = offsetMinutes, Status = ReminderStatus.Pending };
            _db.Reminders.Add(reminder);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Lost a race to a concurrent scan creating the same (appointment, offset) reminder — already handled.
                return false;
            }
        }

        try
        {
            await _sender.SendAsync(appointment, offsetMinutes, cancellationToken);
        }
        catch
        {
            // Left Pending — retried on the next scan.
            return false;
        }

        reminder.Status = ReminderStatus.Sent;
        reminder.SentAtUtc = nowUtc;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
