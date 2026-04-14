namespace Chronos.Agent.Conversation;

/// <summary>
/// A single message in the conversation (system, user, or assistant).
/// </summary>
public record ChatMessage(string Role, string Content);
