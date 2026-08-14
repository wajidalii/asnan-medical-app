namespace Asnan.Domain.Enums;

public enum ReminderStatus
{
    /// <summary>Created because it became due, not yet successfully delivered — retried on the next scan.</summary>
    Pending = 1,

    Sent = 2,
}
