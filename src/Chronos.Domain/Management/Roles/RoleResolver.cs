namespace Chronos.Domain.Management.Roles;

public static class RoleResolver
{
    /// <summary>
    /// If you have the key, then you have everything in the value.
    /// </summary>
    private static readonly Dictionary<Role, List<Role>> RoleMaps = new()
    {
        { Role.Administrator, [Role.Administrator, Role.UserManager, Role.ResourceManager, Role.Operator, Role.Viewer] },
        { Role.UserManager, [Role.UserManager, Role.Viewer] },
        { Role.ResourceManager, [Role.ResourceManager, Role.Operator, Role.Viewer] },
        { Role.Operator, [Role.Operator, Role.Viewer] },
        { Role.Viewer, [Role.Viewer] }
    };

    /// <summary>
    /// Whether the given role includes the required role.
    /// </summary>
    /// <param name="givenRole">The given role to check.</param>
    /// <param name="requiredRole">The required role.</param>
    /// <returns>true if the given role satisfies the required role, false otherwise.</returns>
    public static bool RoleIncludes(this Role givenRole, Role requiredRole)
    {
        if (requiredRole == Role.Viewer || requiredRole == givenRole)
        {
            return true;
        }

        return RoleMaps.TryGetValue(givenRole, out var includedRoles) && includedRoles.Contains(requiredRole);
    }
}