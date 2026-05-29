using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Chronos.Admin.Configuration;
using Chronos.Admin.CredStore.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Chronos.Admin.Auth.Services;

public class AdminTokenGenerator(IOptions<AdminConfiguration> config) : IAdminTokenGenerator
{
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly AdminConfiguration _config = config.Value;

    public Task<string> GenerateTokenAsync(AdminUser user)
    {
        var key = Encoding.UTF8.GetBytes(_config.SecretKey);
        var cred = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256Signature);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _config.Issuer,
            Audience = _config.Audience,
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}")
            ]),
            SigningCredentials = cred,
            Expires = DateTime.UtcNow.AddMinutes(_config.TokenExpiryMinutes)
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return Task.FromResult(_tokenHandler.WriteToken(token));
    }
}
