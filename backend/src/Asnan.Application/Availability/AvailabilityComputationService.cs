using Asnan.Application.Common;
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

        // Does not yet subtract booked Appointments / active AppointmentHolds
        // — see AvailabilitySlotCalculator's doc comment for why.
        var slots = AvailabilitySlotCalculator.Compute(date, doctor.TimeZoneId, doctor.AppointmentDurationMinutes, schedules, exceptions);

        return new DoctorAvailabilityResult(DoctorAvailabilityStatus.Success, new DoctorAvailabilityDto(doctorId, doctor.TimeZoneId, date, slots));
    }
}
