using System.Security.Claims;
using System.Text.Json;
using Chronos.Domain.Management.Roles;
using Chronos.MainApi.Shared.Extensions;
using Chronos.Shared.Exceptions;

namespace Chronos.Tests.MainApi.Shared.Extensions;

[TestFixture]
[Category("Unit")]
public class ClaimsPrincipalExtensionsTests
{
    #region GetUserId

    [Test]
    public void GivenNoNameIdentifierClaim_WhenGetUserId_ThenThrowsTokenMissing()
    {
        var principal = MakePrincipal();

        Assert.Throws<TokenMissingValueException>(() => principal.GetUserId());
    }

    [Test]
    public void GivenInvalidGuidFormat_WhenGetUserId_ThenThrowsTokenMissing()
    {
        var principal = MakePrincipal(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));

        Assert.Throws<TokenMissingValueException>(() => principal.GetUserId());
    }

    [Test]
    public void GivenValidGuid_WhenGetUserId_ThenReturnsGuid()
    {
        var expected = Guid.NewGuid();
        var principal = MakePrincipal(new Claim(ClaimTypes.NameIdentifier, expected.ToString()));

        Assert.That(principal.GetUserId(), Is.EqualTo(expected));
    }

    #endregion

    #region GetOrganizationId

    [Test]
    public void GivenNoOrganizationClaim_WhenGetOrganizationId_ThenThrowsTokenMissing()
    {
        var principal = MakePrincipal();

        Assert.Throws<TokenMissingValueException>(() => principal.GetOrganizationId());
    }

    [Test]
    public void GivenInvalidOrgGuid_WhenGetOrganizationId_ThenThrowsTokenMissing()
    {
        var principal = MakePrincipal(new Claim("organization", "bad-guid"));

        Assert.Throws<TokenMissingValueException>(() => principal.GetOrganizationId());
    }

    [Test]
    public void GivenValidOrgClaim_WhenGetOrganizationId_ThenReturnsGuid()
    {
        var expected = Guid.NewGuid();
        var principal = MakePrincipal(new Claim("organization", expected.ToString()));

        Assert.That(principal.GetOrganizationId(), Is.EqualTo(expected));
    }

    #endregion

    #region GetRoles

    [Test]
    public void GivenNoRoleClaim_WhenGetRoles_ThenReturnsEmpty()
    {
        var principal = MakePrincipal();

        Assert.That(principal.GetRoles(), Is.Empty);
    }

    [Test]
    public void GivenEmptyRoleClaim_WhenGetRoles_ThenReturnsEmpty()
    {
        var principal = MakePrincipal(new Claim("role", ""));

        Assert.That(principal.GetRoles(), Is.Empty);
    }

    [Test]
    public void GivenMalformedJson_WhenGetRoles_ThenReturnsEmpty()
    {
        var principal = MakePrincipal(new Claim("role", "not-json"));

        Assert.That(principal.GetRoles(), Is.Empty);
    }

    [Test]
    public void GivenValidRolesJson_WhenGetRoles_ThenDeserializesCorrectly()
    {
        var orgId = Guid.NewGuid();
        var roles = new List<SimpleRoleAssignment>
        {
            new(Role.Administrator, orgId, null)
        };
        var json = JsonSerializer.Serialize(roles);
        var principal = MakePrincipal(new Claim("role", json));

        var result = principal.GetRoles();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Role, Is.EqualTo(Role.Administrator));
        Assert.That(result[0].OrganizationId, Is.EqualTo(orgId));
    }

    #endregion

    private static ClaimsPrincipal MakePrincipal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }
}
