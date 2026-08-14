using Asnan.Api.Extensions;
using Asnan.Application.Chat;
using Asnan.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Api.Hubs;

/// <summary>
/// Real-time chat — ARCHITECTURE.md §9 (issues #27/#28). JWT-authenticated
/// (query-string <c>access_token</c>, see Program.cs's JwtBearerEvents —
/// browsers/SignalR clients can't set headers on the WS upgrade). Group
/// membership is checked per-join against <c>ChatParticipants</c>, not role
/// alone: a doctor and a patient are authorized identically, purely by
/// being one of the exact two rows on that specific conversation.
///
/// Message persistence/history/read-state logic itself lives in
/// <see cref="IChatService"/> (Asnan.Application, framework-agnostic,
/// independently testable) — this hub only handles the SignalR-specific
/// concerns on top: broadcasting, connection lifecycle, and presence-based
/// offline notification.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IApplicationDbContext _db;
    private readonly IChatService _chatService;
    private readonly IChatPresenceTracker _presenceTracker;
    private readonly IOfflineMessageNotifier _offlineNotifier;

    public ChatHub(IApplicationDbContext db, IChatService chatService, IChatPresenceTracker presenceTracker, IOfflineMessageNotifier offlineNotifier)
    {
        _db = db;
        _chatService = chatService;
        _presenceTracker = presenceTracker;
        _offlineNotifier = offlineNotifier;
    }

    public override Task OnConnectedAsync()
    {
        _presenceTracker.AddConnection(Context.User!.GetUserId(), Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _presenceTracker.RemoveConnection(Context.User!.GetUserId(), Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = Context.User!.GetUserId();

        var isParticipant = await _db.ChatParticipants
            .AnyAsync(p => p.ChatConversationId == conversationId && p.UserId == userId);
        if (!isParticipant)
        {
            // Deliberately generic — doesn't reveal whether the conversation
            // exists at all to a caller who isn't part of it.
            throw new HubException("You are not authorized to join this conversation.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(conversationId));
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(conversationId));
    }

    /// <summary>Persists, broadcasts to the conversation group, then fires the offline-notify hook if the recipient has no live connection.</summary>
    public async Task SendMessage(Guid conversationId, string content)
    {
        var userId = Context.User!.GetUserId();
        var result = await _chatService.SendMessageAsync(conversationId, userId, content);

        if (result.Status != SendMessageStatus.Success)
        {
            throw new HubException(result.Status switch
            {
                SendMessageStatus.InvalidContent => "Message content is required and must be 2000 characters or fewer.",
                SendMessageStatus.Forbidden or SendMessageStatus.ConversationNotFound => "You are not authorized to send messages in this conversation.",
                _ => "Could not send the message.",
            });
        }

        await Clients.Group(GroupName(conversationId)).SendAsync("ReceiveMessage", result.Message);

        if (result.RecipientUserId is { } recipientUserId && !_presenceTracker.IsOnline(recipientUserId))
        {
            await _offlineNotifier.NotifyAsync(recipientUserId, conversationId, result.Message!.Id);
        }
    }

    /// <summary>Updates the caller's read position and notifies the other participant (a live read receipt), if connected.</summary>
    public async Task MarkAsRead(Guid conversationId, Guid lastReadMessageId)
    {
        var userId = Context.User!.GetUserId();
        var result = await _chatService.MarkAsReadAsync(conversationId, userId, lastReadMessageId);

        if (result.Status != ChatConversationAccessStatus.Success)
        {
            throw new HubException("You are not authorized to update read status for this conversation.");
        }

        await Clients.OthersInGroup(GroupName(conversationId)).SendAsync("MessagesRead", userId, lastReadMessageId);
    }

    public static string GroupName(Guid conversationId) => $"conversation:{conversationId}";
}
