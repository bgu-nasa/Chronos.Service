using Chronos.Data.Repositories.Common;
using Chronos.Domain.Schedule;

namespace Chronos.Data.Repositories.Schedule;

public interface IAppealRepository : IOrganizationScopedRepository
{
    Task<Appeal?> GetByIdAsync(Guid id);
    Task<List<Appeal>> GetAllAsync();
    Task<List<Appeal>> GetByAssignmentIdAsync(Guid assignmentId);
    Task<List<Appeal>> GetByAssignmentIdsAsync(List<Guid> assignmentIds);
    Task AddAsync(Appeal appeal);
    Task UpdateAsync(Appeal appeal);
    Task DeleteAsync(Appeal appeal);
    Task<bool> ExistsAsync(Guid id);
}
