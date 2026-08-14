using FluentValidation;

namespace Asnan.Application.Chat;

public record ChatMessageDto(Guid Id, Guid ChatConversationId, Guid SenderUserId, string Content, DateTime SentAtUtc);

public record ChatMessagePageDto(List<ChatMessageDto> Messages, DateTime? NextBeforeCursor, bool HasMore);

public record ReadStatusDto(Guid ChatConversationId, Guid? LastReadMessageId, DateTime? LastReadAtUtc, int UnreadCount);

/// <summary>Shared by every operation that only needs "is the caller a participant on this conversation" — GetMessages/MarkAsRead/GetReadStatus.</summary>
public enum ChatConversationAccessStatus
{
    Success,
    ConversationNotFound,

    /// <summary>Conversation exists, but the caller isn't one of its two ChatParticipants.</summary>
    Forbidden,
}

public enum SendMessageStatus
{
    Success,
    ConversationNotFound,
    Forbidden,
    InvalidContent,
}

public record SendChatMessageDto(string Content);

public class SendChatMessageDtoValidator : AbstractValidator<SendChatMessageDto>
{
    public SendChatMessageDtoValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
    }
}

public record SendMessageResult(SendMessageStatus Status, ChatMessageDto? Message = null, Guid? RecipientUserId = null);

public record GetMessagesResult(ChatConversationAccessStatus Status, ChatMessagePageDto? Page = null);

public record MarkAsReadResult(ChatConversationAccessStatus Status, Guid? RecipientUserId = null);

public record GetReadStatusResult(ChatConversationAccessStatus Status, ReadStatusDto? ReadStatus = null);
