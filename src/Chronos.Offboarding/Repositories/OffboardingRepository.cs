using Chronos.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chronos.Offboarding.Repositories;

public class OffboardingRepository(AppDbContext context, ILogger<OffboardingRepository> logger)
    : IOffboardingRepository
{
    private static readonly string[] OrganizationDeleteStatements =
    [
        "DELETE FROM assignments WHERE organization_id = {0}",
        "DELETE FROM activity_constraints WHERE organization_id = {0}",
        "DELETE FROM resource_attribute_assignments WHERE organization_id = {0}",
        "DELETE FROM activitys WHERE organization_id = {0}",
        "DELETE FROM slots WHERE organization_id = {0}",
        "DELETE FROM organization_policys WHERE organization_id = {0}",
        "DELETE FROM user_constraints WHERE organization_id = {0}",
        "DELETE FROM user_preferences WHERE organization_id = {0}",
        "DELETE FROM subjects WHERE organization_id = {0}",
        "DELETE FROM resources WHERE organization_id = {0}",
        "DELETE FROM resource_attributes WHERE organization_id = {0}",
        "DELETE FROM resource_types WHERE organization_id = {0}",
        "DELETE FROM scheduling_periods WHERE organization_id = {0}",
        "DELETE FROM role_assignments WHERE organization_id = {0}",
        "DELETE FROM users WHERE organization_id = {0}",
        "DELETE FROM departments WHERE organization_id = {0}",
        "DELETE FROM organizations WHERE id = {0}",
    ];

    private static readonly string[] DepartmentDeleteStatements =
    [
        "DELETE FROM assignments WHERE activity_id IN (SELECT a.id FROM activitys a INNER JOIN subjects s ON a.subject_id = s.id WHERE s.department_id = {0})",
        "DELETE FROM activity_constraints WHERE activity_id IN (SELECT a.id FROM activitys a INNER JOIN subjects s ON a.subject_id = s.id WHERE s.department_id = {0})",
        "DELETE FROM activitys WHERE subject_id IN (SELECT id FROM subjects WHERE department_id = {0})",
        "DELETE FROM subjects WHERE department_id = {0}",
        "DELETE FROM role_assignments WHERE department_id = {0}",
        "DELETE FROM departments WHERE id = {0}",
    ];

    public async Task<int> HardDeleteOrganizationAsync(Guid organizationId, CancellationToken ct = default)
    {
        logger.LogInformation("Starting hard delete for organization {OrganizationId}", organizationId);

        var totalDeleted = await ExecuteAtomicDeleteAsync(OrganizationDeleteStatements, organizationId, ct);

        logger.LogInformation("Hard delete for organization {OrganizationId} completed — {TotalDeleted} rows removed",
            organizationId, totalDeleted);

        return totalDeleted;
    }

    public async Task<int> HardDeleteDepartmentAsync(Guid departmentId, CancellationToken ct = default)
    {
        logger.LogInformation("Starting hard delete for department {DepartmentId}", departmentId);

        var totalDeleted = await ExecuteAtomicDeleteAsync(DepartmentDeleteStatements, departmentId, ct);

        logger.LogInformation("Hard delete for department {DepartmentId} completed — {TotalDeleted} rows removed",
            departmentId, totalDeleted);

        return totalDeleted;
    }

    private async Task<int> ExecuteAtomicDeleteAsync(string[] statements, Guid id, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        try
        {
            var totalDeleted = 0;

            foreach (var sql in statements)
            {
                var rowsAffected = await context.Database.ExecuteSqlRawAsync(sql, [id], ct);
                if (rowsAffected > 0)
                {
                    var tableName = ExtractTableName(sql);
                    logger.LogDebug("  Deleted {RowsAffected} rows from {Table}", rowsAffected, tableName);
                }
                totalDeleted += rowsAffected;
            }

            await transaction.CommitAsync(ct);
            return totalDeleted;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Hard delete failed for entity {Id} — rolling back transaction", id);
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static string ExtractTableName(string sql)
    {
        var fromIndex = sql.IndexOf("FROM ", StringComparison.OrdinalIgnoreCase) + 5;
        var spaceIndex = sql.IndexOf(' ', fromIndex);
        return spaceIndex == -1 ? sql[fromIndex..] : sql[fromIndex..spaceIndex];
    }
}
