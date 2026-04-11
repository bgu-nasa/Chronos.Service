using Chronos.Data.Repositories.Common;
using Chronos.Domain.Management.Roles;

namespace Chronos.Data.Repositories.Management;

public interface IRoleAssignmentRepository : IDepartmentScopedRepository
{
    Task<List<RoleAssignment>> GetUserAssignmentsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<RoleAssignment>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<RoleAssignment?> GetAsync(Guid organizationId, Guid roleAssignmentId, CancellationToken cancellationToken = default);
    Task<RoleAssignment> AddAsync(RoleAssignment roleAssignment, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid organizationId, Guid roleAssignmentId, CancellationToken cancellationToken = default);
}