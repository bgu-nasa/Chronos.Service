namespace Chronos.MainApi.Agent.Contracts;

public record SendAgentMessageRequest(string SessionId, string Message);
