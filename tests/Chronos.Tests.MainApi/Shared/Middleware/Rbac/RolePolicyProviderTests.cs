using Chronos.Domain.Management.Roles;
using Chronos.MainApi.Shared.Middleware.Rbac;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Chronos.Tests.MainApi.Shared.Middleware.Rbac;

[TestFixture]
[Category("Unit")]
public class RolePolicyProviderTests
{
    private RolePolicyProvider _sut;

    [SetUp]
    public void SetUp()
    {
        var options = Options.Create(new AuthorizationOptions());
        _sut = new RolePolicyProvider(options);
    }

    [Test]
    public async Task GivenOrgRolePolicy_WhenGetPolicy_ThenReturnsRequireOrgRoleRequirement()
    {
        var policy = await _sut.GetPolicyAsync("OrgRole:Administrator");

        Assert.That(policy, Is.Not.Null);
        var requirement = policy!.Requirements.OfType<RequireOrgRole>().SingleOrDefault();
        Assert.That(requirement, Is.Not.Null);
        Assert.That(requirement!.Role, Is.EqualTo(Role.Administrator));
    }

    [Test]
    public async Task GivenDeptRolePolicy_WhenGetPolicy_ThenReturnsRequireDeptRoleRequirement()
    {
        var policy = await _sut.GetPolicyAsync("DeptRole:Operator");

        Assert.That(policy, Is.Not.Null);
        var requirement = policy!.Requirements.OfType<RequireDeptRole>().SingleOrDefault();
        Assert.That(requirement, Is.Not.Null);
        Assert.That(requirement!.Role, Is.EqualTo(Role.Operator));
    }

    [Test]
    public async Task GivenUnknownPolicy_WhenGetPolicy_ThenFallsBackToDefault()
    {
        var policy = await _sut.GetPolicyAsync("SomeOtherPolicy");

        Assert.That(policy, Is.Null);
    }

    [Test]
    public async Task GivenDefaultPolicy_WhenGetDefaultPolicy_ThenReturnsNonNull()
    {
        var policy = await _sut.GetDefaultPolicyAsync();

        Assert.That(policy, Is.Not.Null);
    }
}
