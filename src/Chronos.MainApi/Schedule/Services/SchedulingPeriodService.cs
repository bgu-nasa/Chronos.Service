using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;

namespace Chronos.MainApi.Schedule.Services;

public class SchedulingPeriodService(
    ISchedulingPeriodRepository schedulingPeriodRepository,
    IManagementExternalService validationService,
    ILogger<SchedulingPeriodService> logger) : ISchedulingPeriodService
{
    public async Task<Guid> CreateSchedulingPeriodAsync(Guid organizationId, string name, DateTime fromDate,
        DateTime toDate)
    {
        logger.LogInformation(
            "Creating scheduling period. OrganizationId: {OrganizationId}, Name: {Name}, FromDate: {FromDate}, ToDate: {ToDate}",
            organizationId, name, fromDate, toDate);
        await validationService.ValidateOrganizationAsync(organizationId);
        
        // Ensure dates are in UTC for PostgreSQL (timestamp with time zone requires UTC)
        var fromDateUtc = fromDate.Kind == DateTimeKind.Utc 
            ? fromDate 
            : (fromDate.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(fromDate, DateTimeKind.Utc)
                : fromDate.ToUniversalTime());
        var toDateUtc = toDate.Kind == DateTimeKind.Utc 
            ? toDate 
            : (toDate.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(toDate, DateTimeKind.Utc)
                : toDate.ToUniversalTime());
        
        await ValidateDateRange(fromDateUtc, toDateUtc);
        
        var period = new SchedulingPeriod
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            FromDate = fromDateUtc,
            ToDate = toDateUtc
        };

        await schedulingPeriodRepository.AddAsync(period);

        logger.LogInformation(
            "Scheduling period created successfully. SchedulingPeriodId: {SchedulingPeriodId}, OrganizationId: {OrganizationId}",
            period.Id, organizationId);

        return period.Id;
    }

    public async Task<SchedulingPeriod> GetSchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId)
    {
        logger.LogInformation(
            "Retrieving scheduling period. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}",
            organizationId, schedulingPeriodId);

        return await ValidateAndGetSchedulingPeriodAsync(organizationId, schedulingPeriodId);
    }
    
    public async Task<SchedulingPeriod> GetSchedulingPeriodByNameAsync(Guid organizationId , string name)
    {
        logger.LogInformation(
            "Retrieving scheduling period by name. OrganizationId: {OrganizationId}, Name: {Name}",
            organizationId, name);
        
        await validationService.ValidateOrganizationAsync(organizationId);

        var period = await schedulingPeriodRepository.GetByNameAsync(name);
        if (period == null || period.OrganizationId != organizationId)
        {
            throw new NotFoundException($"Scheduling period with name '{name}' not found in organization '{organizationId}'.");
        }
        logger.LogInformation(
            "Retrieved scheduling period by name successfully. OrganizationId: {OrganizationId}, Name: {Name}, SchedulingPeriodId: {SchedulingPeriodId}",
            organizationId, name, period.Id);
        return period;
    }
    
    public async Task<List<SchedulingPeriod>> GetAllSchedulingPeriodsAsync(Guid organizationId)
    {
        logger.LogInformation("Retrieving all scheduling periods for organization. OrganizationId: {OrganizationId}", organizationId);

        await validationService.ValidateOrganizationAsync(organizationId);

        var all = await schedulingPeriodRepository.GetAllAsync();
        var filtered = all
            .Where(p => p.OrganizationId == organizationId)
            .OrderBy(p => p.FromDate)
            .ToList();

        logger.LogInformation("Retrieved {Count} scheduling periods for organization. OrganizationId: {OrganizationId}", filtered.Count, organizationId);
        return filtered;
    }

    public async Task UpdateSchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId, string name, DateTime fromDate, DateTime toDate)
    {
        logger.LogInformation(
            "Updating scheduling period. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}",
            organizationId, schedulingPeriodId);

        // Ensure dates are in UTC for PostgreSQL (timestamp with time zone requires UTC)
        var fromDateUtc = fromDate.Kind == DateTimeKind.Utc 
            ? fromDate 
            : (fromDate.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(fromDate, DateTimeKind.Utc)
                : fromDate.ToUniversalTime());
        var toDateUtc = toDate.Kind == DateTimeKind.Utc 
            ? toDate 
            : (toDate.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(toDate, DateTimeKind.Utc)
                : toDate.ToUniversalTime());

        var period = await ValidateAndGetSchedulingPeriodAsync(organizationId, schedulingPeriodId);

        await ValidateDateRange(fromDateUtc, toDateUtc, schedulingPeriodId);

        period.Name = name;
        period.FromDate = fromDateUtc;
        period.ToDate = toDateUtc;

        await schedulingPeriodRepository.UpdateAsync(period);

        logger.LogInformation("Scheduling period updated successfully. SchedulingPeriodId: {SchedulingPeriodId}", schedulingPeriodId);
    }

    public async Task DeleteSchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId)
    {
        logger.LogInformation(
            "Deleting scheduling period. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}",
            organizationId, schedulingPeriodId);

        var period = await ValidateAndGetSchedulingPeriodAsync(organizationId, schedulingPeriodId);
        await schedulingPeriodRepository.DeleteAsync(period);

        logger.LogInformation("Scheduling period deleted successfully. SchedulingPeriodId: {SchedulingPeriodId}", schedulingPeriodId);
    }
    private async Task ValidateDateRange(DateTime fromDate, DateTime toDate, Guid? excludePeriodId = null)
    {
        var todayUtc = DateTime.UtcNow.Date;

        if (fromDate.Date < todayUtc || toDate.Date < todayUtc)
        {
            throw new BadRequestException("FromDate and ToDate cannot be in the past");
        }
        if (fromDate.Date >= toDate.Date)
        {
            throw new BadRequestException("FromDate must be before or equal to ToDate");
        }
        var all = await schedulingPeriodRepository.GetAllAsync();
        foreach (var period in all)
        {
            // Exclude the current period when updating
            if (excludePeriodId.HasValue && period.Id == excludePeriodId.Value)
            {
                continue;
            }

            if (fromDate < period.ToDate && toDate > period.FromDate)
            {
                throw new BadRequestException("The specified date range overlaps with an existing scheduling period.");
            }
        }
    }
    private async Task<SchedulingPeriod> ValidateAndGetSchedulingPeriodAsync(Guid organizationId, Guid schedulingPeriodId)
    {
        var period = await schedulingPeriodRepository.GetByIdAsync(schedulingPeriodId);
        if (period == null || period.OrganizationId != organizationId)
        {
            logger.LogInformation("Scheduling period not found for Organization {OrganizationId} with SchedulingPeriodId {SchedulingPeriodId}", organizationId, schedulingPeriodId);
            throw new NotFoundException($"Scheduling period with ID '{schedulingPeriodId}' not found in organization '{organizationId}'.");
        }

        return period;
    }
}
