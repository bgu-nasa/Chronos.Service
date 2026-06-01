using System.Net;
using Chronos.Agent.Conversation;
using Chronos.MainApi.Agent.Contracts;
using Chronos.MainApi.Schedule.Contracts;
using Chronos.Tests.Acceptance.Infrastructure;
using Chronos.Tests.Acceptance.Support;
using FluentAssertions;

namespace Chronos.Tests.Acceptance.Flows.Agent;

[TestFixture]
[Category("Acceptance")]
public class AgentSubmissionTests
{
    private const string AgentBase = "/api/agent";
    private const string ConstraintsBase = "/api/schedule/constraints";

    [Test]
    public async Task GivenSchedulingPreferences_WhenAgentDraftIsApproved_ThenConstraintsAndPreferencesArePersisted()
    {
        using var ctx = await AcceptanceContext.CreateAsync("Agent Acceptance Org");
        var from = DateTime.UtcNow.Date.AddDays(30);
        var period = await ctx.Seed.CreateSchedulingPeriodAsync("Agent Term", from, from.AddMonths(4));
        var periodId = Guid.Parse(period.Id);

        var start = await ctx.AdminClient.PostJsonAsync(
            $"{AgentBase}/sessions",
            new StartSessionRequest(periodId, "Asia/Jerusalem"));

        start.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = await start.ReadJsonAsync<CreatedAgentSession>()
                      ?? throw new InvalidOperationException("Agent session creation returned no session id.");

        var message = await ctx.AdminClient.PostJsonAsync(
            $"{AgentBase}/sessions/{session.SessionId}/messages",
            new SendMessageRequest("I cannot teach Fridays and prefer Mondays."));

        message.StatusCode.Should().Be(HttpStatusCode.OK);
        var conversation = await message.ReadJsonAsync<AgentSessionEnvelope>();
        conversation.Should().NotBeNull();
        conversation!.State.Should().Be(AgentState.Discovery);
        conversation.AssistantMessage.Should().NotBeNullOrWhiteSpace();

        var submit = await ctx.AdminClient.PostJsonAsync(
            $"{AgentBase}/sessions/{session.SessionId}/submit",
            new { });

        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        var draft = await submit.ReadJsonAsync<AgentSessionEnvelope>();
        draft.Should().NotBeNull();
        draft!.State.Should().Be(AgentState.Submit);
        draft.AllowedActions.Should().BeEquivalentTo(new[] { AgentAction.Approve, AgentAction.Revise });
        draft.ValidationIssues.Should().BeEmpty();
        draft.Draft.Should().NotBeNull();
        draft.Draft!.HardConstraints.Should().ContainSingle(c =>
            c.Key == "forbidden_timerange" && c.Value == "Friday 09:00 - 17:00");
        draft.Draft.SoftPreferences.Should().ContainSingle(p =>
            p.Key == "preferred_weekday" && p.Value == "Monday");

        var approve = await ctx.AdminClient.PostJsonAsync(
            $"{AgentBase}/sessions/{session.SessionId}/approve",
            new { });

        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await approve.ReadJsonAsync<AgentSessionEnvelope>();
        approved.Should().NotBeNull();
        approved!.State.Should().Be(AgentState.Approved);
        approved.AllowedActions.Should().BeEmpty();

        var persistedConstraints = await ReadArrayAsync<UserConstraintResponse>(
            ctx.AdminClient,
            $"{ConstraintsBase}/userConstraint/by-period-and-user/{periodId}/{ctx.AdminUserId}");
        persistedConstraints.Should().ContainSingle(c =>
            c.Key == "forbidden_timerange"
            && c.Value == "Friday 09:00 - 17:00"
            && c.UserId == ctx.AdminUserId.ToString()
            && c.SchedulingPeriodId == periodId.ToString());

        var persistedPreferences = await ReadArrayAsync<UserPreferenceResponse>(
            ctx.AdminClient,
            $"{ConstraintsBase}/preferenceConstraint/by-period-and-user/{periodId}/{ctx.AdminUserId}");
        persistedPreferences.Should().ContainSingle(p =>
            p.Key == "preferred_weekday"
            && p.Value == "Monday"
            && p.UserId == ctx.AdminUserId.ToString()
            && p.SchedulingPeriodId == periodId.ToString());
    }

    private static async Task<T[]> ReadArrayAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadJsonAsync<T[]>()
               ?? throw new InvalidOperationException($"GET {url} returned no response body.");
    }

    private sealed record CreatedAgentSession(Guid SessionId);
    private sealed record AgentSessionEnvelope(
        Guid SessionId,
        AgentState State,
        string? AssistantMessage,
        DraftEnvelope? Draft,
        List<AgentAction> AllowedActions,
        List<ValidationIssueEnvelope> ValidationIssues);

    private sealed record DraftEnvelope(
        List<DraftItemEnvelope> HardConstraints,
        List<DraftItemEnvelope> SoftPreferences);

    private sealed record DraftItemEnvelope(string Key, string Value);
    private sealed record ValidationIssueEnvelope(string Kind, string Key, string Value, string Reason);
}
