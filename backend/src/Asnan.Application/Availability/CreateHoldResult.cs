namespace Asnan.Application.Availability;

public enum CreateHoldStatus
{
    Success,
    DoctorNotFound,

    /// <summary>The requested slot doesn't match any currently-computed available slot.</summary>
    SlotNotAvailable,

    /// <summary>Lost the race to another concurrent request for the same slot — the DB unique index rejected the insert.</summary>
    Conflict,
}

public record CreateHoldResult(CreateHoldStatus Status, HoldDto? Hold = null);
