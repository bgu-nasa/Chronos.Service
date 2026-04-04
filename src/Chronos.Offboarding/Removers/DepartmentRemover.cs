using Chronos.Data.Context;
using Chronos.Data.Repositories.Management;
using Chronos.Data.Repositories.Resources;
using Chronos.Data.Repositories.Schedule;
using Microsoft.Extensions.Logging;

namespace Chronos.Offboarding.Removers;

public class DepartmentRemover(
    AppDbContext context,
    ILogger<DepartmentRemover> logger,
    IDepartmentRepository departmentRepository,
    IAssignmentRepository assignmentRepository,
    IActivityConstraintRepository activityConstraintRepository,
    IActivityRepository activityRepository,
    ISubjectRepository subjectRepository,
    IRoleAssignmentRepository roleAssignmentRepository) : IRemover
{
    public async Task<int> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        logger.LogInformation("Starting removal of Department {DepartmentId}", id);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var totalDeleted = 0;
            int deleted;

            deleted = await assignmentRepository.DeleteAllByDepartmentIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} Assignments for Department {DepartmentId}", deleted, id);
            totalDeleted += deleted;

            deleted = await activityConstraintRepository.DeleteAllByDepartmentIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} ActivityConstraints for Department {DepartmentId}", deleted, id);
            totalDeleted += deleted;

            deleted = await activityRepository.DeleteAllByDepartmentIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} Activities for Department {DepartmentId}", deleted, id);
            totalDeleted += deleted;

            deleted = await subjectRepository.DeleteAllByDepartmentIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} Subjects for Department {DepartmentId}", deleted, id);
            totalDeleted += deleted;

            deleted = await roleAssignmentRepository.DeleteAllByDepartmentIdAsync(id, ct);
            logger.LogInformation("Deleted {Count} RoleAssignments for Department {DepartmentId}", deleted, id);
            totalDeleted += deleted;

            var department = await departmentRepository.GetByIdAsync(id, ct);
            if (department is not null)
            {
                await departmentRepository.DeleteAsync(department, ct);
                totalDeleted++;
            }

            await transaction.CommitAsync(ct);
            logger.LogInformation("Successfully removed Department {DepartmentId}. Total rows deleted: {TotalDeleted}", id, totalDeleted);
            return totalDeleted;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove Department {DepartmentId}. Rolling back transaction", id);
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
