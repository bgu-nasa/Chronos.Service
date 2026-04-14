namespace Chronos.Agent.Conversation;

public record ChatMessage(string Role, string Content, DateTime CreatedAtUtc);
