namespace Asnan.Domain.Enums;

public enum AvailabilityExceptionType
{
    /// <summary>Holiday/time off — whole day when Start/EndTime are both null, or a partial-day block when set.</summary>
    Unavailable = 1,

    /// <summary>Exceptional extra hours outside the normal weekly template. Start/EndTime are required.</summary>
    ExtraAvailability = 2,
}
