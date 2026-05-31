using Chronos.Data.Repositories.Management;
using Chronos.Domain.Management;
using Chronos.Shared.Exceptions;

namespace Chronos.MainApi.Management.Services;

public class DepartmentService(
    IDepartmentRepository departmentRepository,
    ManagementValidationService validationService,
    ILogger<DepartmentService> logger) : IDepartmentService
{
    public async Task<Department> CreateDepartmentAsync(Guid organizationId, string name)
    {
        logger.LogInformation("Creating department. OrganizationId: {OrganizationId}, Name: {Name}", organizationId, name);

        await validationService.ValidateOrganizationAsync(organizationId);

        var department = new Department
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            Deleted = false
        };

        await departmentRepository.AddAsync(department);

        logger.LogInformation("Department created successfully. DepartmentId: {DepartmentId}, OrganizationId: {OrganizationId}", department.Id, organizationId);
        return department;
    }

    public async Task<Department> GetDepartmentAsync(Guid organizationId, Guid departmentId)
    {
        logger.LogDebug("Retrieving department. OrganizationId: {OrganizationId}, DepartmentId: {DepartmentId}", organizationId, departmentId);

        await validationService.ValidateOrganizationAsync(organizationId);
        var department = await validationService.ValidateAndGetDepartmentAsync(organizationId, departmentId, excludeDeleted: true);
        return department;
    }

    public async Task<List<Department>> GetDepartmentsAsync(Guid organizationId)
    {
        logger.LogDebug("Retrieving all departments for organization. OrganizationId: {OrganizationId}", organizationId);

        await validationService.ValidateOrganizationAsync(organizationId);

        var allDepartments = await departmentRepository.GetAllAsync();
        var filteredDepartments = allDepartments
            .Where(d => d.OrganizationId == organizationId && !d.Deleted)
            .ToList();

        logger.LogDebug("Retrieved {Count} departments for organization. OrganizationId: {OrganizationId}", filteredDepartments.Count, organizationId);
        return filteredDepartments;
    }

    public async Task UpdateDepartmentAsync(Guid organizationId, Guid departmentId, string name)
    {
        logger.LogInformation("Updating department. OrganizationId: {OrganizationId}, DepartmentId: {DepartmentId}, NewName: {Name}", organizationId, departmentId, name);

        await validationService.ValidateOrganizationAsync(organizationId);
        var department = await validationService.ValidateAndGetDepartmentAsync(organizationId, departmentId, excludeDeleted: true);

        department.Name = name;
        await departmentRepository.UpdateAsync(department);

        logger.LogInformation("Department updated successfully. DepartmentId: {DepartmentId}", departmentId);
    }

    public async Task SetForDeletionAsync(Guid organizationId, Guid departmentId)
    {
        logger.LogInformation("Setting department for deletion. OrganizationId: {OrganizationId}, DepartmentId: {DepartmentId}", organizationId, departmentId);

        await validationService.ValidateOrganizationAsync(organizationId);
        var department = await validationService.ValidateAndGetDepartmentAsync(organizationId, departmentId, excludeDeleted: false);

        if (department.Deleted)
        {
            logger.LogWarning("Department already set for deletion. DepartmentId: {DepartmentId}", departmentId);
            throw new BadRequestException("Department is already set for deletion");
        }

        department.Deleted = true;
        department.DeletedTime = DateTime.UtcNow;
        await departmentRepository.UpdateAsync(department);

        logger.LogInformation("Department set for deletion successfully. DepartmentId: {DepartmentId}", departmentId);
    }

    public async Task RestoreDeletedDepartmentAsync(Guid organizationId, Guid departmentId)
    {
        logger.LogInformation("Restoring deleted department. OrganizationId: {OrganizationId}, DepartmentId: {DepartmentId}", organizationId, departmentId);

        await validationService.ValidateOrganizationAsync(organizationId);
        var department = await validationService.ValidateAndGetDepartmentAsync(organizationId, departmentId, excludeDeleted: false);

        if (!department.Deleted)
        {
            logger.LogWarning("Department is not set for deletion. DepartmentId: {DepartmentId}", departmentId);
            throw new BadRequestException("Department is not set for deletion");
        }

        department.Deleted = false;
        department.DeletedTime = default;
        await departmentRepository.UpdateAsync(department);

        logger.LogInformation("Department restored successfully. DepartmentId: {DepartmentId}", departmentId);
    }

}