using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;
using Chronos.MainApi.Shared.ExternalMangement;

namespace Chronos.MainApi.Schedule.Services;

public class UserPreferenceService(
    IUserPreferenceRepository userPreferenceRepository,
    ILogger<UserPreferenceService> logger,
    IManagementExternalService scheduleValidationService) : IUserPreferenceService
{
    public async Task<Guid> CreateUserPreferenceAsync(Guid organizationId,Guid userId, Guid schedulingPeriodId,
        string key, string value)
    {
        logger.LogInformation(
            "Creating user preference. UserId: {UserId}, OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}, Key: {Key}, Value: {Value}",
            userId, organizationId, schedulingPeriodId, key, value);

        await scheduleValidationService.ValidateOrganizationAsync(organizationId);

        var userPreference = new UserPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            SchedulingPeriodId = schedulingPeriodId,
            Key = key,
            Value = value
        };

        await userPreferenceRepository.AddAsync(userPreference);

        logger.LogInformation(
            "User preference created successfully. UserPreferenceId: {UserPreferenceId}, UserId: {UserId}, OrganizationId: {OrganizationId}",
            userPreference.Id, userId, organizationId);

        return userPreference.Id;
    }

    public async Task<UserPreference> GetUserPreferenceAsync(Guid organizationId,Guid userId,
        Guid schedulingPeriodId, string key)
    {
        logger.LogInformation(
            "Retrieving user preference. UserId: {UserId}, OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}, Key: {Key}",
            userId, organizationId, schedulingPeriodId, key);


        var preference =
            await ValidateAndGetUserPreferenceAsync(organizationId, schedulingPeriodId);
        return preference;
    }

    public async Task<List<UserPreference>> GetAllUserPreferencesAsync(Guid organizationId)
    {
        logger.LogInformation("Retrieving all user preferences. OrganizationId: {OrganizationId}", organizationId);

        await scheduleValidationService.ValidateOrganizationAsync(organizationId);

        var all = await userPreferenceRepository.GetAllAsync();
        var filtered = all
            .Where(up => up.OrganizationId == organizationId)
            .ToList();

        logger.LogInformation("Retrieved {Count} user preferences. OrganizationId: {OrganizationId}", filtered.Count,
            organizationId);
        return filtered;
    }

    public async Task<List<UserPreference>> GetAllUserPreferencesByUserIdAsync(Guid organizationId,Guid userId)
    {
        logger.LogInformation("Retrieving all user preferences by user. UserId: {UserId}, OrganizationId: {OrganizationId}",
            userId, organizationId);

        await scheduleValidationService.ValidateOrganizationAsync(organizationId);

        var all = await userPreferenceRepository.GetByUserIdAsync(userId);
        var filtered = all
            .Where(up => up.OrganizationId == organizationId)
            .ToList();

        logger.LogInformation(
            "Retrieved {Count} user preferences by user. UserId: {UserId}, OrganizationId: {OrganizationId}",
            filtered.Count, userId, organizationId);
        return filtered;
    }

    public async Task<List<UserPreference>> GetAllUserPreferencesBySchedulingPeriodIdAsync(
        Guid organizationId, Guid schedulingPeriodId)
    {
        logger.LogInformation(
            "Retrieving all user preferences by scheduling period. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}",
            organizationId, schedulingPeriodId);

        await scheduleValidationService.ValidateOrganizationAsync(organizationId);

        var all = await userPreferenceRepository.GetBySchedulingPeriodIdAsync(schedulingPeriodId);
        var filtered = all
            .Where(up => up.OrganizationId == organizationId)
            .ToList();

        logger.LogInformation(
            "Retrieved {Count} user preferences by scheduling period. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}",
            filtered.Count, organizationId, schedulingPeriodId);
        return filtered;
    }

    public async Task<List<UserPreference>> GetAllUserPreferencesByUserAndPeriodAsync(Guid organizationId,Guid userId,
        Guid schedulingPeriodId)
    {
        logger.LogInformation(
            "Retrieving all user preferences by user and scheduling period. UserId: {UserId}, OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}",
            userId, organizationId, schedulingPeriodId);

        await scheduleValidationService.ValidateOrganizationAsync(organizationId);

        var all = await userPreferenceRepository.GetByUserPeriodAsync(userId, schedulingPeriodId);
        var filtered = all.Where(up => up.OrganizationId == organizationId).ToList();

        logger.LogInformation(
            "Retrieved {Count} user preferences by user and scheduling period. UserId: {UserId}, OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}",
            filtered.Count, userId, organizationId, schedulingPeriodId);
        return filtered;
    }
    
    public async Task UpdateUserPreferenceAsync(Guid organizationId,Guid userId,
        Guid schedulingPeriodId, string key, string value)
    {
        logger.LogInformation(
            "Updating user preference. UserId: {UserId}, OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}, Key: {Key}, Value: {Value}",
            userId, organizationId, schedulingPeriodId, key, value);

        var preference =
            await ValidateAndGetUserPreferenceAsync(organizationId, schedulingPeriodId);

        preference.Value = value;

        await userPreferenceRepository.UpdateAsync(preference);

        logger.LogInformation(
            "User preference updated successfully. UserPreferenceId: {UserPreferenceId}, UserId: {UserId}, OrganizationId: {OrganizationId}",
            preference.Id, userId, organizationId);
    }
    
    
    public async Task DeleteUserPreferenceAsync(Guid organizationId, Guid userPreferenceId)
    {
        logger.LogInformation(
            "Deleting user preference. UserPreferenceId: {UserPreferenceId}, OrganizationId: {OrganizationId}",
            userPreferenceId, organizationId);

        var preference =
            await ValidateAndGetUserPreferenceAsync(organizationId, userPreferenceId);

        await userPreferenceRepository.DeleteAsync(preference);

        logger.LogInformation(
            "User preference deleted successfully. UserPreferenceId: {UserPreferenceId}, OrganizationId: {OrganizationId}",
            preference.Id, organizationId);
    }

    private async Task<UserPreference> ValidateAndGetUserPreferenceAsync(Guid organizationId,
        Guid userPreferenceId)
    {
        var preference = await userPreferenceRepository.GetByIdAsync(userPreferenceId);
        if (preference == null || preference.OrganizationId != organizationId)
        {
            logger.LogInformation(
                "User preference not found or does not belong to the organization. UserPreferenceId: {UserPreferenceId}, OrganizationId: {OrganizationId}",
                userPreferenceId, organizationId);
            throw new KeyNotFoundException("User preference not found.");
        }

        return preference;
    }
    
    
}