using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;
using Chronos.Domain.Schedule.Messages;
using Chronos.MainApi.Schedule.Messaging;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;

namespace Chronos.MainApi.Schedule.Services;
public class UserConstraintService(
    IUserConstraintRepository userConstraintRepository,
    IManagementExternalService validationService,
    ISchedulingPeriodService schedulingPeriodService,
    ILogger<UserConstraintService> logger,
    IMessagePublisher messagePublisher) : IUserConstraintService
{
    public async Task<Guid> CreateUserConstraintAsync(Guid organizationId, Guid userId, Guid schedulingPeriodId, string key, string value, int? weekNum = null)
    {
        await validationService.ValidateOrganizationAsync(organizationId);
        await schedulingPeriodService.validateSchedulingPeriodAsync(organizationId, schedulingPeriodId);
        var constraint = new UserConstraint
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            SchedulingPeriodId = schedulingPeriodId,
            WeekNum = weekNum,
            Key = key,
            Value = value
        };
        await userConstraintRepository.AddAsync(constraint);

        await messagePublisher.PublishAsync(
            new HandleConstraintChangeRequest(
                ActivityConstraintId: constraint.Id,
                OrganizationId: organizationId,
                SchedulingPeriodId: schedulingPeriodId,
                Scope: ConstraintScope.User,
                Operation: ConstraintChangeOperation.Created,
                ActivityId: null,
                UserId: userId,
                Mode: SchedulingMode.Online
            ),
            "request.online"
        );

        logger.LogInformation("Created UserConstraint {UserConstraintId} for Organization {OrganizationId}", constraint.Id, organizationId);
        return constraint.Id;
    }
    
    public async Task<UserConstraint> GetUserConstraintByIdAsync(Guid organizationId, Guid userConstraintId)
    {
        logger.LogInformation("Retrieving UserConstraint {UserConstraintId} for Organization {OrganizationId}", userConstraintId, organizationId);
        var uc = await ValidateAndGetUserConstraintAsync(organizationId , userConstraintId);
        logger.LogInformation("Retrieved UserConstraint {UserConstraintId} for Organization {OrganizationId}", userConstraintId, organizationId);
        return uc;
    }
    
    public async Task<List<UserConstraint>> GetAllUserConstraintsAsync(Guid organizationId)
    {
        logger.LogInformation("Retrieving all UserConstraints for Organization {OrganizationId}", organizationId);
        await validationService.ValidateOrganizationAsync(organizationId);
        var constraints = await userConstraintRepository.GetAllAsync();
        var orgConstraints = constraints.Where(uc => uc.OrganizationId == organizationId).ToList();
        logger.LogInformation("Retrieved {Count} UserConstraints for Organization {OrganizationId}", orgConstraints.Count, organizationId);
        return orgConstraints;
    }
    
    public async Task<List<UserConstraint>> GetByUserIdAsync(Guid organizationId, Guid userId)
    {
        logger.LogInformation("Retrieving UserConstraints for User {UserId} in Organization {OrganizationId}", userId, organizationId);
        await validationService.ValidateOrganizationAsync(organizationId);
        var constraints = await userConstraintRepository.GetByUserIdAsync(userId);
        var orgConstraints = constraints.Where(uc => uc.OrganizationId == organizationId).ToList();
        logger.LogInformation("Retrieved {Count} UserConstraints for User {UserId} in Organization {OrganizationId}", orgConstraints.Count, userId, organizationId);
        return orgConstraints;
    }
    
    public async Task<List<UserConstraint>> GetBySchedulingPeriodIdAsync(Guid organizationId, Guid schedulingPeriodId)
    {
        logger.LogInformation("Retrieving UserConstraints for SchedulingPeriod {SchedulingPeriodId} in Organization {OrganizationId}", schedulingPeriodId, organizationId);
        await validationService.ValidateOrganizationAsync(organizationId);
        var constraints = await userConstraintRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId);
        var orgConstraints = constraints.Where(uc => uc.OrganizationId == organizationId).ToList();
        logger.LogInformation("Retrieved {Count} UserConstraints for SchedulingPeriod {SchedulingPeriodId} in Organization {OrganizationId}", orgConstraints.Count, schedulingPeriodId, organizationId);
        return orgConstraints;
    }
    
    public async Task<List<UserConstraint>> GetBySchedulingPeriodAndUserIdAsync(Guid organizationId, Guid schedulingPeriodId, Guid userId)
    {
        logger.LogInformation("Retrieving UserConstraints for User {UserId} and SchedulingPeriod {SchedulingPeriodId} in Organization {OrganizationId}", userId, schedulingPeriodId, organizationId);
        await validationService.ValidateOrganizationAsync(organizationId);
        var constraints = await userConstraintRepository.GetByUserPeriodAsync(userId, schedulingPeriodId);
        var orgConstraints = constraints.Where(uc => uc.OrganizationId == organizationId).ToList();
        logger.LogInformation("Retrieved {Count} UserConstraints for User {UserId} and Scheduling Period {SchedulingPeriodId} in Organization {OrganizationId}", orgConstraints.Count, userId, schedulingPeriodId, organizationId);
        return orgConstraints;
    }
    
    public async Task UpdateUserConstraintAsync(Guid organizationId, Guid userConstraintId, string key, string value, int? weekNum = null)
    {
        logger.LogInformation("Updating UserConstraint {UserConstraintId} for Organization {OrganizationId}", userConstraintId, organizationId);
        var constraint = await ValidateAndGetUserConstraintAsync(organizationId, userConstraintId);
        constraint.WeekNum = weekNum;
        constraint.Key = key;
        constraint.Value = value;
        await userConstraintRepository.UpdateAsync(constraint);

        await messagePublisher.PublishAsync(
            new HandleConstraintChangeRequest(
                ActivityConstraintId: constraint.Id,
                OrganizationId: organizationId,
                SchedulingPeriodId: constraint.SchedulingPeriodId,
                Scope: ConstraintScope.User,
                Operation: ConstraintChangeOperation.Updated,
                ActivityId: null,
                UserId: constraint.UserId,
                Mode: SchedulingMode.Online
            ),
            "request.online"
        );

        logger.LogInformation("Updated UserConstraint {UserConstraintId} for Organization {OrganizationId}", userConstraintId, organizationId);
    }
    
    public async Task DeleteUserConstraintAsync(Guid organizationId, Guid userConstraintId)
    {
        logger.LogInformation("Deleting UserConstraint {UserConstraintId} for Organization {OrganizationId}", userConstraintId, organizationId);
        var constraint = await ValidateAndGetUserConstraintAsync(organizationId, userConstraintId);
        await userConstraintRepository.DeleteAsync(constraint);

        await messagePublisher.PublishAsync(
            new HandleConstraintChangeRequest(
                ActivityConstraintId: constraint.Id,
                OrganizationId: organizationId,
                SchedulingPeriodId: constraint.SchedulingPeriodId,
                Scope: ConstraintScope.User,
                Operation: ConstraintChangeOperation.Deleted,
                ActivityId: null,
                UserId: constraint.UserId,
                Mode: SchedulingMode.Online
            ),
            "request.online"
        );

        logger.LogInformation("Deleted UserConstraint {UserConstraintId} for Organization {OrganizationId}", userConstraintId, organizationId);
    }

    private async Task<UserConstraint> ValidateAndGetUserConstraintAsync(Guid organizationId, Guid userConstraintId)
    {
        var constraint =  await userConstraintRepository.GetByIdAsync(userConstraintId);
        if (constraint == null || constraint.OrganizationId != organizationId)
        {
            logger.LogInformation("UserConstraint {UserConstraintId} not found for Organization {OrganizationId}", userConstraintId, organizationId);
            throw new NotFoundException($"UserConstraint with ID {userConstraintId} not found in organization {organizationId}.");
        }
        return constraint;
    }
}