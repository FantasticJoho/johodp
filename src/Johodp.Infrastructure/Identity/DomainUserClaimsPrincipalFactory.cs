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

        // Add tenant ID and role/scope
        identity.AddClaim(new Claim("tenantid", user.TenantId.Value.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role));
        identity.AddClaim(new Claim("scope", user.Scope));

        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(principal);
    }
}
