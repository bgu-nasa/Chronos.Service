namespace Chronos.Data.Repositories.Common;

public interface IDepartmentScopedRepository : IOrganizationScopedRepository
{
    Task<int> DeleteAllByDepartmentIdAsync(Guid departmentId, CancellationToken ct = default);
}
