using Asnan.Application.Specialties;

namespace Asnan.Application.Doctors;

/// <summary>
/// Public doctor profile detail — like <see cref="DoctorListItemDto"/>,
/// excludes internal-only fields (<c>userId</c>, <c>timeZoneId</c>).
///
/// Deliberately has no "upcoming available dates" field: that requires the
/// availability model (DoctorSchedules / DoctorAvailabilityExceptions,
/// ARCHITECTURE.md §6), which is Milestone 4 and doesn't exist yet. Flagged
/// rather than faked — see the PR for this issue.
/// </summary>
public record DoctorDetailDto(
    Guid Id,
    string FullName,
    string? Bio,
    string? Qualifications,
    List<SpecialtyDto> Specialties,
    int? YearsOfExperience,
    decimal ConsultationFee,
    string Currency,
    string? ClinicAddress,
    int AppointmentDurationMinutes,
    bool IsAcceptingNewPatients);
