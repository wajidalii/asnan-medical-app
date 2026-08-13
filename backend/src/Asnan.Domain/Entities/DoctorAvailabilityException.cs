using Asnan.Domain.Common;
using Asnan.Domain.Enums;

namespace Asnan.Domain.Entities;

/// <summary>
/// Date-specific override to the recurring <see cref="DoctorSchedule"/>
/// template — holiday/time-off or exceptional extra hours. ARCHITECTURE.md §6.
/// </summary>
public class DoctorAvailabilityException : BaseEntity
{
    public Guid DoctorProfileId { get; set; }

    public DoctorProfile DoctorProfile { get; set; } = null!;

    public DateOnly Date { get; set; }

    public AvailabilityExceptionType Type { get; set; }

    /// <summary>Null for a whole-day <see cref="AvailabilityExceptionType.Unavailable"/> block; required otherwise.</summary>
    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string? Reason { get; set; }
}
