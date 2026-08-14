namespace Asnan.Application.Chat;

/// <summary>
/// Fires when a message is sent to a recipient who isn't currently
/// connected to the hub — issue #28. Real push delivery is Milestone 8;
/// this issue only needs the hook to exist and reliably fire, per its own
/// description ("stubbed until Milestone 8 lands, wired fully then").
/// </summary>
public interface IOfflineMessageNotifier
{
    Task NotifyAsync(Guid recipientUserId, Guid conversationId, Guid messageId, CancellationToken cancellationToken = default);
}
