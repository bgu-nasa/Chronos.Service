using Chronos.Admin.CredStore.Entities;

namespace Chronos.Admin.Auth.Services;

public interface IAdminTokenGenerator
{
    Task<string> GenerateTokenAsync(AdminUser user);
}
