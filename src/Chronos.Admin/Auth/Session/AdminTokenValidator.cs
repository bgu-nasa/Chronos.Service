using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Chronos.Admin.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Chronos.Admin.Auth.Session;

public class AdminTokenValidator(IOptions<AdminConfiguration> config) : IAdminTokenValidator
{
    private readonly AdminConfiguration _config = config.Value;
    private readonly JwtSecurityTokenHandler _handler = new();

    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(_config.SecretKey))
        {
            return null;
        }

        try
        {
            var parameters = BuildValidationParameters();
            return _handler.ValidateToken(token, parameters, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    public bool IsExpired(string token)
    {
        try
        {
            var jwt = _handler.ReadJwtToken(token);
            return jwt.ValidTo < DateTime.UtcNow;
        }
        catch
        {
            return true;
        }
    }

    private TokenValidationParameters BuildValidationParameters() =>
        new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.SecretKey)),
            ValidateIssuer = true,
            ValidIssuer = _config.Issuer,
            ValidateAudience = true,
            ValidAudience = _config.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
}
