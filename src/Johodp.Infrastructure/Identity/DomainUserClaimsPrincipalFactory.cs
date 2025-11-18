namespace Johodp.Infrastructure.Identity;

using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Johodp.Domain.Users.Aggregates;

/// <summary>
/// Minimal claims principal factory for the domain <see cref="User"/> used by ASP.NET Identity.
/// This creates a ClaimsPrincipal containing the essential claims (sub, name, email, roles, permissions).
/// </summary>
public class DomainUserClaimsPrincipalFactory : IUserClaimsPrincipalFactory<User>
{
    public Task<ClaimsPrincipal> CreateAsync(User user)
    {
        var identity = new ClaimsIdentity("Identity.Application");

        // Subject / NameIdentifier
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim("sub", user.Id.ToString()));

        // Name and email
        identity.AddClaim(new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email.Value));

        // Roles
        foreach (var role in user.Roles)
        {
            if (!string.IsNullOrWhiteSpace(role.Name))
                identity.AddClaim(new Claim(ClaimTypes.Role, role.Name));
        }

        // Permissions as custom claims
        foreach (var perm in user.Permissions)
        {
            var permissionName = perm.Name?.Value ?? perm.Name?.ToString();
            if (!string.IsNullOrWhiteSpace(permissionName))
                identity.AddClaim(new Claim("permission", permissionName));
        }

        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(principal);
    }
}
