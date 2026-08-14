namespace Asnan.Application.Reminders;

public interface IReminderSchedulingService
{
    /// <summary>Creates and sends any reminders that became due as of <paramref name="nowUtc"/>. Returns how many were successfully sent.</summary>
    Task<int> ScanAndSendDueRemindersAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
