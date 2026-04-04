using Chronos.Domain.Resources;

namespace Chronos.Data.Repositories.Resources;

public interface ISubjectRepository
{
    Task<Subject?> GetByIdAsync(Guid id);
    Task<List<Subject>> GetAllAsync();
    Task AddAsync(Subject subject);
    Task UpdateAsync(Subject subject);
    Task DeleteAsync(Subject subject);
    Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
}