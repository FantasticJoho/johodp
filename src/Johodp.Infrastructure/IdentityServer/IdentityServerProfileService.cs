namespace Johodp.Infrastructure.IdentityServer;

using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Domain.Tenants.ValueObjects;

public class IdentityServerProfileService : IProfileService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<IdentityServerProfileService> _logger;

    public IdentityServerProfileService(
        IUserRepository userRepository,
        ILogger<IdentityServerProfileService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        if (context.Subject == null)
        {
            _logger.LogWarning("GetProfileDataAsync called with null subject");
            return;
        }

        var sub = context.Subject.FindFirst("sub")?.Value
                  ?? context.Subject.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        _logger.LogDebug("Generating profile data for subject: {Subject}", sub);

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
            _logger.LogWarning("User not found for subject: {Subject}", sub);
            context.IssuedClaims = new List<Claim>();
            return;
        }
        
        _logger.LogInformation("Building claims for user: {Email}, tenants: {TenantCount}", user.Email.Value, user.UserTenants.Count);

        var claims = new List<Claim>
        {
            new Claim("sub", user.Id.Value.ToString()),
            new Claim("email", user.Email.Value),
            new Claim("given_name", user.FirstName),
            new Claim("family_name", user.LastName),
            new Claim("email_verified", user.EmailConfirmed.ToString().ToLowerInvariant())
        };

        // Extract tenant from subject claims (set during login)
        var tenantIdClaim = context.Subject.FindFirst("tenant_id")?.Value;
        
        if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out var tenantGuid))
        {
            var tenantId = TenantId.From(tenantGuid);
            var userTenant = user.GetTenantContext(tenantId);
            
            if (userTenant != null)
            {
                // Add tenant-specific claims for the requested tenant only
                claims.Add(new Claim("tenant_id", userTenant.TenantId.Value.ToString()));
                claims.Add(new Claim("tenant_role", userTenant.Role));
                claims.Add(new Claim("tenant_scope", userTenant.Scope));
                
                _logger.LogInformation("Added tenant-specific claims for user {Email}: tenant={TenantId}, role={Role}, scope={Scope}",
                    user.Email.Value, userTenant.TenantId.Value, userTenant.Role, userTenant.Scope);
            }
            else
            {
                _logger.LogWarning("User {Email} requested tenant {TenantId} but does not have access", 
                    user.Email.Value, tenantGuid);
            }
        }
        else
        {
            // No specific tenant requested - add all tenant IDs (but not role/scope to avoid confusion)
            _logger.LogInformation("No specific tenant claim found, adding all tenant IDs for user {Email}", user.Email.Value);
            foreach (var userTenant in user.UserTenants)
            {
                claims.Add(new Claim("tenant_id", userTenant.TenantId.Value.ToString()));
            }
        }

        if (user.Scope != null)
        {
            claims.Add(new Claim("scope", user.Scope.Code));
        }

        // Add roles - default to "reader" if no roles assigned
        if (user.Roles != null && user.Roles.Any())
        {
            foreach (var role in user.Roles)
            {
                claims.Add(new Claim("role", role.Name));
                if (role.RequiresMFA)
                {
                    // add an auth method reference if MFA is required for roles
                    claims.Add(new Claim("amr", "mfa"));
                }
            }
        }
        else
        {
            claims.Add(new Claim("role", "reader"));
        }

        // Add permissions - default to "reader" if no permissions assigned
        if (user.Permissions != null && user.Permissions.Any())
        {
            foreach (var perm in user.Permissions)
            {
                claims.Add(new Claim("permission", perm.Name.Value));
            }
        }
        else
        {
            claims.Add(new Claim("permission", "reader"));
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
            _logger.LogWarning("IsActiveAsync called with null subject");
            context.IsActive = false;
            return;
        }

        var sub = context.Subject.FindFirst("sub")?.Value
                  ?? context.Subject.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        _logger.LogDebug("Checking if user is active for subject: {Subject}", sub);

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
        
        if (user == null)
        {
            _logger.LogWarning("User not found for subject: {Subject} in IsActiveAsync", sub);
        }
        else if (!user.IsActive)
        {
            _logger.LogWarning("User {Email} is not active", user.Email.Value);
        }
        else
        {
            _logger.LogDebug("User {Email} is active", user.Email.Value);
        }
    }
}
