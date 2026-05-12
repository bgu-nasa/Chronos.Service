namespace Chronos.MainApi.Agent.Contracts;

/// <param name="Timezone">
/// IANA timezone name of the invoking client (e.g. "Asia/Jerusalem", "America/New_York").
/// Used so the agent can resolve relative phrases like "tomorrow" or "next Tuesday"
/// against the user's local calendar instead of UTC. Optional — falls back to UTC
/// when omitted or unrecognised.
/// </param>
public record StartSessionRequest(Guid SchedulingPeriodId, string? Timezone = null);

public record SendMessageRequest(string Message);
