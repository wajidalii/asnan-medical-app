using Asnan.Api.Extensions;
using Asnan.Application.Chat;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

/// <summary>
/// Message history + read-status (issue #28) — object-level authorized to
/// the conversation's two ChatParticipants, same as the hub's join check.
/// Sending happens over the hub (ChatHub.SendMessage), not here — this is
/// the read-side REST surface.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chat/conversations")]
[Authorize]
public class ChatController : ControllerBase
{
    private const int DefaultPageSize = 20;

    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid id, [FromQuery] DateTime? before, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var effectivePageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, 100);
        var result = await _chatService.GetMessagesAsync(id, User.GetUserId(), before, effectivePageSize, cancellationToken);

        return result.Status switch
        {
            ChatConversationAccessStatus.Success => Ok(result.Page),
            ChatConversationAccessStatus.ConversationNotFound => NotFound(),
            ChatConversationAccessStatus.Forbidden => Forbid(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(ChatConversationAccessStatus)}: {result.Status}"),
        };
    }

    [HttpGet("{id:guid}/read-status")]
    public async Task<IActionResult> GetReadStatus(Guid id, CancellationToken cancellationToken)
    {
        var result = await _chatService.GetReadStatusAsync(id, User.GetUserId(), cancellationToken);

        return result.Status switch
        {
            ChatConversationAccessStatus.Success => Ok(result.ReadStatus),
            ChatConversationAccessStatus.ConversationNotFound => NotFound(),
            ChatConversationAccessStatus.Forbidden => Forbid(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(ChatConversationAccessStatus)}: {result.Status}"),
        };
    }
}
