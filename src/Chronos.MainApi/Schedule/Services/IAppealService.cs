using Chronos.Domain.Schedule;

namespace Chronos.MainApi.Schedule.Services;

public interface IAppealService
{
    Task<Guid> CreateAppealAsync(Guid organizationId, Guid assignmentId, string title, string description);
    Task<Appeal> GetAppealAsync(Guid organizationId, Guid appealId);
    Task<List<Appeal>> GetAllAppealsAsync(Guid organizationId);
    Task<List<Appeal>> GetAppealsByAssignmentIdAsync(Guid organizationId, Guid assignmentId);
    Task<List<Appeal>> GetAppealsByUserIdAsync(Guid organizationId, Guid userId);
    Task UpdateAppealAsync(Guid organizationId, Guid appealId, string title, string description);
    Task DeleteAppealAsync(Guid organizationId, Guid appealId);
}
