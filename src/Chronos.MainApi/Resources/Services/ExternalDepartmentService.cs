using Chronos.MainApi.Management.Services;

namespace Chronos.MainApi.Resources.Services;

public class ExternalDepartmentService(
    IDepartmentService departmentService,
    ILogger<ExternalDepartmentService> logger) : IExternalDepartmentService
{
    public async Task validateDepartmentAsync(Guid organizationId, Guid departmentId)
    {
        logger.LogInformation("Validating department. OrganizationId: {OrganizationId}, DepartmentId: {DepartmentId}", organizationId, departmentId);
        if(await departmentService.GetDepartmentAsync(organizationId, departmentId) == null)
        {
            logger.LogWarning("Department not found. OrganizationId: {OrganizationId}, DepartmentId: {DepartmentId}", organizationId, departmentId);
            throw new Exception("Department not found");
        }
    }
}
