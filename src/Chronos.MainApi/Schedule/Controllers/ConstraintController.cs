using Chronos.MainApi.Schedule.Contracts;
using Chronos.MainApi.Schedule.Extensions;
using Chronos.MainApi.Schedule.Services;
using Chronos.MainApi.Shared.Controllers.Utils;
using Chronos.MainApi.Shared.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chronos.MainApi.Schedule.Controllers;

[ApiController]
[RequireOrganization]
[Route("api/schedule/constraints")]
public class ConstraintController(
    ILogger<ConstraintController> logger,
    IActivityConstraintService activityConstraintService,
    IUserConstraintService userConstraintService,
    IUserPreferenceService userPreferenceService,
    IOrganizationPolicyService organizationPolicyService)
    : ControllerBase
{
    private const string ViewerPolicy = "OrgRole:Viewer";
    private const string OperatorPolicy = "OrgRole:Operator";
    private const string ResourceManagerPolicy = "OrgRole:ResourceManager";

    [Authorize(Policy = OperatorPolicy)]
    [HttpPost("activityConstraint")]
    public async Task<IActionResult> CreateActivityConstraint([FromBody] CreateActivityConstraintRequest request)
    {
        logger.LogInformation("Create activity constraint endpoint was called");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var constraintId = await activityConstraintService.CreateActivityConstraintAsync(
            organizationId,
            request.ActivityId,
            request.Key,
            request.Value,
            request.WeekNum);

        var constraint = await activityConstraintService.GetActivityConstraintByIdAsync(organizationId, constraintId);
        var response = constraint.ToActivityConstraintResponse();

        return CreatedAtAction(nameof(GetActivityConstraint), new { activityConstraintId = constraintId }, response);
    }

    [Authorize(Policy = ViewerPolicy)]
    [HttpGet("activityConstraint/{activityConstraintId}")]
    public async Task<IActionResult> GetActivityConstraint(Guid activityConstraintId)
    {
        logger.LogInformation("Get activity constraint endpoint was called for {ConstraintId}", activityConstraintId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var constraint = await activityConstraintService.GetActivityConstraintByIdAsync(organizationId, activityConstraintId);
        if (constraint == null)
            return NotFound();

        return Ok(constraint.ToActivityConstraintResponse());
    }

    [Authorize(Policy = ViewerPolicy)]
    [HttpGet("activityConstraint")]
    public async Task<IActionResult> GetAllActivityConstraints()
    {
        logger.LogInformation("Get all activity constraints endpoint was called");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var constraints = await activityConstraintService.GetAllActivityConstraintsAsync(organizationId);
        var responses = constraints.Select(c => c.ToActivityConstraintResponse()).ToList();

        return Ok(responses);
    }

    [Authorize(Policy = ViewerPolicy)]
    [HttpGet("activityConstraint/by-activity/{activityId}")]
    public async Task<IActionResult> GetActivityConstraintsByActivity(Guid activityId)
    {
        logger.LogInformation("Get activity constraints by activity endpoint was called for {ActivityId}", activityId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var constraints = await activityConstraintService.GetByActivityIdAsync(organizationId, activityId);
        var responses = constraints.Select(c => c.ToActivityConstraintResponse()).ToList();

        return Ok(responses);
    }

    [Authorize (Policy = OperatorPolicy)]
    [HttpPatch("activityConstraint/{activityConstraintId}")]
    public async Task<IActionResult> UpdateActivityConstraint(
        Guid activityConstraintId,
        [FromBody] UpdateActivityConstraintRequest request)
    {
        logger.LogInformation("Update activity constraint endpoint was called for {ConstraintId}", activityConstraintId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await activityConstraintService.UpdateActivityConstraintAsync(
            organizationId,
            activityConstraintId,
            request.Key,
            request.Value,
            request.WeekNum);

        return NoContent();
    }

    [Authorize (Policy = OperatorPolicy)]
    [HttpDelete("activityConstraint/{activityConstraintId}")]
    public async Task<IActionResult> DeleteActivityConstraint(Guid activityConstraintId)
    {
        logger.LogInformation("Delete activity constraint endpoint was called for {ConstraintId}", activityConstraintId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await activityConstraintService.DeleteActivityConstraintAsync(organizationId, activityConstraintId);

        return NoContent();
    }


    [Authorize (Policy = OperatorPolicy)]
    [HttpPost("userConstraint")]
    public async Task<IActionResult> CreateUserConstraint([FromBody] CreateUserConstraintRequest request)
    {
        logger.LogInformation("Create user constraint endpoint was called");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var constraintId = await userConstraintService.CreateUserConstraintAsync(
            organizationId,
            request.UserId,
            request.SchedulingPeriodId,
            request.Key,
            request.Value,
            request.WeekNum);

        var constraint = await userConstraintService.GetUserConstraintByIdAsync(organizationId, constraintId);
        var response = constraint.ToUserConstraintResponse();

        return CreatedAtAction(nameof(GetUserConstraint), new { userConstraintId = constraintId }, response);
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("userConstraint/{userConstraintId}")]
    public async Task<IActionResult> GetUserConstraint(Guid userConstraintId)
    {
        logger.LogInformation("Get user constraint endpoint was called for {ConstraintId}", userConstraintId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var constraint = await userConstraintService.GetUserConstraintByIdAsync(organizationId, userConstraintId);
        if (constraint == null)
            return NotFound();

        return Ok(constraint.ToUserConstraintResponse());
    }

    [Authorize(Policy = ViewerPolicy)]
    [HttpGet("userConstraint")]
    public async Task<IActionResult> GetAllUserConstraints()
    {
        logger.LogInformation("Get all user constraints endpoint was called");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var constraints = await userConstraintService.GetAllUserConstraintsAsync(organizationId);
        var responses = constraints.Select(c => c.ToUserConstraintResponse()).ToList();

        return Ok(responses);
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("userConstraint/by-user/{userId}")]
    public async Task<IActionResult> GetUserConstraintsByUser(Guid userId)
    {
        logger.LogInformation("Get user constraints by user endpoint was called for {UserId}", userId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var constraints = await userConstraintService.GetByUserIdAsync(organizationId, userId);
        var responses = constraints.Select(c => c.ToUserConstraintResponse()).ToList();

        return Ok(responses);
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("userConstraint/by-period/{schedulingPeriodId}")]
    public async Task<IActionResult> GetUserConstraintsByPeriod(Guid schedulingPeriodId)
    {
        logger.LogInformation("Get user constraints by period endpoint was called for {PeriodId}", schedulingPeriodId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var constraints = await userConstraintService.GetBySchedulingPeriodIdAsync(organizationId, schedulingPeriodId);
        var responses = constraints.Select(c => c.ToUserConstraintResponse()).ToList();

        return Ok(responses);
    }

    [Authorize(Policy = ViewerPolicy)]
    [HttpGet("userConstraint/by-period-and-user/{schedulingPeriodId}/{userId}")]
    public async Task<IActionResult> GetUserConstraintsByPeriodAndUser(Guid schedulingPeriodId, Guid userId)
    {
        logger.LogInformation("Get user constraints by period and user endpoint was called for {PeriodId} and {UserId}", schedulingPeriodId, userId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var constraints = await userConstraintService.GetBySchedulingPeriodAndUserIdAsync(organizationId, schedulingPeriodId, userId);
        var responses = constraints.Select(c => c.ToUserConstraintResponse()).ToList();

        return Ok(responses);
    }

    [Authorize(Policy = OperatorPolicy) ]
    [HttpPatch("userConstraint/{userConstraintId}")]
    public async Task<IActionResult> UpdateUserConstraint(
        Guid userConstraintId,
        [FromBody] UpdateUserConstraintRequest request)
    {
        logger.LogInformation("Update user constraint endpoint was called for {ConstraintId}", userConstraintId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await userConstraintService.UpdateUserConstraintAsync(
            organizationId,
            userConstraintId,
            request.Key,
            request.Value,
            request.WeekNum);

        return NoContent();
    }

    [Authorize(Policy = OperatorPolicy)]
    [HttpDelete("userConstraint/{userConstraintId}")]
    public async Task<IActionResult> DeleteUserConstraint(Guid userConstraintId)
    {
        logger.LogInformation("Delete user constraint endpoint was called for {ConstraintId}", userConstraintId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await userConstraintService.DeleteUserConstraintAsync(organizationId, userConstraintId);

        return NoContent();
    }


    [Authorize(Policy = OperatorPolicy)]
    [HttpPost("preferenceConstraint")]
    public async Task<IActionResult> CreateUserPreference([FromBody] CreateUserPreferenceRequest request)
    {
        logger.LogInformation("Create user preference endpoint was called");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var preferenceId = await userPreferenceService.CreateUserPreferenceAsync(
            organizationId,
            request.UserId,
            request.SchedulingPeriodId,
            request.Key,
            request.Value);

        var preference = await userPreferenceService.GetUserPreferenceAsync(
            organizationId,
            request.UserId,
            request.SchedulingPeriodId,
            request.Key);
        var response = preference.ToUserPreferenceResponse();

        return CreatedAtAction(
            nameof(GetUserPreference),
            new { userId = request.UserId, schedulingPeriodId = request.SchedulingPeriodId, key = request.Key },
            response);
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("preferenceConstraint/{userId}/{schedulingPeriodId}/{key}")]
    public async Task<IActionResult> GetUserPreference(Guid userId, Guid schedulingPeriodId, string key)
    {
        logger.LogInformation("Get user preference endpoint was called for {UserId}, {PeriodId}, {Key}", userId, schedulingPeriodId, key);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var preference = await userPreferenceService.GetUserPreferenceAsync(organizationId, userId, schedulingPeriodId, key);
        if (preference == null)
            return NotFound();

        return Ok(preference.ToUserPreferenceResponse());
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("preferenceConstraint")]
    public async Task<IActionResult> GetAllUserPreferences()
    {
        logger.LogInformation("Get all user preferences endpoint was called");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var preferences = await userPreferenceService.GetAllUserPreferencesAsync(organizationId);
        var responses = preferences.Select(p => p.ToUserPreferenceResponse()).ToList();

        return Ok(responses);
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("preferenceConstraint/by-user/{userId}")]
    public async Task<IActionResult> GetUserPreferencesByUser(Guid userId)
    {
        logger.LogInformation("Get user preferences by user endpoint was called for {UserId}", userId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var preferences = await userPreferenceService.GetAllUserPreferencesByUserIdAsync(organizationId, userId);
        var responses = preferences.Select(p => p.ToUserPreferenceResponse()).ToList();

        return Ok(responses);
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("preferenceConstraint/by-period/{schedulingPeriodId}")]
    public async Task<IActionResult> GetUserPreferencesByPeriod(Guid schedulingPeriodId)
    {
        logger.LogInformation("Get user preferences by period endpoint was called for {PeriodId}", schedulingPeriodId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var preferences = await userPreferenceService.GetAllUserPreferencesBySchedulingPeriodIdAsync(organizationId, schedulingPeriodId);
        var responses = preferences.Select(p => p.ToUserPreferenceResponse()).ToList();

        return Ok(responses);
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("preferenceConstraint/by-period-and-user/{schedulingPeriodId}/{userId}")]
    public async Task<IActionResult> GetUserPreferencesByPeriodAndUser(Guid schedulingPeriodId, Guid userId)
    {
        logger.LogInformation("Get user preferences by period and user endpoint was called for {PeriodId} and {UserId}", schedulingPeriodId, userId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var preferences = await userPreferenceService.GetAllUserPreferencesByUserAndPeriodAsync(organizationId, userId, schedulingPeriodId);
        var responses = preferences.Select(p => p.ToUserPreferenceResponse()).ToList();

        return Ok(responses);
    }

    [Authorize (Policy = OperatorPolicy)]
    [HttpPatch("preferenceConstraint/{userId}/{schedulingPeriodId}/{key}")]
    public async Task<IActionResult> UpdateUserPreference(
        Guid userId,
        Guid schedulingPeriodId,
        string key,
        [FromBody] UpdateUserPreferenceRequest request)
    {
        logger.LogInformation("Update user preference endpoint was called for {UserId}, {PeriodId}, {Key}", userId, schedulingPeriodId, key);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await userPreferenceService.UpdateUserPreferenceAsync(
            organizationId,
            userId,
            schedulingPeriodId,
            key,
            request.Value);

        return NoContent();
    }

    [Authorize (Policy = OperatorPolicy)]
    [HttpDelete("preferenceConstraint/{userPreferenceId}")]
    public async Task<IActionResult> DeleteUserPreference(Guid userPreferenceId)
    {
        logger.LogInformation("Delete user preference endpoint was called for {PreferenceId}", userPreferenceId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await userPreferenceService.DeleteUserPreferenceAsync(organizationId, userPreferenceId);

        return NoContent();
    }


    [Authorize (Policy = ResourceManagerPolicy)]
    [HttpPost("policy")]
    public async Task<IActionResult> CreateOrganizationPolicy([FromBody] CreateOrganizationPolicyRequest request)
    {
        logger.LogInformation("Create organization policy endpoint was called");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var policy = await organizationPolicyService.CreatePolicyAsync(
            organizationId,
            request.SchedulingPeriodId,
            request.Key,
            request.Value);

        var response = policy.ToOrganizationPolicyResponse();

        return CreatedAtAction(nameof(GetOrganizationPolicy), new { policyId = policy.Id }, response);
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("policy/{policyId}")]
    public async Task<IActionResult> GetOrganizationPolicy(Guid policyId)
    {
        logger.LogInformation("Get organization policy endpoint was called for {PolicyId}", policyId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var policies = await organizationPolicyService.GetAllPoliciesAsync(organizationId);
        var policy = policies.FirstOrDefault(p => p.Id == policyId);
        
        if (policy == null)
            return NotFound();

        return Ok(policy.ToOrganizationPolicyResponse());
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("policy")]
    public async Task<IActionResult> GetAllOrganizationPolicies()
    {
        logger.LogInformation("Get all organization policies endpoint was called");
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var policies = await organizationPolicyService.GetAllPoliciesAsync(organizationId);
        var responses = policies.Select(p => p.ToOrganizationPolicyResponse()).ToList();

        return Ok(responses);
    }

    [Authorize (Policy = ViewerPolicy)]
    [HttpGet("policy/by-period/{schedulingPeriodId}")]
    public async Task<IActionResult> GetOrganizationPoliciesByPeriod(Guid schedulingPeriodId)
    {
        logger.LogInformation("Get organization policies by period endpoint was called for {PeriodId}", schedulingPeriodId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        var policies = await organizationPolicyService.GetPoliciesBySchedulingPeriodIdsAsync(organizationId, schedulingPeriodId);
        var responses = policies.Select(p => p.ToOrganizationPolicyResponse()).ToList();

        return Ok(responses);
    }

    [Authorize (Policy = ResourceManagerPolicy)]
    [HttpPatch("policy/{policyId}")]
    public async Task<IActionResult> UpdateOrganizationPolicy(
        Guid policyId,
        [FromBody] UpdateOrganizationPolicyRequest request)
    {
        logger.LogInformation("Update organization policy endpoint was called for {PolicyId}", policyId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await organizationPolicyService.UpdatePolicyAsync(
            organizationId,
            policyId,
            request.Key,
            request.Value);

        return NoContent();
    }

    [Authorize (Policy = ResourceManagerPolicy)]
    [HttpDelete("policy/{policyId}")]
    public async Task<IActionResult> DeleteOrganizationPolicy(Guid policyId)
    {
        logger.LogInformation("Delete organization policy endpoint was called for {PolicyId}", policyId);
        var organizationId = ControllerUtils.GetOrganizationIdAndFailIfMissing(HttpContext, logger);

        await organizationPolicyService.DeletePolicyAsync(organizationId, policyId);

        return NoContent();
    }
}
