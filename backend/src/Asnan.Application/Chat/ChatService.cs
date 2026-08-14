using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Chat;

public class ChatService : IChatService
{
    private readonly IApplicationDbContext _db;

    public ChatService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SendMessageResult> SendMessageAsync(Guid conversationId, Guid senderUserId, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 2000)
        {
            return new SendMessageResult(SendMessageStatus.InvalidContent);
        }

        var (participants, notFound, forbidden) = await AuthorizeAsync(conversationId, senderUserId, cancellationToken);
        if (notFound) return new SendMessageResult(SendMessageStatus.ConversationNotFound);
        if (forbidden) return new SendMessageResult(SendMessageStatus.Forbidden);

        var message = new ChatMessage
        {
            ChatConversationId = conversationId,
            SenderUserId = senderUserId,
            Content = content,
            SentAtUtc = DateTime.UtcNow,
        };
        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        var recipientUserId = participants!.First(p => p.UserId != senderUserId).UserId;

        return new SendMessageResult(SendMessageStatus.Success, ToDto(message), recipientUserId);
    }

    public async Task<GetMessagesResult> GetMessagesAsync(Guid conversationId, Guid callerId, DateTime? before, int pageSize, CancellationToken cancellationToken = default)
    {
        var (_, notFound, forbidden) = await AuthorizeAsync(conversationId, callerId, cancellationToken);
        if (notFound) return new GetMessagesResult(ChatConversationAccessStatus.ConversationNotFound);
        if (forbidden) return new GetMessagesResult(ChatConversationAccessStatus.Forbidden);

        var query = _db.ChatMessages.Where(m => m.ChatConversationId == conversationId);
        if (before.HasValue)
        {
            query = query.Where(m => m.SentAtUtc < before.Value);
        }

        var page = await query
            .OrderByDescending(m => m.SentAtUtc)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = page.Count > pageSize;
        var items = page.Take(pageSize).ToList();
        var nextCursor = items.Count > 0 ? items[^1].SentAtUtc : (DateTime?)null;

        items.Reverse(); // oldest-first for display
        var dtoPage = new ChatMessagePageDto(items.Select(ToDto).ToList(), hasMore ? nextCursor : null, hasMore);

        return new GetMessagesResult(ChatConversationAccessStatus.Success, dtoPage);
    }

    public async Task<MarkAsReadResult> MarkAsReadAsync(Guid conversationId, Guid callerId, Guid lastReadMessageId, CancellationToken cancellationToken = default)
    {
        var (participants, notFound, forbidden) = await AuthorizeAsync(conversationId, callerId, cancellationToken);
        if (notFound) return new MarkAsReadResult(ChatConversationAccessStatus.ConversationNotFound);
        if (forbidden) return new MarkAsReadResult(ChatConversationAccessStatus.Forbidden);

        var now = DateTime.UtcNow;
        var readStatus = await _db.MessageReadStatuses
            .FirstOrDefaultAsync(r => r.ChatConversationId == conversationId && r.UserId == callerId, cancellationToken);

        if (readStatus is null)
        {
            _db.MessageReadStatuses.Add(new MessageReadStatus
            {
                ChatConversationId = conversationId,
                UserId = callerId,
                LastReadMessageId = lastReadMessageId,
                LastReadAtUtc = now,
            });
        }
        else
        {
            readStatus.LastReadMessageId = lastReadMessageId;
            readStatus.LastReadAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var recipientUserId = participants!.First(p => p.UserId != callerId).UserId;
        return new MarkAsReadResult(ChatConversationAccessStatus.Success, recipientUserId);
    }

    public async Task<GetReadStatusResult> GetReadStatusAsync(Guid conversationId, Guid callerId, CancellationToken cancellationToken = default)
    {
        var (_, notFound, forbidden) = await AuthorizeAsync(conversationId, callerId, cancellationToken);
        if (notFound) return new GetReadStatusResult(ChatConversationAccessStatus.ConversationNotFound);
        if (forbidden) return new GetReadStatusResult(ChatConversationAccessStatus.Forbidden);

        var readStatus = await _db.MessageReadStatuses
            .FirstOrDefaultAsync(r => r.ChatConversationId == conversationId && r.UserId == callerId, cancellationToken);

        var unreadCount = await _db.ChatMessages
            .Where(m => m.ChatConversationId == conversationId && m.SenderUserId != callerId)
            .Where(m => readStatus == null || m.SentAtUtc > readStatus.LastReadAtUtc)
            .CountAsync(cancellationToken);

        var dto = new ReadStatusDto(conversationId, readStatus?.LastReadMessageId, readStatus?.LastReadAtUtc, unreadCount);
        return new GetReadStatusResult(ChatConversationAccessStatus.Success, dto);
    }

    private async Task<(List<ChatParticipant>? Participants, bool NotFound, bool Forbidden)> AuthorizeAsync(Guid conversationId, Guid callerId, CancellationToken cancellationToken)
    {
        var conversationExists = await _db.ChatConversations.AnyAsync(c => c.Id == conversationId, cancellationToken);
        if (!conversationExists)
        {
            return (null, true, false);
        }

        var participants = await _db.ChatParticipants.Where(p => p.ChatConversationId == conversationId).ToListAsync(cancellationToken);
        if (participants.All(p => p.UserId != callerId))
        {
            return (null, false, true);
        }

        return (participants, false, false);
    }

    private static ChatMessageDto ToDto(ChatMessage message) =>
        new(message.Id, message.ChatConversationId, message.SenderUserId, message.Content, message.SentAtUtc);
}
