using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;
using Chronos.MainApi.Shared.ExternalMangement;
using Chronos.Shared.Exceptions;

namespace Chronos.MainApi.Schedule.Services;

public class AppealService(
    IAppealRepository appealRepository,
    IAssignmentService assignmentService,
    IManagementExternalService validationService,
    ILogger<AppealService> logger) : IAppealService
{
    public async Task<Guid> CreateAppealAsync(Guid organizationId, Guid assignmentId, string title, string description)
    {
        logger.LogInformation(
            "Creating appeal. OrganizationId: {OrganizationId}, AssignmentId: {AssignmentId}",
            organizationId, assignmentId);

        await validationService.ValidateOrganizationAsync(organizationId);
        await ValidateAssignmentExistsAsync(organizationId, assignmentId);

        var existing = await appealRepository.GetByAssignmentIdAsync(assignmentId);
        if (existing.Count > 0)
            throw new BadRequestException($"An appeal already exists for assignment {assignmentId}.");

        var appeal = new Appeal
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            AssignmentId = assignmentId,
            Title = title,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await appealRepository.AddAsync(appeal);

        logger.LogInformation(
            "Appeal created successfully. AppealId: {AppealId}, OrganizationId: {OrganizationId}",
            appeal.Id, organizationId);

        return appeal.Id;
    }

    public async Task<Appeal> GetAppealAsync(Guid organizationId, Guid appealId)
    {
        logger.LogInformation(
            "Retrieving appeal. OrganizationId: {OrganizationId}, AppealId: {AppealId}",
            organizationId, appealId);

        var appeal = await ValidateAndGetAppealAsync(organizationId, appealId);

        logger.LogInformation(
            "Appeal retrieved successfully. AppealId: {AppealId}, OrganizationId: {OrganizationId}",
            appeal.Id, organizationId);

        return appeal;
    }

    public async Task<List<Appeal>> GetAllAppealsAsync(Guid organizationId)
    {
        logger.LogInformation("Retrieving all appeals. OrganizationId: {OrganizationId}", organizationId);

        await validationService.ValidateOrganizationAsync(organizationId);

        var appeals = await appealRepository.GetAllAsync();

        logger.LogInformation(
            "Retrieved {Count} appeals. OrganizationId: {OrganizationId}",
            appeals.Count, organizationId);

        return appeals;
    }

    public async Task<List<Appeal>> GetAppealsByAssignmentIdAsync(Guid organizationId, Guid assignmentId)
    {
        logger.LogInformation(
            "Retrieving appeals by assignment. OrganizationId: {OrganizationId}, AssignmentId: {AssignmentId}",
            organizationId, assignmentId);

        await validationService.ValidateOrganizationAsync(organizationId);
        await ValidateAssignmentExistsAsync(organizationId, assignmentId);

        var appeals = await appealRepository.GetByAssignmentIdAsync(assignmentId);

        logger.LogInformation(
            "Retrieved {Count} appeals for assignment. OrganizationId: {OrganizationId}, AssignmentId: {AssignmentId}",
            appeals.Count, organizationId, assignmentId);

        return appeals;
    }

    public async Task UpdateAppealAsync(Guid organizationId, Guid appealId, string title, string description)
    {
        logger.LogInformation(
            "Updating appeal. OrganizationId: {OrganizationId}, AppealId: {AppealId}",
            organizationId, appealId);

        var appeal = await ValidateAndGetAppealAsync(organizationId, appealId);

        appeal.Title = title;
        appeal.Description = description;
        appeal.UpdatedAt = DateTime.UtcNow;

        await appealRepository.UpdateAsync(appeal);

        logger.LogInformation(
            "Appeal updated successfully. AppealId: {AppealId}, OrganizationId: {OrganizationId}",
            appeal.Id, organizationId);
    }

    public async Task DeleteAppealAsync(Guid organizationId, Guid appealId)
    {
        logger.LogInformation(
            "Deleting appeal. OrganizationId: {OrganizationId}, AppealId: {AppealId}",
            organizationId, appealId);

        var appeal = await ValidateAndGetAppealAsync(organizationId, appealId);
        await appealRepository.DeleteAsync(appeal);

        logger.LogInformation(
            "Appeal deleted successfully. AppealId: {AppealId}, OrganizationId: {OrganizationId}",
            appeal.Id, organizationId);
    }

    private async Task<Appeal> ValidateAndGetAppealAsync(Guid organizationId, Guid appealId)
    {
        var appeal = await appealRepository.GetByIdAsync(appealId);
        if (appeal == null || appeal.OrganizationId != organizationId)
        {
            logger.LogInformation("Appeal {AppealId} not found for Organization {OrganizationId}", appealId, organizationId);
            throw new NotFoundException($"Appeal with ID {appealId} not found in organization {organizationId}.");
        }

        return appeal;
    }

    private async Task ValidateAssignmentExistsAsync(Guid organizationId, Guid assignmentId)
    {
        await assignmentService.GetAssignmentAsync(organizationId, assignmentId);
    }
}
