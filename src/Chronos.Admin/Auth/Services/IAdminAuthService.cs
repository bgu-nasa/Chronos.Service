using Chronos.Admin.Auth.Contracts;

namespace Chronos.Admin.Auth.Services;

public interface IAdminAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UserResponse> AddAccountAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserResponse>> ListAccountsAsync(CancellationToken cancellationToken = default);
}
