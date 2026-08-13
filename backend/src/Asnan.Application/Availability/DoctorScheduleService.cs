using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Availability;

public class DoctorScheduleService : IDoctorScheduleService
{
    private readonly IApplicationDbContext _db;

    public DoctorScheduleService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ScheduleListResult> GetAllAsync(Guid doctorId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == doctorId, cancellationToken);
        if (doctor is null)
        {
            return new ScheduleListResult(ScheduleQueryStatus.DoctorNotFound);
        }

        if (!IsAuthorized(doctor, caller))
        {
            return new ScheduleListResult(ScheduleQueryStatus.Forbidden);
        }

        var schedules = await _db.DoctorSchedules
            .Where(s => s.DoctorProfileId == doctorId)
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

        return new ScheduleListResult(ScheduleQueryStatus.Success, schedules.Select(ToDto).ToList());
    }

    public async Task<ScheduleMutationResult> CreateAsync(Guid doctorId, CreateDoctorScheduleDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == doctorId, cancellationToken);
        if (doctor is null)
        {
            return new ScheduleMutationResult(ScheduleMutationStatus.DoctorNotFound);
        }

        if (!IsAuthorized(doctor, caller))
        {
            return new ScheduleMutationResult(ScheduleMutationStatus.Forbidden);
        }

        if (await HasOverlapAsync(doctorId, dto.DayOfWeek, dto.StartTime, dto.EndTime, excludeScheduleId: null, cancellationToken))
        {
            return new ScheduleMutationResult(ScheduleMutationStatus.OverlappingWindow);
        }

        var schedule = new DoctorSchedule
        {
            DoctorProfileId = doctorId,
            DayOfWeek = dto.DayOfWeek,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            SlotDurationMinutes = dto.SlotDurationMinutes,
            BufferMinutes = dto.BufferMinutes,
        };
        _db.DoctorSchedules.Add(schedule);
        await _db.SaveChangesAsync(cancellationToken);

        return new ScheduleMutationResult(ScheduleMutationStatus.Success, ToDto(schedule));
    }

    public async Task<ScheduleMutationResult> UpdateAsync(Guid doctorId, Guid scheduleId, UpdateDoctorScheduleDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == doctorId, cancellationToken);
        if (doctor is null)
        {
            return new ScheduleMutationResult(ScheduleMutationStatus.DoctorNotFound);
        }

        if (!IsAuthorized(doctor, caller))
        {
            return new ScheduleMutationResult(ScheduleMutationStatus.Forbidden);
        }

        var schedule = await _db.DoctorSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId && s.DoctorProfileId == doctorId, cancellationToken);
        if (schedule is null)
        {
            return new ScheduleMutationResult(ScheduleMutationStatus.NotFound);
        }

        if (await HasOverlapAsync(doctorId, dto.DayOfWeek, dto.StartTime, dto.EndTime, excludeScheduleId: scheduleId, cancellationToken))
        {
            return new ScheduleMutationResult(ScheduleMutationStatus.OverlappingWindow);
        }

        schedule.DayOfWeek = dto.DayOfWeek;
        schedule.StartTime = dto.StartTime;
        schedule.EndTime = dto.EndTime;
        schedule.SlotDurationMinutes = dto.SlotDurationMinutes;
        schedule.BufferMinutes = dto.BufferMinutes;
        schedule.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new ScheduleMutationResult(ScheduleMutationStatus.Success, ToDto(schedule));
    }

    public async Task<ScheduleMutationResult> DeleteAsync(Guid doctorId, Guid scheduleId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == doctorId, cancellationToken);
        if (doctor is null)
        {
            return new ScheduleMutationResult(ScheduleMutationStatus.DoctorNotFound);
        }

        if (!IsAuthorized(doctor, caller))
        {
            return new ScheduleMutationResult(ScheduleMutationStatus.Forbidden);
        }

        var schedule = await _db.DoctorSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId && s.DoctorProfileId == doctorId, cancellationToken);
        if (schedule is null)
        {
            return new ScheduleMutationResult(ScheduleMutationStatus.NotFound);
        }

        schedule.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new ScheduleMutationResult(ScheduleMutationStatus.Success);
    }

    private static bool IsAuthorized(DoctorProfile doctor, CallerContext caller) => caller.IsAdmin || doctor.UserId == caller.UserId;

    private async Task<bool> HasOverlapAsync(Guid doctorId, DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end, Guid? excludeScheduleId, CancellationToken cancellationToken)
    {
        var existing = await _db.DoctorSchedules
            .Where(s => s.DoctorProfileId == doctorId && s.DayOfWeek == dayOfWeek && (excludeScheduleId == null || s.Id != excludeScheduleId))
            .ToListAsync(cancellationToken);

        return existing.Any(s => start < s.EndTime && s.StartTime < end);
    }

    private static DoctorScheduleDto ToDto(DoctorSchedule s) => new(s.Id, s.DayOfWeek, s.StartTime, s.EndTime, s.SlotDurationMinutes, s.BufferMinutes);
}
