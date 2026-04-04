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
        context.Subjects.Remove(subject);
        await context.SaveChangesAsync();
    }

    public async Task DeleteByDepartmentIdAsync(Guid departmentId)
    {
        var subjects = await context.Subjects
            .Where(s => s.DepartmentId == departmentId)
            .ToListAsync();
        context.Subjects.RemoveRange(subjects);
        await context.SaveChangesAsync();
    }

    public async Task DeleteBySchedulingPeriodIdAsync(Guid schedulingPeriodId)
    {
        var subjects = await context.Subjects
            .Where(s => s.SchedulingPeriodId == schedulingPeriodId)
            .ToListAsync();
        context.Subjects.RemoveRange(subjects);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.Subjects
            .AnyAsync(s => s.Id == id);
    }
}