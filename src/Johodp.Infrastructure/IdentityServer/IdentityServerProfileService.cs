namespace Johodp.Infrastructure.IdentityServer;

using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using IdentityServer4.Models;
using IdentityServer4.Services;
using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Users.ValueObjects;

public class IdentityServerProfileService : IProfileService
{
    private readonly IUserRepository _userRepository;

    public IdentityServerProfileService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        if (context.Subject == null)
            return;

        var sub = context.Subject.FindFirst("sub")?.Value
                  ?? context.Subject.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        Domain.Users.Aggregates.User? user = null;

        if (Guid.TryParse(sub, out var guid))
        {
            user = await _userRepository.GetByIdAsync(UserId.From(guid));
        }
        else
        {
            var email = context.Subject.FindFirst("email")?.Value;
            if (!string.IsNullOrEmpty(email))
                user = await _userRepository.GetByEmailAsync(email!);
        }

        if (user == null)
        {
            context.IssuedClaims = new List<Claim>();
            return;
        }

        var claims = new List<Claim>
        {
            new Claim("sub", user.Id.Value.ToString()),
            new Claim("email", user.Email.Value),
            new Claim("given_name", user.FirstName),
            new Claim("family_name", user.LastName),
            new Claim("email_verified", user.EmailConfirmed.ToString().ToLowerInvariant())
        };

        if (user.Scope != null)
        {
            claims.Add(new Claim("scope", user.Scope.Code));
        }

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim("role", role.Name));
            if (role.RequiresMFA)
            {
                // add an auth method reference if MFA is required for roles
                claims.Add(new Claim("amr", "mfa"));
            }
        }

        foreach (var perm in user.Permissions)
        {
            claims.Add(new Claim("permission", perm.Name.Value));
        }

        // Filter by requested claim types if provided
        if (context.RequestedClaimTypes != null && context.RequestedClaimTypes.Any())
        {
            context.IssuedClaims = claims.Where(c => context.RequestedClaimTypes.Contains(c.Type)).ToList();
        }
        else
        {
            context.IssuedClaims = claims;
        }
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        if (context.Subject == null)
        {
            context.IsActive = false;
            return;
        }

        var sub = context.Subject.FindFirst("sub")?.Value
                  ?? context.Subject.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        Domain.Users.Aggregates.User? user = null;
        if (Guid.TryParse(sub, out var guid))
        {
            user = await _userRepository.GetByIdAsync(UserId.From(guid));
        }
        else
        {
            var email = context.Subject.FindFirst("email")?.Value;
            if (!string.IsNullOrEmpty(email))
                user = await _userRepository.GetByEmailAsync(email!);
        }

        context.IsActive = user != null && user.IsActive;
    }
}
