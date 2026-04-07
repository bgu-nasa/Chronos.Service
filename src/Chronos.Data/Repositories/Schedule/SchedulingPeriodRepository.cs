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
            context.UserConstraints.RemoveRange(context.UserConstraints.Where(uc => uc.SchedulingPeriodId == schedulingPeriod.Id));
            context.UserPreferences.RemoveRange(context.UserPreferences.Where(up => up.SchedulingPeriodId == schedulingPeriod.Id));
            context.OrganizationPolicies.RemoveRange(context.OrganizationPolicies.Where(op => op.SchedulingPeriodId == schedulingPeriod.Id));
            var slots = await context.Slots
                .Where(s => s.SchedulingPeriodId == schedulingPeriod.Id)
                .ToListAsync();
            foreach (var slot in slots)
            {
                context.Assignments.RemoveRange(context.Assignments.Where(a => a.SlotId == slot.Id));
                context.Slots.Remove(slot);
            }
            
            var subjects = await context.Subjects
                .Where(a => a.SchedulingPeriodId == schedulingPeriod.Id)
                .ToListAsync();

            foreach (var subject in subjects)
            {
                var activities = await context.Activities
                .Where(a => a.SubjectId == subject.Id)
                .ToListAsync();

                foreach (var activity in activities)
                {
                    context.ActivityConstraints.RemoveRange(context.ActivityConstraints.Where(ac => ac.ActivityId == activity.Id));
                    context.Activities.Remove(activity);
                }
                context.Subjects.Remove(subject);
            }

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
