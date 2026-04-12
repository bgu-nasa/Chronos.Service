using Chronos.Data.Context;
using Chronos.Domain.Resources;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Resources;

public class SubjectRepository(AppDbContext context) : ISubjectRepository
{
    public async Task<Subject?> GetByIdAsync(Guid id)
    {
        return await context.Subjects
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<List<Subject>> GetAllAsync()
    {
        return await context.Subjects
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task AddAsync(Subject subject)
    { 
        await context.Subjects.AddAsync(subject);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Subject subject)
    {
        context.Subjects.Update(subject);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Subject subject)
    {
        var activities = await context.Activities
        .Where(a => a.SubjectId == subject.Id)
        .ToListAsync();

        foreach (var activity in activities)
        {
            context.ActivityConstraints.RemoveRange(context.ActivityConstraints.Where(ac => ac.ActivityId == activity.Id));
            context.Assignments.RemoveRange(context.Assignments.Where(a => a.ActivityId == activity.Id));
            context.Activities.Remove(activity);
        }
        context.Subjects.Remove(subject);
        await context.SaveChangesAsync();
    }

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var subjects = await context.Subjects
            .IgnoreQueryFilters()
            .Where(s => s.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.Subjects.RemoveRange(subjects);
        await context.SaveChangesAsync(ct);
        return subjects.Count;
    }

    public async Task<int> DeleteAllByDepartmentIdAsync(Guid departmentId, CancellationToken ct = default)
    {
        var subjects = await context.Subjects
            .IgnoreQueryFilters()
            .Where(s => s.DepartmentId == departmentId)
            .ToListAsync(ct);
        context.Subjects.RemoveRange(subjects);
        await context.SaveChangesAsync(ct);
        return subjects.Count;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.Subjects
            .AnyAsync(s => s.Id == id);
    }
}