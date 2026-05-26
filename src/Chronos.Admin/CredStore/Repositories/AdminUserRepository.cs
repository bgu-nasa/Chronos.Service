using Chronos.Admin.CredStore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Admin.CredStore.Repositories;

public class AdminUserRepository(AdminCredDbContext context) : IAdminUserRepository
{
    public async Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.AdminUsers
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<AdminUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await context.AdminUsers
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
    }

    public async Task<IReadOnlyList<AdminUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.AdminUsers
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await context.AdminUsers.CountAsync(cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await context.AdminUsers
            .AnyAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
    }

    public async Task AddAsync(AdminUser user, CancellationToken cancellationToken = default)
    {
        await context.AdminUsers.AddAsync(user, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(AdminUser user, CancellationToken cancellationToken = default)
    {
        context.AdminUsers.Update(user);
        await context.SaveChangesAsync(cancellationToken);
    }
}
