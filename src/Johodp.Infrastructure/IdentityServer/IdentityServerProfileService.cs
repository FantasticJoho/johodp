namespace Johodp.Infrastructure.IdentityServer;

using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using IdentityModel;
using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Domain.Tenants.ValueObjects;

public class IdentityServerProfileService : IProfileService
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<IdentityServerProfileService> _logger;

    public IdentityServerProfileService(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        ILogger<IdentityServerProfileService> logger)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        if (context.Subject == null)
        {
            _logger.LogWarning("GetProfileDataAsync called with null subject");
            return;
        }

        var sub = context.Subject.FindFirst(JwtClaimTypes.Subject)?.Value
                  ?? context.Subject.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(sub))
        {
            _logger.LogWarning("Subject identifier not found in claims");
            context.IssuedClaims = new List<Claim>();
            return;
        }
        
        _logger.LogDebug("Generating profile data for subject: {Subject}", sub);

        // Retrieve user
        var user = Guid.TryParse(sub, out var userId)
            ? await _userRepository.GetByIdAsync(UserId.From(userId))
            : await GetUserByEmailAsync(context.Subject);

        if (user == null)
        {
            _logger.LogWarning("User not found for subject: {Subject}", sub);
            context.IssuedClaims = new List<Claim>();
            return;
        }
        
        _logger.LogInformation("Building claims for user: {Email}, tenant: {TenantId}", user.Email.Value, user.TenantId.Value);

        // Pre-calculate string conversions
        var userIdString = user.Id.Value.ToString();
        var tenantIdString = user.TenantId.Value.ToString();
        var emailVerified = user.EmailConfirmed ? "true" : "false";
        
        // Check MFA status once
        var mfaWasVerified = context.Subject.FindFirst("mfa_verified")?.Value == "true";
        
        // Read tenant claims directly from the principal if present (no DB access)
        var claimTenantId = context.Subject.FindFirst("tenant_id")?.Value;
        var claimTenantRole = context.Subject.FindFirst("tenant_role")?.Value;
        var claimTenantScope = context.Subject.FindFirst("tenant_scope")?.Value;

        var claims = new List<Claim>(12)
        {
            new Claim(JwtClaimTypes.Subject, userIdString),
            new Claim(JwtClaimTypes.Email, user.Email.Value),
            new Claim(JwtClaimTypes.GivenName, user.FirstName),
            new Claim(JwtClaimTypes.FamilyName, user.LastName),
            new Claim(JwtClaimTypes.EmailVerified, emailVerified)
        };

        if (!string.IsNullOrEmpty(claimTenantId))
            claims.Add(new Claim("tenant_id", claimTenantId));
        if (!string.IsNullOrEmpty(claimTenantRole))
            claims.Add(new Claim("tenant_role", claimTenantRole));
        if (!string.IsNullOrEmpty(claimTenantScope))
            claims.Add(new Claim("tenant_scope", claimTenantScope));

        // Add audience claim (clientId+tenantId) if client exists
        var tenant = await _tenantRepository.GetByIdAsync(user.TenantId);
        if (tenant?.ClientId != null)
        {
            var audience = $"{tenant.ClientId.Value}+{tenantIdString}";
            claims.Add(new Claim(JwtClaimTypes.Audience, audience));
            _logger.LogDebug("Added audience: {Audience}", audience);
        }
        else
        {
            claims.Add(new Claim(JwtClaimTypes.Audience, tenantIdString));
        }

        // Add OIDC authentication claims
        var amrValue = mfaWasVerified 
            ? "[\"pwd\",\"mfa\",\"otp\"]" 
            : "[\"pwd\"]";
        var acrValue = mfaWasVerified ? "2" : "1";
        
        claims.Add(new Claim(JwtClaimTypes.AuthenticationMethod, amrValue));
        claims.Add(new Claim(JwtClaimTypes.AuthenticationContextClassReference, acrValue));
        
        _logger.LogInformation("Auth level for {Email}: acr={Acr}, mfa={MfaVerified}",
            user.Email.Value, acrValue, mfaWasVerified);

        // Filter by requested claim types if specified
        context.IssuedClaims = context.RequestedClaimTypes?.Any() == true
            ? claims.Where(c => context.RequestedClaimTypes.Contains(c.Type)).ToList()
            : claims;
    }

    private async Task<Domain.Users.Aggregates.User?> GetUserByEmailAsync(ClaimsPrincipal subject)
    {
        var email = subject.FindFirst(JwtClaimTypes.Email)?.Value;
        return string.IsNullOrEmpty(email) ? null : await _userRepository.GetByEmailAsync(email);
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        if (context.Subject == null)
        {
            _logger.LogWarning("IsActiveAsync called with null subject");
            context.IsActive = false;
            return;
        }

        var sub = context.Subject.FindFirst(JwtClaimTypes.Subject)?.Value
                  ?? context.Subject.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(sub))
        {
            _logger.LogWarning("Subject identifier not found in IsActiveAsync");
            context.IsActive = false;
            return;
        }
        
        _logger.LogDebug("Checking if user is active for subject: {Subject}", sub);

        var user = Guid.TryParse(sub, out var userId)
            ? await _userRepository.GetByIdAsync(UserId.From(userId))
            : await GetUserByEmailAsync(context.Subject);

        context.IsActive = user?.IsActive == true;
        
        if (user == null)
        {
            _logger.LogWarning("User not found for subject: {Subject}", sub);
        }
        else if (!user.IsActive)
        {
            _logger.LogWarning("User {Email} is inactive", user.Email.Value);
        }
    }
}
