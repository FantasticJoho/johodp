namespace Johodp.Domain.Clients.Aggregates;

using Common;
using Events;
using ValueObjects;

public class Client : AggregateRoot
{
    public ClientId Id { get; private set; } = null!;
    public string ClientName { get; private set; } = null!;
    public string[] AllowedScopes { get; private set; } = Array.Empty<string>();
    public bool RequireClientSecret { get; private set; }
    public bool RequireConsent { get; private set; }
    public bool RequireMfa { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Les tenant IDs associés à ce client
    private readonly List<string> _associatedTenantIds = new();
    public IReadOnlyList<string> AssociatedTenantIds => _associatedTenantIds.AsReadOnly();

    private Client() { }

    public static Client Create(
        string clientName,
        string[] allowedScopes,
        bool requireConsent = true,
        bool requireMfa = false)
    {
        if (string.IsNullOrWhiteSpace(clientName))
            throw new ArgumentException("Client name cannot be empty", nameof(clientName));

        var client = new Client
        {
            Id = ClientId.Create(),
            ClientName = clientName,
            AllowedScopes = allowedScopes ?? Array.Empty<string>(),
            RequireClientSecret = true,
            RequireConsent = requireConsent,
            RequireMfa = requireMfa,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        return client;

        client.AddDomainEvent(new ClientCreatedEvent(
            client.Id.Value,
            client.ClientName,
            Array.Empty<string>() // Les URLs seront calculées depuis les tenants
        ));

        return client;
    }

    /// <summary>
    /// Calcule les returnUrls autorisées en agrégeant celles de tous les tenants associés
    /// </summary>
    public string[] GetAllowedRedirectUris(Func<string, string[]> getTenantReturnUrls)
    {
        var allUrls = _associatedTenantIds
            .SelectMany(getTenantReturnUrls)
            .Distinct()
            .ToArray();

        if (allUrls.Length == 0)
            throw new InvalidOperationException($"Client {ClientName} has no redirect URIs from associated tenants");

        return allUrls;
    }

    public void AssociateTenant(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));

        if (!_associatedTenantIds.Contains(tenantId))
        {
            _associatedTenantIds.Add(tenantId);
        }
    }

    public void DissociateTenant(string tenantId)
    {
        _associatedTenantIds.Remove(tenantId);
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    // Les redirect URIs sont maintenant gérées au niveau des Tenants
    // Ces méthodes ne sont plus nécessaires car les URLs viennent des tenants associés

    public void UpdateAllowedScopes(string[] scopes)
    {
        AllowedScopes = scopes ?? Array.Empty<string>();
    }

    public void EnableMfa()
    {
        RequireMfa = true;
    }

    public void DisableMfa()
    {
        RequireMfa = false;
    }

    public void RequireSecret()
    {
        RequireClientSecret = true;
    }

    public void AllowPublicClient()
    {
        RequireClientSecret = false;
    }

    public void EnableConsent()
    {
        RequireConsent = true;
    }

    public void DisableConsent()
    {
        RequireConsent = false;
    }
}
