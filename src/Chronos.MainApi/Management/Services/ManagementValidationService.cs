using Chronos.Data.Repositories.Management;
using Chronos.Domain.Management;
using Chronos.Shared.Exceptions;

namespace Chronos.MainApi.Management.Services;

public class ManagementValidationService(
    IOrganizationRepository organizationRepository,
    IDepartmentRepository departmentRepository,
    ILogger<ManagementValidationService> logger)
{
    public async Task ValidateOrganizationAsync(Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);
        
        if (organization == null)
        {
            logger.LogInformation("Organization not found. OrganizationId: {OrganizationId}", organizationId);
            throw new NotFoundException("Organization not found");
        }

        if (organization.Deleted)
        {
            logger.LogInformation("Soft deleted organization retrieved. OrganizationId: {OrganizationId}", organizationId);
        }
    }

    public async Task<Department> ValidateAndGetDepartmentAsync(Guid organizationId, Guid departmentId, bool excludeDeleted)
    {
        var department = await departmentRepository.GetByIdAsync(departmentId);
        
        if (department == null || department.OrganizationId != organizationId)
        {
            logger.LogWarning("Department not found or does not belong to organization. DepartmentId: {DepartmentId}, OrganizationId: {OrganizationId}", departmentId, organizationId);
            throw new NotFoundException("Department not found");
        }

        if (excludeDeleted && department.Deleted)
        {
            logger.LogWarning("Department is deleted. DepartmentId: {DepartmentId}", departmentId);
            throw new NotFoundException("Department not found");
        }

        return department;
    }
}