using Chronos.Domain.Resources;

namespace Chronos.MainApi.Resources.Services;

public interface ISubjectService
{
    Task<Subject> CreateSubjectAsync(Guid organizationId, Guid departmentId, Guid schedulingPeriodId, string code, string name);
    Task<Subject> GetSubjectAsync(Guid organizationId, Guid subjectId);
    Task<List<Subject>> GetSubjectsAsync(Guid organizationId);
    Task<List<Subject>> GetSubjectsByDepartmentAsync(Guid organizationId, Guid departmentId);
    Task UpdateSubjectAsync(Guid organizationId, Guid subjectId, Guid departmentId, Guid schedulingPeriodId, string code, string name);
    Task DeleteSubjectAsync(Guid organizationId, Guid subjectId);
    Task DeleteSubjectsByDepartmentIdAsync(Guid organizationId, Guid departmentId);
    Task DeleteSubjectsBySchedulingPeriodIdAsync(Guid organizationId, Guid schedulingPeriodId);
}