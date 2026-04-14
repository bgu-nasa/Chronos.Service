namespace Chronos.Agent.Conversation;

public interface IConversationStore
{
    Task<ConversationSession> CreateAsync(ConversationSession session, CancellationToken cancellationToken = default);
    Task<ConversationSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task SaveAsync(ConversationSession session, CancellationToken cancellationToken = default);
}
