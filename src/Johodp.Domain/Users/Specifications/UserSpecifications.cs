namespace Johodp.Domain.Users.Specifications;

using Common.Specifications;
using Aggregates;

/// <summary>
/// Specification pour récupérer un utilisateur avec ses rôles et permissions
/// </summary>
public class UserWithRolesAndPermissionsSpecification : Specification<User>
{
    public UserWithRolesAndPermissionsSpecification(Guid userId)
    {
        Criteria = u => u.Id.Value == userId;
        AddInclude("Roles");
        AddInclude("Scope");
    }
}

/// <summary>
/// Specification pour récupérer les utilisateurs admin (avec MFA requis)
/// </summary>
public class AdminUsersWithMFASpecification : Specification<User>
{
    public AdminUsersWithMFASpecification()
    {
        Criteria = u => u.Roles.Any(r => r.RequiresMFA && r.IsActive) && u.IsActive;
        AddInclude("Roles");
    }
}

/// <summary>
/// Specification pour récupérer les utilisateurs actifs par rôle
/// </summary>
public class ActiveUsersByRoleSpecification : Specification<User>
{
    public ActiveUsersByRoleSpecification(Guid roleId)
    {
        Criteria = u => u.IsActive && u.Roles.Any(r => r.Id.Value == roleId && r.IsActive);
        AddInclude("Roles");
        AddInclude("Scope");
    }
}

/// <summary>
/// Specification pour récupérer les utilisateurs par périmètre
/// </summary>
public class UsersByScopeSpecification : Specification<User>
{
    public UsersByScopeSpecification(Guid scopeId)
    {
        Criteria = u => u.Scope != null && u.Scope.Id.Value == scopeId && u.IsActive;
        AddInclude("Roles");
        AddInclude("Scope");
    }
}
