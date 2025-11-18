namespace Johodp.Infrastructure.Services;

using System.Security.Claims;
using Johodp.Domain.Users.Aggregates;

/// <summary>
/// Builder pour créer les claims JWT à partir d'un utilisateur
/// Inclut les rôles, permissions et périmètre
/// </summary>
public class ClaimsBuilder
{
    private readonly List<Claim> _claims = new();

    public ClaimsBuilder AddUserClaims(User user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        _claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id.Value.ToString()));
        _claims.Add(new Claim(ClaimTypes.Email, user.Email.Value));
        _claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));
        _claims.Add(new Claim(ClaimTypes.Surname, user.LastName));

        return this;
    }

    public ClaimsBuilder AddRoles(IReadOnlyList<Role> roles)
    {
        if (roles == null)
            return this;

        foreach (var role in roles.Where(r => r.IsActive))
        {
            _claims.Add(new Claim(ClaimTypes.Role, role.Name));
        }

        return this;
    }

    public ClaimsBuilder AddPermissions(IReadOnlyList<Permission> permissions)
    {
        if (permissions == null)
            return this;

        foreach (var permission in permissions.Where(p => p.IsActive))
        {
            _claims.Add(new Claim("permission", permission.Name.Value));
        }

        return this;
    }

    public ClaimsBuilder AddRolePermissions(IReadOnlyList<Role> roles)
    {
        if (roles == null)
            return this;

        var permissionNames = new HashSet<string>();

        foreach (var role in roles.Where(r => r.IsActive))
        {
            foreach (var permissionId in role.PermissionIds)
            {
                // Note: In a real scenario, you'd fetch permissions from repository
                permissionNames.Add($"role:{role.Name}:permission");
            }
        }

        foreach (var permission in permissionNames)
        {
            _claims.Add(new Claim("permission", permission));
        }

        return this;
    }

    public ClaimsBuilder AddScope(Scope? scope)
    {
        if (scope == null || !scope.IsActive)
            return this;

        _claims.Add(new Claim("scope", scope.Code));
        _claims.Add(new Claim("scope_id", scope.Id.Value.ToString()));

        return this;
    }

    public ClaimsBuilder AddMFARequirement(User user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        if (user.RequiresMFA())
        {
            _claims.Add(new Claim("mfa_required", "true"));
            _claims.Add(new Claim("mfa_enabled", user.MFAEnabled.ToString().ToLowerInvariant()));
        }

        return this;
    }

    public ClaimsBuilder AddCustomClaim(string type, string value)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Claim type cannot be empty", nameof(type));

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Claim value cannot be empty", nameof(value));

        _claims.Add(new Claim(type, value));
        return this;
    }

    public IEnumerable<Claim> Build()
    {
        return _claims.AsReadOnly();
    }

    public ClaimsPrincipal BuildClaimsPrincipal()
    {
        var identity = new ClaimsIdentity(_claims, "Bearer");
        return new ClaimsPrincipal(identity);
    }
}
