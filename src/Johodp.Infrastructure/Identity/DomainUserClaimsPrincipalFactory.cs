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
        // Cette méthode est appelée par ASP.NET Identity pour générer le ClaimsPrincipal à partir d'un utilisateur du domaine.
        // Elle ajoute les claims "métier" (nom, email, rôles, sous-périmètres, etc.) à chaque construction du principal.
        // Ces claims sont contextuels et recalculés à chaque requête, ce qui garantit qu'ils sont toujours à jour (ex : changement de rôle, activation/désactivation, etc.).
        // Elle filtre aussi les claims selon le tenant indiqué dans le claim "acr_values" (persisté lors du login).
    {
        var identity = new ClaimsIdentity("Identity.Application");

        // Subject / NameIdentifier
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim("sub", user.Id.ToString()));

        // Name and email
        identity.AddClaim(new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"));
        identity.AddClaim(new Claim("given_name", user.FirstName));
        identity.AddClaim(new Claim("family_name", user.LastName));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email.Value));


        // Récupère acr_values directement depuis les claims du principal (ajouté lors du login)
        string? acrTenantName = null;
        var acrClaim = ClaimsPrincipal.Current?.FindFirst("acr_values");
        if (acrClaim != null)
        {
            acrTenantName = acrClaim.Value;
        }

        if (user.UserTenants != null)
        {
            foreach (var ut in user.UserTenants)
            {
                // If acrTenantName is set, only include claims for that tenant name
                if (acrTenantName == null || (ut.Tenant != null && ut.Tenant.Name == acrTenantName))
                {
                    identity.AddClaim(new Claim("tenantid", ut.TenantId.Value.ToString()));
                    identity.AddClaim(new Claim(ClaimTypes.Role, ut.Role));
                    if (ut.SubScopes != null)
                    {
                        foreach (var subscope in ut.SubScopes)
                        {
                            identity.AddClaim(new Claim("subscope", subscope));
                        }
                    }
                }
            }
        }

        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(principal);
    }
}
