namespace Asnan.Application.Appointments;

/// <summary>
/// Configurable cancellation-window/refund-percentage policy — ARCHITECTURE.md
/// "Appointment Cancellation": "make the rules configurable rather than
/// hard-coded". Tiers are evaluated by <see cref="CancellationPolicy"/>.
/// </summary>
public class CancellationPolicyOptions
{
    public const string SectionName = "CancellationPolicy";

    /// <summary>
    /// Ordered by how far in advance of the appointment a cancellation
    /// happens; the tier with the largest <see cref="RefundTierOptions.HoursBeforeAppointment"/>
    /// not exceeding the actual hours-before-appointment applies. Cancelling
    /// closer than every configured tier's threshold is not allowed at all.
    /// Defaults: 24h+ out = full refund, 1h+ out = half refund, under 1h = no cancellation.
    /// </summary>
    public List<RefundTierOptions> RefundTiers { get; set; } =
    [
        new RefundTierOptions { HoursBeforeAppointment = 24, RefundPercentage = 100 },
        new RefundTierOptions { HoursBeforeAppointment = 1, RefundPercentage = 50 },
    ];
}

public class RefundTierOptions
{
    public double HoursBeforeAppointment { get; set; }

    public int RefundPercentage { get; set; }
}
