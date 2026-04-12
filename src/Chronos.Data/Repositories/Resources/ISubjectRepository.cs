using Chronos.Data.Repositories.Common;
using Chronos.Domain.Resources;

namespace Chronos.Data.Repositories.Resources;

public interface ISubjectRepository : IDepartmentScopedRepository
{
    Task<Subject?> GetByIdAsync(Guid id);
    Task<List<Subject>> GetAllAsync();
    Task AddAsync(Subject subject);
    Task UpdateAsync(Subject subject);
    Task DeleteAsync(Subject subject);
}