using Chronos.Data.Repositories.Common;
using Chronos.Domain.Management;

namespace Chronos.Data.Repositories.Management;

public interface IDepartmentRepository : IOrganizationScopedRepository
{
    Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Department>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Department department, CancellationToken cancellationToken = default);
    Task UpdateAsync(Department department, CancellationToken cancellationToken = default);
    Task DeleteAsync(Department department, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Department>> GetAllDeletedAsync(CancellationToken cancellationToken = default);
}