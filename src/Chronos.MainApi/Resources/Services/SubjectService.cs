using Chronos.Data.Repositories.Resources;
using Chronos.Domain.Resources;

namespace Chronos.MainApi.Resources.Services;

public class SubjectService(
    ISubjectRepository subjectRepository,
    ResourceValidationService validationService,
    ILogger<SubjectService> logger) : ISubjectService
{
    public async Task<Subject> CreateSubjectAsync(Guid organizationId, Guid departmentId, Guid schedulingPeriodId, string code, string name)
    {
        logger.LogInformation("Creating subject. OrganizationId: {OrganizationId}, DepartmentId: {DepartmentId}, SchedulingPeriodId: {SchedulingPeriodId}, Code: {Code}, Name: {Name}",
            organizationId, departmentId, schedulingPeriodId, code, name);

        await validationService.ValidationOrganizationAsync(organizationId);

        var subject = new Subject
        {
            OrganizationId = organizationId,
            DepartmentId = departmentId,
            SchedulingPeriodId = schedulingPeriodId,
            Code = code,
            Name = name
        };

        await subjectRepository.AddAsync(subject);

        logger.LogInformation("Subject created successfully. SubjectId: {SubjectId}, OrganizationId: {OrganizationId}", subject.Id, organizationId);
        return subject;
    }

    public async Task<Subject> GetSubjectAsync(Guid organizationId, Guid subjectId)
    {
        logger.LogDebug("Retrieving subject. OrganizationId: {OrganizationId}, SubjectId: {SubjectId}", organizationId, subjectId);

        await validationService.ValidationOrganizationAsync(organizationId);
        var subject = await validationService.ValidateAndGetSubjectAsync(organizationId, subjectId);
        return subject;
    }

    public async Task<List<Subject>> GetSubjectsAsync(Guid organizationId)
    {
        logger.LogDebug("Retrieving all subjects for organization. OrganizationId: {OrganizationId}", organizationId);

        await validationService.ValidationOrganizationAsync(organizationId);

        var allSubjects = await subjectRepository.GetAllAsync();
        var filteredSubjects = allSubjects
            .Where(s => s.OrganizationId == organizationId)
            .ToList();
        
        logger.LogDebug("Retrieved {Count} subjects for organization. OrganizationId: {OrganizationId}", filteredSubjects.Count, organizationId);
        return filteredSubjects;
    }

    public async Task<List<Subject>> GetSubjectsByDepartmentAsync(Guid organizationId, Guid departmentId)
    {
        logger.LogDebug("Retrieving subjects for department. OrganizationId: {OrganizationId}, DepartmentId: {DepartmentId}", organizationId, departmentId);

        await validationService.ValidationOrganizationAsync(organizationId);

        var allSubjects =  await subjectRepository.GetAllAsync();
        var filteredSubjects = allSubjects
            .Where(s => s.OrganizationId == organizationId && s.DepartmentId == departmentId)
            .ToList();

        logger.LogDebug("Retrieved {Count} subjects for department. OrganizationId: {OrganizationId}, DepartmentId: {DepartmentId}", filteredSubjects.Count, organizationId, departmentId);
        return filteredSubjects;
    }

    public async Task UpdateSubjectAsync(Guid organizationId, Guid subjectId, Guid departmentId, Guid schedulingPeriodId, string code,
        string name)
    {
        logger.LogInformation("Updating subject. OrganizationId: {OrganizationId}, SubjectId: {SubjectId}, DepartmentId: {DepartmentId}, SchedulingPeriodId: {SchedulingPeriodId}, Code: {Code}, Name: {Name}",
            organizationId, subjectId,  departmentId, schedulingPeriodId, code, name);

        await validationService.ValidationOrganizationAsync(organizationId);
        var subject = await validationService.ValidateAndGetSubjectAsync(organizationId, subjectId);

        subject.DepartmentId = departmentId;
        subject.SchedulingPeriodId = schedulingPeriodId;
        subject.Code = code;
        subject.Name = name;
        await subjectRepository.UpdateAsync(subject);

        logger.LogInformation("Subject updated successfully. SubjectId: {SubjectId}", subjectId);
    }

    public async Task DeleteSubjectAsync(Guid organizationId, Guid subjectId)
    {
        logger.LogInformation("Deleting subject. OrganizationId: {OrganizationId}, SubjectId: {SubjectId}", organizationId, subjectId);

        await validationService.ValidationOrganizationAsync(organizationId);
        var subject = await validationService.ValidateAndGetSubjectAsync(organizationId, subjectId);
        await subjectRepository.DeleteAsync(subject);

        logger.LogInformation("Subject deleted successfully. SubjectId: {SubjectId}", subjectId);
    }

    public async Task DeleteSubjectsByDepartmentIdAsync(Guid organizationId, Guid departmentId)
    {
        logger.LogInformation("Deleting subjects by department. OrganizationId: {OrganizationId}, DepartmentId: {DepartmentId}", organizationId, departmentId);

        await validationService.ValidationOrganizationAsync(organizationId);
        await subjectRepository.DeleteByDepartmentIdAsync(departmentId);

        logger.LogInformation("Subjects deleted successfully by department. DepartmentId: {DepartmentId}, OrganizationId: {OrganizationId}", departmentId, organizationId);
    }

    public async Task DeleteSubjectsBySchedulingPeriodIdAsync(Guid organizationId, Guid schedulingPeriodId)
    {
        logger.LogInformation("Deleting subjects by scheduling period. OrganizationId: {OrganizationId}, SchedulingPeriodId: {SchedulingPeriodId}", organizationId, schedulingPeriodId);

        await validationService.ValidationOrganizationAsync(organizationId);
        await subjectRepository.DeleteBySchedulingPeriodIdAsync(schedulingPeriodId);

        logger.LogInformation("Subjects deleted successfully by scheduling period. SchedulingPeriodId: {SchedulingPeriodId}, OrganizationId: {OrganizationId}", schedulingPeriodId, organizationId);
    }
}