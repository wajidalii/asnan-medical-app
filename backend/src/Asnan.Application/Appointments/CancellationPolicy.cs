namespace Asnan.Application.Appointments;

public record CancellationPolicyResult(bool IsAllowed, int RefundPercentage);

/// <summary>Pure evaluation of <see cref="CancellationPolicyOptions"/> against a specific appointment/cancellation-time pair — no I/O, trivially unit-testable.</summary>
public static class CancellationPolicy
{
    public static CancellationPolicyResult Evaluate(DateTime nowUtc, DateTime slotStartUtc, IReadOnlyList<RefundTierOptions> tiers)
    {
        var hoursBeforeAppointment = (slotStartUtc - nowUtc).TotalHours;

        var applicableTier = tiers
            .OrderByDescending(t => t.HoursBeforeAppointment)
            .FirstOrDefault(t => hoursBeforeAppointment >= t.HoursBeforeAppointment);

        return applicableTier is null
            ? new CancellationPolicyResult(false, 0)
            : new CancellationPolicyResult(true, applicableTier.RefundPercentage);
    }
}
