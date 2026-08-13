using Asnan.Domain.Common;

namespace Asnan.Domain.Entities;

/// <summary>
/// Recurring weekly availability template — ARCHITECTURE.md §6. Times are in
/// the doctor's own timezone (<see cref="DoctorProfile.TimeZoneId"/>), not UTC.
/// </summary>
public class DoctorSchedule : BaseEntity
{
    public Guid DoctorProfileId { get; set; }

    public DoctorProfile DoctorProfile { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int SlotDurationMinutes { get; set; }

    public int BufferMinutes { get; set; }
}
