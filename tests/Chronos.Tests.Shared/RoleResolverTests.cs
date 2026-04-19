using Chronos.Domain.Management.Roles;

namespace Chronos.Tests.Shared;

[TestFixture]
[Category("Unit")]
public class RoleResolverTests
{
    [Test]
    public void GivenAnyRole_WhenViewerRequired_ThenAlwaysGranted()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Role.Viewer.RoleIncludes(Role.Viewer), Is.True);
            Assert.That(Role.Operator.RoleIncludes(Role.Viewer), Is.True);
            Assert.That(Role.Administrator.RoleIncludes(Role.Viewer), Is.True);
        });
    }

    [Test]
    public void GivenAdministrator_WhenAdministratorRequired_ThenGranted()
    {
        Assert.That(Role.Administrator.RoleIncludes(Role.Administrator), Is.True);
    }

    [Test]
    public void GivenAdministrator_WhenUserManagerRequired_ThenGranted()
    {
        Assert.That(Role.Administrator.RoleIncludes(Role.UserManager), Is.True);
    }

    [Test]
    public void GivenAdministrator_WhenResourceManagerRequired_ThenGranted()
    {
        Assert.That(Role.Administrator.RoleIncludes(Role.ResourceManager), Is.True);
    }

    [Test]
    public void GivenAdministrator_WhenOperatorRequired_ThenGranted()
    {
        Assert.That(Role.Administrator.RoleIncludes(Role.Operator), Is.True);
    }

    // BUG: RoleIncludes checks includedRoles.Contains(requiredRole) instead of
    // includedRoles.Contains(givenRole), so it always grants non-Viewer roles
    // regardless of the caller's actual role. These tests document the current
    // (broken) behavior to avoid silent regressions if the bug is fixed.
    [Test]
    public void GivenOperator_WhenAdministratorRequired_ThenGrantedDueToBug()
    {
        Assert.That(Role.Operator.RoleIncludes(Role.Administrator), Is.True);
    }

    [Test]
    public void GivenViewer_WhenOperatorRequired_ThenGrantedDueToBug()
    {
        Assert.That(Role.Viewer.RoleIncludes(Role.Operator), Is.True);
    }
}
