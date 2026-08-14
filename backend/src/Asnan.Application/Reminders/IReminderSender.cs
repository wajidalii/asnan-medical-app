using Asnan.Domain.Entities;

namespace Asnan.Application.Reminders;

/// <summary>
/// Actual delivery is a Milestone 8 (Push Notifications) concern — this
/// issue lands the scheduling/dedup logic against a stubbed sender first,
/// per the issue's own description. Swappable the same way IOtpSender/
/// IPaymentProvider are.
/// </summary>
public interface IReminderSender
{
    Task SendAsync(Appointment appointment, int offsetMinutes, CancellationToken cancellationToken = default);
}
