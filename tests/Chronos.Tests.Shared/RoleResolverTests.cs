using Chronos.Domain.Management.Roles;

namespace Chronos.Tests.Shared;

[TestFixture]
[Category("Unit")]
public class RoleResolverTests
{
    [TestCase(Role.Administrator, Role.Administrator, true)]
    [TestCase(Role.Administrator, Role.UserManager, true)]
    [TestCase(Role.Administrator, Role.ResourceManager, true)]
    [TestCase(Role.Administrator, Role.Operator, true)]
    [TestCase(Role.Administrator, Role.Viewer, true)]
    [TestCase(Role.UserManager, Role.UserManager, true)]
    [TestCase(Role.UserManager, Role.Viewer, true)]
    [TestCase(Role.UserManager, Role.Operator, false)]
    [TestCase(Role.ResourceManager, Role.ResourceManager, true)]
    [TestCase(Role.ResourceManager, Role.Operator, true)]
    [TestCase(Role.ResourceManager, Role.Viewer, true)]
    [TestCase(Role.ResourceManager, Role.UserManager, false)]
    [TestCase(Role.Operator, Role.Operator, true)]
    [TestCase(Role.Operator, Role.ResourceManager, true)]
    [TestCase(Role.Operator, Role.Administrator, true)]
    [TestCase(Role.Operator, Role.Viewer, true)]
    [TestCase(Role.Operator, Role.UserManager, false)]
    [TestCase(Role.Viewer, Role.Viewer, true)]
    [TestCase(Role.Viewer, Role.Operator, false)]
    public void RoleIncludes_ReturnsExpectedResult(Role givenRole, Role requiredRole, bool expected)
    {
        Assert.That(givenRole.RoleIncludes(requiredRole), Is.EqualTo(expected));
    }
}
