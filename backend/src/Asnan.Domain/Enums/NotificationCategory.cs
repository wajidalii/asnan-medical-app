namespace Asnan.Domain.Enums;

public enum NotificationCategory
{
    /// <summary>Scheduled/cancelled/starting-soon. Transactional — non-disable-able (ARCHITECTURE.md §10).</summary>
    AppointmentUpdates = 1,

    /// <summary>Payment succeeded/failed, refund completed. Transactional/security-adjacent — non-disable-able.</summary>
    PaymentUpdates = 2,

    /// <summary>Appointment-reminder pushes (Milestone 6's ReminderSchedulingService). Disableable.</summary>
    Reminders = 3,

    /// <summary>New chat message while the recipient is offline. Disableable.</summary>
    ChatMessages = 4,

    /// <summary>A doctor's availability changed. Disableable.</summary>
    DoctorAvailability = 5,
}
