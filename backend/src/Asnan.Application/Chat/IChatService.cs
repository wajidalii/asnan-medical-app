namespace Asnan.Application.Chat;

/// <summary>
/// Chat message persistence/history/read-state (issue #28) — the
/// SignalR-specific concerns (broadcasting, presence, the offline-notify
/// decision) stay in ChatHub (Asnan.Api); this service only ever persists
/// and reads, so it's testable without a hub connection.
/// </summary>
public interface IChatService
{
    /// <summary>Persists the message ("durable-first") — the caller (ChatHub) broadcasts it afterward.</summary>
    Task<SendMessageResult> SendMessageAsync(Guid conversationId, Guid senderUserId, string content, CancellationToken cancellationToken = default);

    /// <summary>Cursor-paginated, newest-first internally but returned oldest-first for display. <paramref name="before"/> null fetches the most recent page.</summary>
    Task<GetMessagesResult> GetMessagesAsync(Guid conversationId, Guid callerId, DateTime? before, int pageSize, CancellationToken cancellationToken = default);

    Task<MarkAsReadResult> MarkAsReadAsync(Guid conversationId, Guid callerId, Guid lastReadMessageId, CancellationToken cancellationToken = default);

    Task<GetReadStatusResult> GetReadStatusAsync(Guid conversationId, Guid callerId, CancellationToken cancellationToken = default);
}
