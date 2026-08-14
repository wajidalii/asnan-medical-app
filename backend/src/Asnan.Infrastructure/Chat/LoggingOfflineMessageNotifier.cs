using Asnan.Application.Chat;
using Microsoft.Extensions.Logging;

namespace Asnan.Infrastructure.Chat;

/// <summary>
/// Stub — logs instead of pushing (real push delivery is Milestone 8),
/// same pattern as LoggingReminderSender. Never logs message content, only ids.
/// </summary>
public class LoggingOfflineMessageNotifier : IOfflineMessageNotifier
{
    private readonly ILogger<LoggingOfflineMessageNotifier> _logger;

    public LoggingOfflineMessageNotifier(ILogger<LoggingOfflineMessageNotifier> logger)
    {
        _logger = logger;
    }

    public Task NotifyAsync(Guid recipientUserId, Guid conversationId, Guid messageId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Chat message {MessageId} in conversation {ConversationId} delivered to an offline recipient {RecipientUserId} — no real push provider configured yet (Milestone 8).",
            messageId,
            conversationId,
            recipientUserId);
        return Task.CompletedTask;
    }
}
