using Asnan.Application.Common;
using Asnan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Availability;

public class AvailabilityComputationService : IAvailabilityComputationService
{
    private readonly IApplicationDbContext _db;

    public AvailabilityComputationService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DoctorAvailabilityResult> GetAvailabilityAsync(Guid doctorId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == doctorId, cancellationToken);
        if (doctor is null)
        {
            return new DoctorAvailabilityResult(DoctorAvailabilityStatus.DoctorNotFound);
        }

        var schedules = await _db.DoctorSchedules
            .Where(s => s.DoctorProfileId == doctorId && s.DayOfWeek == date.DayOfWeek)
            .ToListAsync(cancellationToken);

        var exceptions = await _db.DoctorAvailabilityExceptions
            .Where(e => e.DoctorProfileId == doctorId && e.Date == date)
            .ToListAsync(cancellationToken);

        var slots = AvailabilitySlotCalculator.Compute(date, doctor.TimeZoneId, doctor.AppointmentDurationMinutes, schedules, exceptions);

        // Subtract active, unexpired holds — closes the gap flagged when this
        // method was first written (issue #16), before AppointmentHolds
        // existed. Booked Appointments still can't be subtracted; that table
        // doesn't exist until Milestone 6.
        var now = DateTime.UtcNow;
        var heldSlotStarts = await _db.AppointmentHolds
            .Where(h => h.DoctorProfileId == doctorId && h.Status == HoldStatus.Active && h.ExpiresAtUtc > now)
            .Select(h => h.SlotStartUtc)
            .ToListAsync(cancellationToken);

        var available = heldSlotStarts.Count == 0
            ? slots
            : slots.Where(s => !heldSlotStarts.Contains(s.StartUtc)).ToList();

        return new DoctorAvailabilityResult(DoctorAvailabilityStatus.Success, new DoctorAvailabilityDto(doctorId, doctor.TimeZoneId, date, available));
    }
}
