namespace Chronos.Agent.Conversation;

/// <summary>
/// Persistence abstraction for conversation sessions. 
/// In-memory implementation for POC; can be swapped for durable storage later.
/// </summary>
public interface IConversationStore
{
    Task<ConversationSession?> GetAsync(Guid sessionId);
    Task SaveAsync(ConversationSession session);
    Task DeleteAsync(Guid sessionId);
}
