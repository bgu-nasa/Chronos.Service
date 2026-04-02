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
        
        public async Task<(List<SchedulingPeriod> Items, int TotalCount)> GetAllAsync(int page, int pageSize)
        {
            var query = context.SchedulingPeriods.OrderBy(sp => sp.FromDate);
            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, total);
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

        public async Task<bool> ExistsAsync(Guid id)
        {
            return await context.SchedulingPeriods
                .AnyAsync(sp => sp.Id == id);
        }
    }
}
