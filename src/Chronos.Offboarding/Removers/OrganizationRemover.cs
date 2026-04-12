using Chronos.Data.Context;
using Chronos.Data.Repositories.Auth;
using Chronos.Data.Repositories.Management;
using Chronos.Data.Repositories.Resources;
using Chronos.Data.Repositories.Schedule;
using Microsoft.Extensions.Logging;

namespace Chronos.Offboarding.Removers;

public class OrganizationRemover(
    AppDbContext context,
    ILogger<OrganizationRemover> logger,
    IOrganizationRepository organizationRepository,
    IAssignmentRepository assignmentRepository,
    IActivityConstraintRepository activityConstraintRepository,
    IUserPreferenceRepository userPreferenceRepository,
    IUserConstraintRepository userConstraintRepository,
    IResourceAttributeAssignmentRepository resourceAttributeAssignmentRepository,
    IActivityRepository activityRepository,
    ISlotRepository slotRepository,
    ISubjectRepository subjectRepository,
    ISchedulingPeriodRepository schedulingPeriodRepository,
    IResourceRepository resourceRepository,
    IResourceAttributeRepository resourceAttributeRepository,
    IResourceTypeRepository resourceTypeRepository,
    IRoleAssignmentRepository roleAssignmentRepository,
    IOrganizationPolicyRepository organizationPolicyRepository,
    IUserRepository userRepository,
    IDepartmentRepository departmentRepository) : IRemover
{
    public async Task<int> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        logger.LogInformation("Starting removal of Organization {OrganizationId}", id);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var totalDeleted = 0;
            int deleted;

            deleted = await assignmentRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} Assignments for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await activityConstraintRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} ActivityConstraints for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await userPreferenceRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} UserPreferences for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await userConstraintRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} UserConstraints for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await resourceAttributeAssignmentRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} ResourceAttributeAssignments for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await activityRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} Activities for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await slotRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} Slots for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await subjectRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} Subjects for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await schedulingPeriodRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} SchedulingPeriods for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await resourceRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} Resources for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await resourceAttributeRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} ResourceAttributes for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await resourceTypeRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} ResourceTypes for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await roleAssignmentRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} RoleAssignments for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await organizationPolicyRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} OrganizationPolicies for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await userRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} Users for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            deleted = await departmentRepository.DeleteAllByOrganizationIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} Departments for Organization {OrganizationId}", deleted, id);
            totalDeleted += deleted;

            var organization = await organizationRepository.GetByIdAsync(id, ct);
            if (organization is not null)
            {
                await organizationRepository.DeleteAsync(organization, ct);
                totalDeleted++;
            }

            await transaction.CommitAsync(ct);
            logger.LogInformation("Successfully removed Organization {OrganizationId}. Total rows deleted: {TotalDeleted}", id, totalDeleted);
            return totalDeleted;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove Organization {OrganizationId}. Rolling back transaction", id);
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}