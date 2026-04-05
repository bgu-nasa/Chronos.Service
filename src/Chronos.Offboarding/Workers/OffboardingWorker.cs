using Chronos.Data.Repositories.Management;
using Cronos;

namespace Chronos.Offboarding.Workers;

public class OffboardingWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<OffboardingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cronSchedule = configuration["Offboarding:CronSchedule"]
                           ?? throw new InvalidOperationException("Offboarding:CronSchedule is not configured.");
        var retentionDays = configuration.GetValue<int>("Offboarding:RetentionDays");
        var cronExpression = CronExpression.Parse(cronSchedule);

        logger.LogInformation("OffboardingWorker started with schedule: {CronSchedule}", cronSchedule);

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

            await RunOffboardingAsync(retentionDays, stoppingToken);
        }
    }

    private async Task RunOffboardingAsync(int retentionDays, CancellationToken ct)
    {
        logger.LogInformation("Offboarding run started");

        using var scope = scopeFactory.CreateScope();
        var organizationRepository = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();
        var departmentRepository = scope.ServiceProvider.GetRequiredService<IDepartmentRepository>();

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
                    // TODO: Call OrganizationRemover.RemoveAsync(organization.Id, ct) after merging removers branch
                    logger.LogInformation("Removed organization {OrganizationId} ({OrganizationName})",
                        organization.Id, organization.Name);
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
                    // TODO: Call DepartmentRemover.RemoveAsync(department.Id, ct) after merging removers branch
                    logger.LogInformation("Removed department {DepartmentId} ({DepartmentName})",
                        department.Id, department.Name);
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
