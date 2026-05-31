using Chronos.Domain.Schedule;
namespace Chronos.MainApi.Schedule.Services;

public interface IUserPreferenceService
{
    Task<Guid> CreateUserPreferenceAsync(Guid organizationId,Guid userId,  Guid schedulingPeriodId, string key, string value);
    Task<UserPreference> GetUserPreferenceAsync(Guid organizationId,Guid userId, Guid schedulingPeriodId, string key);
    Task<List<UserPreference>> GetAllUserPreferencesAsync(Guid organizationId);
    Task<List<UserPreference>> GetAllUserPreferencesByUserIdAsync(Guid organizationId,Guid userId);
    Task<List<UserPreference>> GetAllUserPreferencesBySchedulingPeriodIdAsync(Guid organizationId, Guid schedulingPeriodId);
    Task<List<UserPreference>> GetAllUserPreferencesByUserAndPeriodAsync(Guid organizationId,Guid userId, Guid schedulingPeriodId);
    
    Task UpdateUserPreferenceAsync(Guid organizationId, Guid preferenceId, Guid userId, Guid schedulingPeriodId, string key, string value);
    Task DeleteUserPreferenceAsync(Guid organizationId, Guid userPreferenceId);
    
}