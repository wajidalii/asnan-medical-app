namespace Asnan.Application.Specialties;

public enum SpecialtyMutationStatus
{
    Success,
    NotFound,
    DuplicateName,
}

public record SpecialtyMutationResult(SpecialtyMutationStatus Status, SpecialtyDto? Specialty = null);
