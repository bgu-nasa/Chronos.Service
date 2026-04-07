using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;

namespace Chronos.MainApi.Schedule.Services;

public class OrganizationPolicyService(
    IOrganizationPolicyRepository organizationPolicyRepository,
    IManagementExternalService validationService,
    ISchedulingPeriodService schedulingPeriodService,
    ILogger<OrganizationPolicyService> logger) : IOrganizationPolicyService
{
    public async Task<OrganizationPolicy> CreatePolicyAsync(Guid organizationId, Guid schedulingPeriodId, string key,
        string value)
    {
        logger.LogInformation(
            "Creating organization policy. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}, Key: {Key}, Value: {Value}",
            organizationId, schedulingPeriodId, key, value);

        await validationService.ValidateOrganizationAsync(organizationId);
        await schedulingPeriodService.validateSchedulingPeriodAsync(organizationId, schedulingPeriodId);

        var organizationPolicy = new OrganizationPolicy
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            Key = key,
            Value = value
        };

        await organizationPolicyRepository.AddAsync(organizationPolicy);

        logger.LogInformation(
            "Organization policy created successfully. OrganizationPolicyId: {OrganizationPolicyId}, OrganizationId: {OrganizationId}",
            organizationPolicy.Id, organizationId);

        return organizationPolicy;
    }
    
    public async Task<OrganizationPolicy> GetPolicyAsync(Guid organizationId, Guid organizationPolicyId)
    {
        logger.LogInformation(
            "Retrieving organization policy. OrganizationId: {OrganizationId}, OrganizationPolicyId: {OrganizationPolicyId}",
            organizationId, organizationPolicyId);

        return await ValidateAndGetOrganizationPolicyAsync(organizationId, organizationPolicyId);
    }
    
    public async Task<List<OrganizationPolicy>> GetAllPoliciesAsync(Guid organizationId)
    {
        logger.LogInformation("Retrieving all organization policies. OrganizationId: {OrganizationId}", organizationId);

        await validationService.ValidateOrganizationAsync(organizationId);

        var all = await organizationPolicyRepository.GetAllAsync();
        var filtered = all
            .Where(op => op.OrganizationId == organizationId)
            .ToList();

        return filtered;
    }
    
    public async Task<List<OrganizationPolicy>> GetPoliciesBySchedulingPeriodIdsAsync(Guid organizationId, Guid schedulingPeriodId)
    {
        logger.LogInformation(
            "Retrieving organization policies by SchedulingPeriodId. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}",
            organizationId, schedulingPeriodId);

        await validationService.ValidateOrganizationAsync(organizationId);

        var all = await organizationPolicyRepository.GetByPeriodAsync(schedulingPeriodId);
        var filtered = all
            .Where(op => op.OrganizationId == organizationId)
            .ToList();

        return filtered;
    }

    public async Task<OrganizationPolicy> UpdatePolicyAsync(Guid organizationId, Guid policyId, string key,
        string value)
    {   
        logger.LogInformation(
            "Updating organization policy. OrganizationId: {OrganizationId}, OrganizationPolicyId: {OrganizationPolicyId}, Key: {Key}, Value: {Value}",
            organizationId, policyId, key, value);

        var policy = await ValidateAndGetOrganizationPolicyAsync(organizationId, policyId);

        policy.Key = key;
        policy.Value = value;

        await organizationPolicyRepository.UpdateAsync(policy);

        logger.LogInformation(
            "Organization policy updated successfully. OrganizationPolicyId: {OrganizationPolicyId}, OrganizationId: {OrganizationId}",
            policy.Id, organizationId);

        return policy;
    }
    
    public async Task DeletePolicyAsync(Guid organizationId, Guid organizationPolicyId)
    {
        logger.LogInformation(
            "Deleting organization policy. OrganizationId: {OrganizationId}, OrganizationPolicyId: {OrganizationPolicyId}",
            organizationId, organizationPolicyId);

        var policy = await ValidateAndGetOrganizationPolicyAsync(organizationId, organizationPolicyId);

        await organizationPolicyRepository.DeleteAsync(policy);

        logger.LogInformation(
            "Organization policy deleted successfully. OrganizationPolicyId: {OrganizationPolicyId}, OrganizationId: {OrganizationId}",
            organizationPolicyId, organizationId);
    }

    private async Task<OrganizationPolicy> ValidateAndGetOrganizationPolicyAsync(Guid organizationId, Guid organizationPolicyId)
    {
        await validationService.ValidateOrganizationAsync(organizationId);
        var organizationPolicy = await organizationPolicyRepository.GetByIdAsync(organizationPolicyId);
        if (organizationPolicy == null || organizationPolicy.OrganizationId != organizationId)
        {
            logger.LogInformation(
                "Organization policy not found or does not belong to organization. OrganizationPolicyId: {OrganizationPolicyId}, OrganizationId: {OrganizationId}",
                organizationPolicyId, organizationId);
            throw new NotFoundException("Organization policy not found");
        }

        return organizationPolicy;
    }
    
    
}