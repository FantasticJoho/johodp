namespace Johodp.Infrastructure.Identity;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Johodp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Gestionnaire d'authentification par API Key pour les applications tierces
/// Utilisé pour sécuriser l'endpoint /api/users/register
/// </summary>
public class TenantApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ITenantRepository _tenantRepository;

    public TenantApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ITenantRepository tenantRepository)
        : base(options, logger, encoder)
    {
        _tenantRepository = tenantRepository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Vérifier le header Authorization
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return AuthenticateResult.Fail("Missing Authorization header");
        }

        var token = authHeader.ToString();
        
        // Support "Bearer <token>" ou juste "<token>"
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token.Substring("Bearer ".Length).Trim();
        }

        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.Fail("Invalid Authorization header format");
        }

        // Extraire le tenantId depuis un header custom
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(tenantId))
        {
            return AuthenticateResult.Fail("Missing X-Tenant-Id header");
        }

        // Récupérer le tenant et vérifier l'API key
        var tenant = await _tenantRepository.GetByNameAsync(tenantId);

        if (tenant == null)
        {
            Logger.LogWarning("Tenant not found: {TenantId}", tenantId);
            return AuthenticateResult.Fail("Invalid tenant");
        }

        if (!tenant.IsActive)
        {
            Logger.LogWarning("Tenant is inactive: {TenantId}", tenantId);
            return AuthenticateResult.Fail("Tenant is inactive");
        }

        if (string.IsNullOrEmpty(tenant.ApiKey) || tenant.ApiKey != token)
        {
            Logger.LogWarning("Invalid API key for tenant: {TenantId}", tenantId);
            return AuthenticateResult.Fail("Invalid API key");
        }

        // Créer le principal avec les claims
        var claims = new[]
        {
            new Claim("tenant_id", tenant.Name),
            new Claim("client_type", "external_app"),
            new Claim(ClaimTypes.Name, tenant.DisplayName),
            new Claim("tenant_display_name", tenant.DisplayName)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogInformation("Successfully authenticated tenant: {TenantId} via API key", tenantId);

        return AuthenticateResult.Success(ticket);
    }
}
