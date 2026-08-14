using Asnan.Application.Chat;
using Asnan.Application.Common;
using Asnan.Application.Notifications;
using Asnan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Infrastructure.Chat;

/// <summary>
/// Real push delivery for ChatHub's offline-recipient hook — issue #31
/// (replaces the LoggingOfflineMessageNotifier stub from #28). Never
/// includes the message content, only who it's from — ARCHITECTURE.md §9's
/// "no sensitive content in notification text" rule, matching the example
/// copy ("New message from Dr. X") verbatim for a patient recipient. A
/// patient has no display name anywhere in this domain model (no
/// PatientProfile.FullName exists), so a doctor recipient gets a generic
/// "a patient" label rather than an invented one.
/// </summary>
public class NotificationOfflineMessageNotifier : IOfflineMessageNotifier
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationDispatchService _notificationDispatch;

    public NotificationOfflineMessageNotifier(IApplicationDbContext db, INotificationDispatchService notificationDispatch)
    {
        _db = db;
        _notificationDispatch = notificationDispatch;
    }

    public async Task NotifyAsync(Guid recipientUserId, Guid conversationId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var conversation = await _db.ChatConversations
            .Include(c => c.Appointment).ThenInclude(a => a.DoctorProfile)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
        if (conversation is null) return; // defensive — the hub already validated participancy before calling this

        var recipientIsTheDoctor = conversation.Appointment.DoctorProfile.UserId == recipientUserId;
        var senderLabel = recipientIsTheDoctor ? "a patient" : $"Dr. {conversation.Appointment.DoctorProfile.FullName}";

        await _notificationDispatch.DispatchAsync(
            recipientUserId,
            NotificationCategory.ChatMessages,
            new PushNotification("New message", $"New message from {senderLabel}.", $"asnan://chat/{conversationId}"),
            cancellationToken);
    }
}
