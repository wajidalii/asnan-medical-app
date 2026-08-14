using System.Collections.Concurrent;

namespace Asnan.Api.Hubs;

/// <summary>
/// Singleton, in-process only — fine for a single-instance deployment; a
/// multi-instance deployment would need a shared backplane (e.g. Redis) for
/// both this and SignalR's own group routing, which is a separate, larger
/// concern than issue #28 scope.
/// </summary>
public class InMemoryChatPresenceTracker : IChatPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _connectionsByUser = new();

    public void AddConnection(Guid userId, string connectionId)
    {
        var connections = _connectionsByUser.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
        connections[connectionId] = 0;
    }

    public void RemoveConnection(Guid userId, string connectionId)
    {
        if (_connectionsByUser.TryGetValue(userId, out var connections))
        {
            connections.TryRemove(connectionId, out _);
            if (connections.IsEmpty)
            {
                _connectionsByUser.TryRemove(userId, out _);
            }
        }
    }

    public bool IsOnline(Guid userId) => _connectionsByUser.TryGetValue(userId, out var connections) && !connections.IsEmpty;
}
