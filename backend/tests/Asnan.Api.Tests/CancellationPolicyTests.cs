using Asnan.Application.Appointments;

namespace Asnan.Api.Tests;

/// <summary>
/// Pure unit tests for <see cref="CancellationPolicy"/> (issue #24) — no
/// database involved, per the issue's testing requirement covering
/// "cancellation-policy edge cases (window boundaries)".
/// </summary>
public class CancellationPolicyTests
{
    private static readonly List<RefundTierOptions> DefaultTiers =
    [
        new RefundTierOptions { HoursBeforeAppointment = 24, RefundPercentage = 100 },
        new RefundTierOptions { HoursBeforeAppointment = 1, RefundPercentage = 50 },
    ];

    [Fact]
    public void Evaluate_WellOutsideTheFirstTier_ReturnsFullRefund()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var slotStart = now.AddHours(48);

        var result = CancellationPolicy.Evaluate(now, slotStart, DefaultTiers);

        Assert.True(result.IsAllowed);
        Assert.Equal(100, result.RefundPercentage);
    }

    [Fact]
    public void Evaluate_ExactlyAtTheFirstTierBoundary_UsesThatTier()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var slotStart = now.AddHours(24);

        var result = CancellationPolicy.Evaluate(now, slotStart, DefaultTiers);

        Assert.True(result.IsAllowed);
        Assert.Equal(100, result.RefundPercentage);
    }

    [Fact]
    public void Evaluate_JustUnderTheFirstTierBoundary_DropsToTheNextTier()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var slotStart = now.AddHours(23.99);

        var result = CancellationPolicy.Evaluate(now, slotStart, DefaultTiers);

        Assert.True(result.IsAllowed);
        Assert.Equal(50, result.RefundPercentage);
    }

    [Fact]
    public void Evaluate_ExactlyAtTheLastTierBoundary_UsesThatTier()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var slotStart = now.AddHours(1);

        var result = CancellationPolicy.Evaluate(now, slotStart, DefaultTiers);

        Assert.True(result.IsAllowed);
        Assert.Equal(50, result.RefundPercentage);
    }

    [Fact]
    public void Evaluate_JustUnderTheLastTierBoundary_IsNotAllowed()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var slotStart = now.AddMinutes(59);

        var result = CancellationPolicy.Evaluate(now, slotStart, DefaultTiers);

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void Evaluate_AfterTheAppointmentHasAlreadyStarted_IsNotAllowed()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var slotStart = now.AddHours(-1);

        var result = CancellationPolicy.Evaluate(now, slotStart, DefaultTiers);

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void Evaluate_WithNoTiersConfigured_IsNeverAllowed()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = CancellationPolicy.Evaluate(now, now.AddDays(30), []);

        Assert.False(result.IsAllowed);
    }
}
