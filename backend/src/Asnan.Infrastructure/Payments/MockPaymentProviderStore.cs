using System.Collections.Concurrent;

namespace Asnan.Infrastructure.Payments;

/// <summary>Singleton in-memory session store backing <see cref="MockPaymentProvider"/> — see MockPaymentSession's doc comment for why in-memory is sufficient here.</summary>
public class MockPaymentProviderStore
{
    private readonly ConcurrentDictionary<string, MockPaymentSession> _sessions = new();

    public void Add(MockPaymentSession session) => _sessions[session.ProviderSessionId] = session;

    public MockPaymentSession? Find(string providerSessionId) =>
        _sessions.TryGetValue(providerSessionId, out var session) ? session : null;
}
