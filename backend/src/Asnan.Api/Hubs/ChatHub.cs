using Asnan.Api.Extensions;
using Asnan.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Api.Hubs;

/// <summary>
/// Real-time chat — ARCHITECTURE.md §9 (issue #27). JWT-authenticated
/// (query-string <c>access_token</c>, see Program.cs's JwtBearerEvents —
/// browsers/SignalR clients can't set headers on the WS upgrade). Group
/// membership is checked per-join against <c>ChatParticipants</c>, not role
/// alone: a doctor and a patient are authorized identically, purely by
/// being one of the exact two rows on that specific conversation. Message
/// send/receive/history/read-receipts are issue #28's scope, not this one.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IApplicationDbContext _db;

    public ChatHub(IApplicationDbContext db)
    {
        _db = db;
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

    public static string GroupName(Guid conversationId) => $"conversation:{conversationId}";
}
