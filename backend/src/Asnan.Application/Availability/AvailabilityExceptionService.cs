using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Availability;

public class AvailabilityExceptionService : IAvailabilityExceptionService
{
    private readonly IApplicationDbContext _db;

    public AvailabilityExceptionService(IApplicationDbContext db)
    {
        _db = db;
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
}
