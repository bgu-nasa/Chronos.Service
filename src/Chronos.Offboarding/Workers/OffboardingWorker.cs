using Chronos.Data.Repositories.Management;
using Chronos.Offboarding.Removers;
using Cronos;
using Microsoft.Extensions.Options;

namespace Chronos.Offboarding.Workers;

public class OffboardingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<OffboardingConfiguration> options,
    ILogger<OffboardingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        var cronExpression = CronExpression.Parse(config.CronSchedule);

        logger.LogInformation("OffboardingWorker started with schedule: {CronSchedule}", config.CronSchedule);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextOccurrence = cronExpression.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Utc);
            if (nextOccurrence is null)
            {
                logger.LogWarning("No next occurrence found for cron expression. Stopping worker");
                break;
            }

            var delay = nextOccurrence.Value - DateTimeOffset.UtcNow;
            logger.LogInformation("Next offboarding run scheduled at {NextRun} (in {Delay})", nextOccurrence.Value, delay);
            await Task.Delay(delay, stoppingToken);

            await RunOffboardingAsync(config.RetentionDays, stoppingToken);
        }
    }

    private async Task RunOffboardingAsync(int retentionDays, CancellationToken ct)
    {
        logger.LogInformation("Offboarding run started");

        using var scope = scopeFactory.CreateScope();
        var organizationRepository = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();
        var departmentRepository = scope.ServiceProvider.GetRequiredService<IDepartmentRepository>();
        var organizationRemover = scope.ServiceProvider.GetRequiredService<OrganizationRemover>();
        var departmentRemover = scope.ServiceProvider.GetRequiredService<DepartmentRemover>();

        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        try
        {
            var deletedOrganizations = await organizationRepository.GetAllDeletedAsync(ct);
            var expiredOrganizations = deletedOrganizations
                .Where(o => o.DeletedTime <= cutoffDate)
                .ToList();

            logger.LogInformation("Found {Count} organizations past the {Days}-day retention period",
                expiredOrganizations.Count, retentionDays);

            foreach (var organization in expiredOrganizations)
            {
                try
                {
                    var deleted = await organizationRemover.RemoveAsync(organization.Id, ct);
                    logger.LogInformation("Removed organization {OrganizationId} ({OrganizationName}). Rows deleted: {Deleted}",
                        organization.Id, organization.Name, deleted);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to remove organization {OrganizationId} ({OrganizationName})",
                        organization.Id, organization.Name);
                }
            }

            var deletedDepartments = await departmentRepository.GetAllDeletedAsync(ct);
            var expiredDepartments = deletedDepartments
                .Where(d => d.DeletedTime <= cutoffDate)
                .ToList();

            logger.LogInformation("Found {Count} departments past the {Days}-day retention period",
                expiredDepartments.Count, retentionDays);

            foreach (var department in expiredDepartments)
            {
                try
                {
                    var deleted = await departmentRemover.RemoveAsync(department.Id, ct);
                    logger.LogInformation("Removed department {DepartmentId} ({DepartmentName}). Rows deleted: {Deleted}",
                        department.Id, department.Name, deleted);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to remove department {DepartmentId} ({DepartmentName})",
                        department.Id, department.Name);
                }
            }

            logger.LogInformation("Offboarding run completed. Processed {OrgCount} organizations and {DeptCount} departments",
                expiredOrganizations.Count, expiredDepartments.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Offboarding run failed");
        }
    }
}
