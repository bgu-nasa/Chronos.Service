using Chronos.Data.Context;
using Chronos.Domain.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Data.Repositories.Schedule
{
    public class SchedulingPeriodRepository(AppDbContext context) : ISchedulingPeriodRepository
    {
        public async Task<SchedulingPeriod?> GetByIdAsync(Guid id)
        {
            return await context.SchedulingPeriods
                .FirstOrDefaultAsync(sp => sp.Id == id);
        }
        
        public async Task<SchedulingPeriod?> GetByNameAsync(string name)
        {
            return await context.SchedulingPeriods
                .FirstOrDefaultAsync(sp => sp.Name == name);
        }
        
        public async Task<List<SchedulingPeriod>> GetAllAsync()
        {
            return await context.SchedulingPeriods
                .OrderBy(sp => sp.FromDate)
                .ToListAsync();
        }

        public async Task AddAsync(SchedulingPeriod schedulingPeriod)
        {
            await context.SchedulingPeriods.AddAsync(schedulingPeriod);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(SchedulingPeriod schedulingPeriod)
        {
            context.SchedulingPeriods.Update(schedulingPeriod);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(SchedulingPeriod schedulingPeriod)
        {
            context.SchedulingPeriods.Remove(schedulingPeriod);
            await context.SaveChangesAsync();
        }

        public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
        {
            var schedulingPeriods = await context.SchedulingPeriods
                .IgnoreQueryFilters()
                .Where(sp => sp.OrganizationId == organizationId)
                .ToListAsync(ct);
            context.SchedulingPeriods.RemoveRange(schedulingPeriods);
            await context.SaveChangesAsync(ct);
            return schedulingPeriods.Count;
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            return await context.SchedulingPeriods
                .AnyAsync(sp => sp.Id == id);
        }
    }
}
