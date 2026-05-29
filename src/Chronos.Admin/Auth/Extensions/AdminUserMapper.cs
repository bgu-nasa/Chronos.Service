using Chronos.Admin.Auth.Contracts;
using Chronos.Admin.CredStore.Entities;

namespace Chronos.Admin.Auth.Extensions;

public static class AdminUserMapper
{
    public static UserResponse ToUserResponse(this AdminUser user) =>
        new(
            user.Id.ToString(),
            user.Email,
            user.FirstName,
            user.LastName,
            user.AvatarUrl,
            user.Verified,
            user.IsBootstrap,
            user.CreatedAt);
}
