namespace Johodp.Infrastructure.IdentityServer;

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Johodp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Custom Client Store qui charge les clients depuis la base de données
/// et calcule dynamiquement les RedirectUris depuis les tenants associés
/// </summary>
public class CustomClientStore : IClientStore
{
    private readonly IClientRepository _clientRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<CustomClientStore> _logger;

    public CustomClientStore(
        IClientRepository clientRepository,
        ITenantRepository tenantRepository,
        ILogger<CustomClientStore> logger)
    {
        _clientRepository = clientRepository;
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    public async Task<Client?> FindClientByIdAsync(string clientId)
    {
        try
        {
            // Chercher d'abord par ClientId (GUID)
            if (Guid.TryParse(clientId, out var clientGuid))
            {
                var domainClient = await _clientRepository.GetByIdAsync(
                    Johodp.Domain.Clients.ValueObjects.ClientId.From(clientGuid));

                if (domainClient != null)
                {
                    return await MapToIdentityServerClient(domainClient);
                }
            }

            // Chercher par nom de client
            var clientByName = await _clientRepository.GetByNameAsync(clientId);
            if (clientByName != null)
            {
                return await MapToIdentityServerClient(clientByName);
            }

            _logger.LogWarning("Client not found: {ClientId}", clientId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding client by ID: {ClientId}", clientId);
            throw;
        }
    }

    private async Task<Client?> MapToIdentityServerClient(Domain.Clients.Aggregates.Client domainClient)
    {
        // Récupérer tous les tenants associés
        var tenants = new List<Domain.Tenants.Aggregates.Tenant>();
        foreach (var tenantIdString in domainClient.AssociatedTenantIds)
        {
            // Parse tenant ID as GUID
            if (Guid.TryParse(tenantIdString, out var tenantGuid))
            {
                var tenantId = Domain.Tenants.ValueObjects.TenantId.From(tenantGuid);
                var tenant = await _tenantRepository.GetByIdAsync(tenantId);
                if (tenant != null && tenant.IsActive)
                {
                    tenants.Add(tenant);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Invalid tenant ID format in client {ClientName}: {TenantId}",
                    domainClient.ClientName,
                    tenantIdString);
            }
        }

        // Agréger les redirect URIs de tous les tenants
        var redirectUris = tenants
            .SelectMany(t => t.AllowedReturnUrls)
            .Distinct()
            .ToList();

        // Agréger les CORS origins de tous les tenants
        var corsOrigins = tenants
            .SelectMany(t => t.AllowedCorsOrigins)
            .Distinct()
            .ToList();

        // Validation: un client sans tenant ou sans redirect URI n'est pas visible pour IdentityServer
        if (domainClient.AssociatedTenantIds.Count == 0)
        {
            _logger.LogInformation(
                "Client {ClientName} (ID: {ClientId}) has no associated tenants. Client is not visible to IdentityServer.",
                domainClient.ClientName,
                domainClient.Id.Value);
            return null;
        }

        if (redirectUris.Count == 0)
        {
            _logger.LogWarning(
                "Client {ClientName} (ID: {ClientId}) has associated tenants but no redirect URIs. Associated tenant IDs: {TenantIds}. Client is not visible to IdentityServer.",
                domainClient.ClientName,
                domainClient.Id.Value,
                string.Join(", ", domainClient.AssociatedTenantIds));
            return null;
        }

        return new Client
        {
            ClientId = domainClient.ClientName,
            ClientName = domainClient.ClientName,
            
            // Authorization Code Flow avec PKCE (SPA)
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = domainClient.RequireClientSecret,
            RequireConsent = domainClient.RequireConsent,
            
            // URLs calculées depuis les tenants
            RedirectUris = redirectUris,
            PostLogoutRedirectUris = redirectUris, // Même URLs pour logout
            AllowedCorsOrigins = corsOrigins,
            
            // Scopes
            AllowedScopes = domainClient.AllowedScopes.ToList(),
            
            // Tokens
            AllowAccessTokensViaBrowser = true,
            AllowOfflineAccess = true, // Refresh tokens
            
            // Durées de vie (vous pouvez les ajuster)
            AccessTokenLifetime = 3600, // 1 heure
            IdentityTokenLifetime = 300, // 5 minutes
            RefreshTokenUsage = TokenUsage.OneTimeOnly,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 1296000, // 15 jours
            
            // Enabled/disabled
            Enabled = domainClient.IsActive
        };
    }
}
