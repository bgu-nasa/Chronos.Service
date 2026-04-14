using System.Collections.Concurrent;

namespace Chronos.Agent.Conversation;

/// <summary>
/// Thread-safe in-memory conversation store for POC usage.
/// </summary>
public class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<Guid, ConversationSession> _sessions = new();

    public Task<ConversationSession?> GetAsync(Guid sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task SaveAsync(ConversationSession session)
    {
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }
}
