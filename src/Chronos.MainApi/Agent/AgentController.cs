using Chronos.Agent;
using Chronos.Agent.Conversation;
using Chronos.Agent.Extraction;
using Chronos.MainApi.Agent.Contracts;
using Chronos.MainApi.Shared.Controllers.Utils;
using Chronos.MainApi.Shared.Extensions;
using Chronos.MainApi.Shared.Middleware;
using Chronos.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chronos.MainApi.Agent;

[ApiController]
[Route("api/agent")]
[RequireOrganization]
[Authorize]
public class AgentController(
    ILogger<AgentController> logger,
    AgentOrchestrator orchestrator,
    IConversationStore conversationStore)
    : ControllerBase
{
    [HttpPost("sessions")]
    public async Task<IActionResult> StartSession([FromBody] StartSessionRequest request)
    {
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        var userId = User.GetUserId();

        logger.LogInformation("Agent: starting session for user {UserId}", userId);

        var sessionId = await orchestrator.StartSessionAsync(userId, organizationId, request.SchedulingPeriodId);

        return CreatedAtAction(nameof(SendMessage), new { sessionId }, new { sessionId });
    }

    [HttpPost("sessions/{sessionId}/messages")]
    public async Task<IActionResult> SendMessage(Guid sessionId, [FromBody] SendMessageRequest request)
    {
        var userId = User.GetUserId();
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await VerifySessionOwnershipAsync(sessionId, userId, organizationId);

        logger.LogInformation("Agent: message in session {SessionId}", sessionId);

        var result = await ExecuteAgentActionAsync(() => orchestrator.SendMessageAsync(sessionId, request.Message));
        return Ok(ToResponse(sessionId, result));
    }

    [HttpPost("sessions/{sessionId}/submit")]
    public async Task<IActionResult> RequestSubmit(Guid sessionId)
    {
        var userId = User.GetUserId();
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await VerifySessionOwnershipAsync(sessionId, userId, organizationId);

        logger.LogInformation("Agent: submit requested for session {SessionId}", sessionId);

        var result = await ExecuteAgentActionAsync(() => orchestrator.RequestSubmitAsync(sessionId));
        return Ok(ToResponse(sessionId, result));
    }

    [HttpPost("sessions/{sessionId}/approve")]
    public async Task<IActionResult> Approve(Guid sessionId)
    {
        var userId = User.GetUserId();
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await VerifySessionOwnershipAsync(sessionId, userId, organizationId);

        logger.LogInformation("Agent: approval for session {SessionId}", sessionId);

        var result = await ExecuteAgentActionAsync(() => orchestrator.ApproveAsync(sessionId));
        return Ok(ToResponse(sessionId, result));
    }

    [HttpPost("sessions/{sessionId}/revise")]
    public async Task<IActionResult> Revise(Guid sessionId)
    {
        var userId = User.GetUserId();
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await VerifySessionOwnershipAsync(sessionId, userId, organizationId);

        logger.LogInformation("Agent: revision requested for session {SessionId}", sessionId);

        var result = await ExecuteAgentActionAsync(() => orchestrator.ReviseAsync(sessionId));
        return Ok(ToResponse(sessionId, result));
    }

    private async Task VerifySessionOwnershipAsync(Guid sessionId, Guid userId, Guid organizationId)
    {
        var session = await conversationStore.GetAsync(sessionId);
        if (session is null)
            throw new NotFoundException($"Session {sessionId} not found.");

        if (session.UserId != userId || session.OrganizationId != organizationId)
            throw new UnauthorizedException("You do not own this session.");
    }

    private static async Task<AgentResponse> ExecuteAgentActionAsync(Func<Task<AgentResponse>> action)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException ex)
        {
            throw new NotFoundException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new BadRequestException(ex.Message);
        }
        catch (ExtractionException ex)
        {
            throw new BadRequestException($"Constraint extraction failed: {ex.Message}");
        }
    }

    private static AgentSessionResponse ToResponse(Guid sessionId, AgentResponse result)
    {
        DraftResponse? draftResponse = null;
        if (result.Draft is not null)
        {
            draftResponse = new DraftResponse(
                result.Draft.HardConstraints.Select(c => new DraftItemResponse(c.Key, c.Value)).ToList(),
                result.Draft.SoftPreferences.Select(p => new DraftItemResponse(p.Key, p.Value)).ToList());
        }

        return new AgentSessionResponse(
            sessionId,
            result.State,
            result.AssistantMessage,
            draftResponse,
            result.AllowedActions);
    }
}
