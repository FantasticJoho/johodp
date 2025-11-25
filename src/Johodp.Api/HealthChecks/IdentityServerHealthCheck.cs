using Microsoft.Extensions.Diagnostics.HealthChecks;
using Duende.IdentityServer.Services;

namespace Johodp.Api.HealthChecks;

/// <summary>
/// Health check pour vérifier que IdentityServer est opérationnel
/// </summary>
public class IdentityServerHealthCheck : IHealthCheck
{
    private readonly IIssuerNameService _issuerNameService;
    private readonly ILogger<IdentityServerHealthCheck> _logger;

    public IdentityServerHealthCheck(
        IIssuerNameService issuerNameService,
        ILogger<IdentityServerHealthCheck> logger)
    {
        _issuerNameService = issuerNameService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Vérifier que IdentityServer peut générer son issuer URL
            var issuer = await _issuerNameService.GetCurrentAsync();
            
            if (string.IsNullOrEmpty(issuer))
            {
                _logger.LogWarning("IdentityServer health check failed: issuer is null or empty");
                return HealthCheckResult.Unhealthy("IdentityServer issuer is not configured");
            }

            _logger.LogDebug("IdentityServer health check passed. Issuer: {Issuer}", issuer);
            return HealthCheckResult.Healthy($"IdentityServer is operational (issuer: {issuer})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IdentityServer health check failed with exception");
            return HealthCheckResult.Unhealthy("IdentityServer is not operational", ex);
        }
    }
}
