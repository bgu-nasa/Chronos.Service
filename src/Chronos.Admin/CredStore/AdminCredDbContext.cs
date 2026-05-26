using Chronos.Admin.CredStore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Admin.CredStore;

public class AdminCredDbContext(DbContextOptions<AdminCredDbContext> options) : DbContext(options)
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AdminUserConfiguration());
    }
}
