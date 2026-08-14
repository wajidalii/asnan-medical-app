namespace Asnan.Api.Hubs;

/// <summary>
/// Tracks which users currently have at least one live hub connection —
/// issue #28's "isn't connected" check for the offline-notify hook.
/// Connected-to-the-hub-at-all is used as the online signal (not
/// connected-to-this-specific-conversation-group) — a reasonable proxy that
/// avoids needing full per-group presence tracking for what's fundamentally
/// a delivery-hint, not a security decision.
/// </summary>
public interface IChatPresenceTracker
{
    void AddConnection(Guid userId, string connectionId);

    void RemoveConnection(Guid userId, string connectionId);

    bool IsOnline(Guid userId);
}
