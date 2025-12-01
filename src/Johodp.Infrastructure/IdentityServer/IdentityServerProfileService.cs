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
        
        _logger.LogInformation("Building claims for user: {Email}, tenant: {TenantId}", user.Email.Value, user.TenantId.Value);

        var claims = new List<Claim>
        {
            new Claim("sub", user.Id.Value.ToString()),
            new Claim("email", user.Email.Value),
            new Claim("given_name", user.FirstName),
            new Claim("family_name", user.LastName),
            new Claim("email_verified", user.EmailConfirmed.ToString().ToLowerInvariant())
        };

        // Add tenant-specific claims (user belongs to single tenant)
        claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));
        claims.Add(new Claim("tenant_role", user.Role));
        claims.Add(new Claim("tenant_scope", user.Scope));
        
        _logger.LogInformation("Added tenant-specific claims for user {Email}: tenant={TenantId}, role={Role}, scope={Scope}",
            user.Email.Value, user.TenantId.Value, user.Role, user.Scope);

        // Note: System-level Scope/Role/Permission aggregates removed
        // All authorization is now handled via UserTenant.Role and UserTenant.Scope (strings)
        // These are already added above as tenant_role and tenant_scope claims

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
