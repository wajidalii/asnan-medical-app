namespace Asnan.Application.Reminders;

/// <summary>
/// Configurable reminder offsets — ARCHITECTURE.md/prompt "Appointment
/// Reminders": "do not assume these exact values are final". Defaults are
/// 24h/1h/15min before the appointment.
/// </summary>
public class ReminderOptions
{
    public const string SectionName = "Reminders";

    public List<int> OffsetsMinutes { get; set; } = [24 * 60, 60, 15];

    /// <summary>How often the background scan runs.</summary>
    public int ScanIntervalSeconds { get; set; } = 60;
}
