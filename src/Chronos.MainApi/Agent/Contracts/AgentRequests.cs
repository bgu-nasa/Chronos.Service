namespace Chronos.MainApi.Agent.Contracts;

public record StartSessionRequest(Guid SchedulingPeriodId);

public record SendMessageRequest(string Message);
