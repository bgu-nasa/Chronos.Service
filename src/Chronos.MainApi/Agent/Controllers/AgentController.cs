using Chronos.MainApi.Agent.Contracts;
using Chronos.MainApi.Agent.Services;
using Chronos.MainApi.Shared.Controllers.Utils;
using Chronos.MainApi.Shared.Extensions;
using Chronos.MainApi.Shared.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chronos.MainApi.Agent.Controllers;

[ApiController]
[Authorize]
[RequireOrganization]
[Route("api/agent")]
public sealed class AgentController(
    ILogger<AgentController> logger,
    IAgentService agentService) : ControllerBase
{
    [HttpPost("session/start")]
    public async Task<IActionResult> StartSession([FromBody] StartAgentSessionRequest request, CancellationToken cancellationToken)
    {
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        var userId = HttpContext.User.GetUserId();

        var response = await agentService.StartSessionAsync(organizationId, userId, request.SchedulingPeriodId, cancellationToken);
        return Ok(response);
    }

    [HttpPost("session/message")]
    public async Task<IActionResult> SendMessage([FromBody] SendAgentMessageRequest request, CancellationToken cancellationToken)
    {
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        var userId = HttpContext.User.GetUserId();

        var response = await agentService.SendMessageAsync(
            organizationId,
            userId,
            Guid.Parse(request.SessionId),
            request.Message,
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("session/revise")]
    public async Task<IActionResult> Revise([FromBody] SendAgentMessageRequest request, CancellationToken cancellationToken)
    {
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        var userId = HttpContext.User.GetUserId();

        var response = await agentService.ReviseAsync(
            organizationId,
            userId,
            Guid.Parse(request.SessionId),
            request.Message,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("session/{sessionId}")]
    public async Task<IActionResult> GetSession([FromRoute] Guid sessionId, CancellationToken cancellationToken)
    {
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        var userId = HttpContext.User.GetUserId();

        var response = await agentService.GetSessionAsync(organizationId, userId, sessionId, cancellationToken);
        return Ok(response);
    }

    [HttpPost("session/approve")]
    public async Task<IActionResult> Approve([FromBody] ApproveAgentSessionRequest request, CancellationToken cancellationToken)
    {
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);
        var userId = HttpContext.User.GetUserId();

        var response = await agentService.ApproveAsync(
            organizationId,
            userId,
            Guid.Parse(request.SessionId),
            cancellationToken);

        return Ok(response);
    }
}
