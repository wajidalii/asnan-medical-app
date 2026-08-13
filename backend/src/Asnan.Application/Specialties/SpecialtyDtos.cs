using FluentValidation;

namespace Asnan.Application.Specialties;

public record SpecialtyDto(Guid Id, string Name, string? Description);

public record CreateSpecialtyDto(string Name, string? Description);

public record UpdateSpecialtyDto(string Name, string? Description);

public class CreateSpecialtyDtoValidator : AbstractValidator<CreateSpecialtyDto>
{
    public CreateSpecialtyDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024);
    }
}

public class UpdateSpecialtyDtoValidator : AbstractValidator<UpdateSpecialtyDto>
{
    public UpdateSpecialtyDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024);
    }
}
