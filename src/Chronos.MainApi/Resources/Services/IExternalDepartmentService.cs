
namespace Chronos.MainApi.Resources.Services;

public interface IExternalDepartmentService
{
    Task validateDepartmentAsync(Guid organizationId, Guid departmentId);
}
