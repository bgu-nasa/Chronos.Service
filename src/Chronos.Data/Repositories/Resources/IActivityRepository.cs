using Chronos.Domain.Resources;

namespace Chronos.Data.Repositories.Resources;

public interface IActivityRepository
{
    Task<Activity?> GetByIdAsync(Guid id);
    Task<List<Activity>> GetAllAsync();
    Task AddAsync(Activity activity);
    Task UpdateAsync(Activity activity);
    Task DeleteAsync(Activity activity);
    Task DeleteBySubjectIdAsync(Guid subjectId);
    Task DeleteByAssignedUserIdAsync(Guid assignedUserId);
}