using System.Collections.Concurrent;

namespace Chronos.Agent.Conversation;

public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<Guid, ConversationSession> _sessions = new();

    public Task<ConversationSession> CreateAsync(ConversationSession session, CancellationToken cancellationToken = default)
    {
        _sessions[session.Id] = session;
        return Task.FromResult(session);
    }

    public Task<ConversationSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task SaveAsync(ConversationSession session, CancellationToken cancellationToken = default)
    {
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }
}
