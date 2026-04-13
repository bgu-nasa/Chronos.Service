using System.IdentityModel.Tokens.Jwt;
using Chronos.Domain.Auth;
using Chronos.MainApi.Auth.Configuration;
using Chronos.MainApi.Auth.Services;
using Chronos.MainApi.Management.Contracts;
using Chronos.MainApi.Management.Services;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronos.Tests.MainApi.Auth.Services;

[TestFixture]
[Category("Unit")]
public class TokenGeneratorTests
{
    private IRoleService _roleService;
    private TokenGenerator _sut;

    private readonly AuthConfiguration _config = new()
    {
        SecretKey = "ThisIsASecretKeyForTestingPurposesOnly123!",
        Issuer = "test-issuer",
        Audience = "test-audience",
        ExpiryMinutes = 60
    };

    [SetUp]
    public void SetUp()
    {
        _roleService = Substitute.For<IRoleService>();
        var options = Substitute.For<IOptions<AuthConfiguration>>();
        options.Value.Returns(_config);
        _sut = new TokenGenerator(options, _roleService);
    }

    [Test]
    public async Task GivenUserWithRoles_WhenGenerateToken_ThenTokenContainsUserClaims()
    {
        var user = MakeUser();
        var orgId = user.OrganizationId;
        _roleService.GetUserAssignmentsAsync(orgId, user.Id)
            .Returns(new List<RoleAssignmentResponse>
            {
                new(Guid.NewGuid(), user.Id, orgId, null, RoleType.Administrator)
            });

        var token = await _sut.GenerateTokenAsync(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Multiple(() =>
        {
            Assert.That(jwt.Issuer, Is.EqualTo(_config.Issuer));
            Assert.That(jwt.Audiences.First(), Is.EqualTo(_config.Audience));
            Assert.That(jwt.Claims.First(c => c.Type == "email").Value,
                Is.EqualTo(user.Email));
            Assert.That(jwt.Claims.First(c => c.Type == "organization").Value,
                Is.EqualTo(orgId.ToString()));
            Assert.That(jwt.Claims.Any(c => c.Type == "roles"), Is.True);
        });
    }

    [Test]
    public async Task GivenUserWithNoRoles_WhenGenerateToken_ThenTokenHasEmptyRolesList()
    {
        var user = MakeUser();
        _roleService.GetUserAssignmentsAsync(user.OrganizationId, user.Id)
            .Returns(new List<RoleAssignmentResponse>());

        var token = await _sut.GenerateTokenAsync(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var rolesClaim = jwt.Claims.First(c => c.Type == "roles").Value;
        Assert.That(rolesClaim, Is.EqualTo("[]"));
    }

    [Test]
    public async Task GivenValidUser_WhenGenerateToken_ThenTokenExpiresWithinConfiguredWindow()
    {
        var user = MakeUser();
        _roleService.GetUserAssignmentsAsync(user.OrganizationId, user.Id)
            .Returns(new List<RoleAssignmentResponse>());

        var before = DateTime.UtcNow;
        var token = await _sut.GenerateTokenAsync(user);
        var after = DateTime.UtcNow;

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.That(jwt.ValidTo, Is.GreaterThan(before.AddMinutes(_config.ExpiryMinutes - 1)));
        Assert.That(jwt.ValidTo, Is.LessThan(after.AddMinutes(_config.ExpiryMinutes + 1)));
    }

    private static User MakeUser()
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = "token@test.com",
            FirstName = "Token",
            LastName = "Tester",
            PasswordHash = "hash",
            OrganizationId = Guid.NewGuid()
        };
    }
}
