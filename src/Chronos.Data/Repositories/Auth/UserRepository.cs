using Chronos.Data.Context;
using Chronos.Data.Repositories.Resources;
using Chronos.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;

namespace Chronos.Data.Repositories.Auth;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
    }

    public async Task<User?> GetByEmailIgnoreFiltersAsync(string email, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
    }

    public async Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Users
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await context.Users.AddAsync(user, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Remove(user);
        foreach (var activity in context.Activities.Where(a => a.AssignedUserId == user.Id))
        {
            context.Assignments.RemoveRange(context.Assignments.Where(a => a.ActivityId == activity.Id));
            context.ActivityConstraints.RemoveRange(context.ActivityConstraints.Where(ac => ac.ActivityId == activity.Id));
            context.Activities.Remove(activity);
        }
        context.UserConstraints.RemoveRange(context.UserConstraints.Where(uc => uc.UserId == user.Id));
        context.UserPreferences.RemoveRange(context.UserPreferences.Where(up => up.UserId == user.Id));
        context.RoleAssignments.RemoveRange(context.RoleAssignments.Where(ra => ra.UserId == user.Id));
        await context.SaveChangesAsync(cancellationToken);
        
    }

    public async Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var users = await context.Users
            .IgnoreQueryFilters()
            .Where(u => u.OrganizationId == organizationId)
            .ToListAsync(ct);
        context.Users.RemoveRange(users);
        await context.SaveChangesAsync(ct);
        return users.Count;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AnyAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<bool> EmailExistsIgnoreFiltersAsync(string email, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
    }
}