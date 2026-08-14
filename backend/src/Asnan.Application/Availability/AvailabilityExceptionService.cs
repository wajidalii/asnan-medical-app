using Asnan.Application.Common;
using Asnan.Application.Notifications;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Availability;

public class AvailabilityExceptionService : IAvailabilityExceptionService
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationDispatchService _notificationDispatch;

    public AvailabilityExceptionService(IApplicationDbContext db, INotificationDispatchService notificationDispatch)
    {
        _db = db;
        _notificationDispatch = notificationDispatch;
    }

    public async Task<AvailabilityExceptionListResult> GetAllAsync(Guid doctorId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == doctorId, cancellationToken);
        if (doctor is null)
        {
            return new AvailabilityExceptionListResult(AvailabilityExceptionQueryStatus.DoctorNotFound);
        }

        if (!IsAuthorized(doctor, caller))
        {
            return new AvailabilityExceptionListResult(AvailabilityExceptionQueryStatus.Forbidden);
        }

        var exceptions = await _db.DoctorAvailabilityExceptions
            .Where(e => e.DoctorProfileId == doctorId)
            .OrderBy(e => e.Date)
            .ToListAsync(cancellationToken);

        return new AvailabilityExceptionListResult(AvailabilityExceptionQueryStatus.Success, exceptions.Select(ToDto).ToList());
    }

    public async Task<AvailabilityExceptionMutationResult> CreateAsync(Guid doctorId, CreateAvailabilityExceptionDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == doctorId, cancellationToken);
        if (doctor is null)
        {
            return new AvailabilityExceptionMutationResult(AvailabilityExceptionMutationStatus.DoctorNotFound);
        }

        if (!IsAuthorized(doctor, caller))
        {
            return new AvailabilityExceptionMutationResult(AvailabilityExceptionMutationStatus.Forbidden);
        }

        if (await HasConflictAsync(doctorId, dto.Date, dto.StartTime, dto.EndTime, excludeExceptionId: null, cancellationToken))
        {
            return new AvailabilityExceptionMutationResult(AvailabilityExceptionMutationStatus.Conflict);
        }

        var exception = new DoctorAvailabilityException
        {
            DoctorProfileId = doctorId,
            Date = dto.Date,
            Type = dto.Type,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Reason = dto.Reason,
        };
        _db.DoctorAvailabilityExceptions.Add(exception);
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyAffectedPatientsAsync(doctor, exception, cancellationToken);

        return new AvailabilityExceptionMutationResult(AvailabilityExceptionMutationStatus.Success, ToDto(exception));
    }

    public async Task<AvailabilityExceptionMutationResult> UpdateAsync(Guid doctorId, Guid exceptionId, UpdateAvailabilityExceptionDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == doctorId, cancellationToken);
        if (doctor is null)
        {
            return new AvailabilityExceptionMutationResult(AvailabilityExceptionMutationStatus.DoctorNotFound);
        }

        if (!IsAuthorized(doctor, caller))
        {
            return new AvailabilityExceptionMutationResult(AvailabilityExceptionMutationStatus.Forbidden);
        }

        var exception = await _db.DoctorAvailabilityExceptions.FirstOrDefaultAsync(e => e.Id == exceptionId && e.DoctorProfileId == doctorId, cancellationToken);
        if (exception is null)
        {
            return new AvailabilityExceptionMutationResult(AvailabilityExceptionMutationStatus.NotFound);
        }

        if (await HasConflictAsync(doctorId, dto.Date, dto.StartTime, dto.EndTime, excludeExceptionId: exceptionId, cancellationToken))
        {
            return new AvailabilityExceptionMutationResult(AvailabilityExceptionMutationStatus.Conflict);
        }

        exception.Date = dto.Date;
        exception.Type = dto.Type;
        exception.StartTime = dto.StartTime;
        exception.EndTime = dto.EndTime;
        exception.Reason = dto.Reason;
        exception.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyAffectedPatientsAsync(doctor, exception, cancellationToken);

        return new AvailabilityExceptionMutationResult(AvailabilityExceptionMutationStatus.Success, ToDto(exception));
    }

    public async Task<AvailabilityExceptionMutationResult> DeleteAsync(Guid doctorId, Guid exceptionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == doctorId, cancellationToken);
        if (doctor is null)
        {
            return new AvailabilityExceptionMutationResult(AvailabilityExceptionMutationStatus.DoctorNotFound);
        }

        if (!IsAuthorized(doctor, caller))
        {
            return new AvailabilityExceptionMutationResult(AvailabilityExceptionMutationStatus.Forbidden);
        }

        var exception = await _db.DoctorAvailabilityExceptions.FirstOrDefaultAsync(e => e.Id == exceptionId && e.DoctorProfileId == doctorId, cancellationToken);
        if (exception is null)
        {
            return new AvailabilityExceptionMutationResult(AvailabilityExceptionMutationStatus.NotFound);
        }

        exception.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new AvailabilityExceptionMutationResult(AvailabilityExceptionMutationStatus.Success);
    }

    private static bool IsAuthorized(DoctorProfile doctor, CallerContext caller) => caller.IsAdmin || doctor.UserId == caller.UserId;

    /// <summary>
    /// A whole-day block (null Start/EndTime) conflicts with anything else on
    /// that date; a partial-day entry conflicts with an existing whole-day
    /// block or with any time-overlapping partial-day entry.
    /// </summary>
    private async Task<bool> HasConflictAsync(Guid doctorId, DateOnly date, TimeOnly? start, TimeOnly? end, Guid? excludeExceptionId, CancellationToken cancellationToken)
    {
        var existing = await _db.DoctorAvailabilityExceptions
            .Where(e => e.DoctorProfileId == doctorId && e.Date == date && (excludeExceptionId == null || e.Id != excludeExceptionId))
            .ToListAsync(cancellationToken);

        if (existing.Count == 0)
        {
            return false;
        }

        if (start is null || end is null)
        {
            return true;
        }

        return existing.Any(e => e.StartTime is null || e.EndTime is null || (start < e.EndTime.Value && e.StartTime.Value < end));
    }

    private static AvailabilityExceptionDto ToDto(DoctorAvailabilityException e) => new(e.Id, e.Date, e.Type, e.StartTime, e.EndTime, e.Reason);

    /// <summary>
    /// Notifies patients whose already-Scheduled appointment falls inside a
    /// new/changed <see cref="AvailabilityExceptionType.Unavailable"/>
    /// window — issue #31's "doctor availability changes" trigger.
    /// ExtraAvailability additions don't affect any existing booking, so
    /// they're not notified. This does not itself cancel/move the affected
    /// appointment(s); it's a "please check your appointment" nudge, not a
    /// new cancellation-cascade feature.
    /// </summary>
    private async Task NotifyAffectedPatientsAsync(DoctorProfile doctor, DoctorAvailabilityException exception, CancellationToken cancellationToken)
    {
        if (exception.Type != AvailabilityExceptionType.Unavailable)
        {
            return;
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(doctor.TimeZoneId);
        var windowStartLocal = exception.Date.ToDateTime(exception.StartTime ?? TimeOnly.MinValue);
        var windowEndLocal = exception.StartTime is null || exception.EndTime is null
            ? exception.Date.AddDays(1).ToDateTime(TimeOnly.MinValue)
            : exception.Date.ToDateTime(exception.EndTime.Value);
        var windowStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(windowStartLocal, DateTimeKind.Unspecified), timeZone);
        var windowEndUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(windowEndLocal, DateTimeKind.Unspecified), timeZone);

        var affectedAppointments = await _db.Appointments
            .Where(a => a.DoctorProfileId == doctor.Id && a.Status == AppointmentStatus.Scheduled)
            .Where(a => a.SlotStartUtc < windowEndUtc && a.SlotEndUtc > windowStartUtc)
            .ToListAsync(cancellationToken);

        foreach (var appointment in affectedAppointments)
        {
            await _notificationDispatch.DispatchAsync(
                appointment.PatientUserId,
                NotificationCategory.DoctorAvailability,
                new PushNotification(
                    "Doctor availability changed",
                    $"Dr. {doctor.FullName}'s availability has changed — please check your appointment.",
                    $"asnan://appointments/{appointment.Id}"),
                cancellationToken);
        }
    }
}
