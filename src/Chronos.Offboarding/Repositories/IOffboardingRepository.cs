namespace Chronos.Offboarding.Repositories;

public interface IOffboardingRepository
{
    /// <summary>
    /// Atomically hard-deletes an organization and all of its related entities.
    /// Returns the total number of rows deleted across all tables.
    /// </summary>
    Task<int> HardDeleteOrganizationAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Atomically hard-deletes a department and all of its related entities.
    /// Returns the total number of rows deleted across all tables.
    /// </summary>
    Task<int> HardDeleteDepartmentAsync(Guid departmentId, CancellationToken ct = default);
}