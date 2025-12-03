namespace Johodp.Domain.Clients.Aggregates;

using Common;
using Events;
using ValueObjects;

/// <summary>
/// Client aggregate root - represents an OAuth2/OIDC client application.
/// Clients can be shared across multiple tenants for centralized authentication.
/// </summary>
/// <remarks>
/// <para><strong>OAuth2/OIDC Architecture:</strong></para>
/// <list type="bullet">
/// <item>Clients represent frontend applications that authenticate users via IdentityServer</item>
/// <item>One client can serve multiple tenants (tenant-specific URLs aggregated from associated tenants)</item>
/// <item>Redirect URIs are calculated from associated tenant URLs, not stored directly</item>
/// <item>Scopes define what resources the client can access (e.g., "openid", "profile", "api")</item>
/// </list>
/// 
/// <para><strong>MFA Configuration:</strong></para>
/// <para>If RequireMfa is true, all users from associated tenants must enable TOTP before login.</para>
/// <para>This is enforced at the Client level for security consistency across tenants.</para>
/// 
/// <para><strong>Business Rules:</strong></para>
/// <list type="bullet">
/// <item>Client name must be unique and non-empty</item>
/// <item>RequireClientSecret is false for public clients (SPAs, mobile apps)</item>
/// <item>RequireConsent determines if users see consent screen</item>
/// <item>Tenants can be dynamically associated/dissociated</item>
/// </list>
/// 
/// <para><strong>Domain Events:</strong></para>
/// <list type="bullet">
/// <item>ClientCreatedEvent - fired when new client is created</item>
/// </list>
/// </remarks>
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

    /// <summary>
    /// Factory method to create a new OAuth2/OIDC Client.
    /// Fires ClientCreatedEvent domain event.
    /// </summary>
    /// <param name="clientName">Unique client name (cannot be empty)</param>
    /// <param name="allowedScopes">OAuth2 scopes this client can request (e.g., ["openid", "profile", "api"])</param>
    /// <param name="requireConsent">If true, users see consent screen (default: true)</param>
    /// <param name="requireMfa">If true, all users must enable TOTP multi-factor authentication (default: false)</param>
    /// <returns>New Client instance in Active status</returns>
    /// <exception cref="ArgumentException">Thrown when clientName is empty</exception>
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
            RequireClientSecret = false,
            RequireConsent = requireConsent,
            RequireMfa = requireMfa,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        client.AddDomainEvent(new ClientCreatedEvent(
            client.Id.Value,
            client.ClientName,
            Array.Empty<string>() // Les URLs seront calculées depuis les tenants
        ));

        return client;
    }

    /// <summary>
    /// Returns whether this client has any associated tenants.
    /// Redirect URIs should be calculated by aggregating URLs from associated tenants.
    /// </summary>
    /// <returns>True if client has associated tenants, false otherwise</returns>
    public bool HasAssociatedTenants() => _associatedTenantIds.Count > 0;

    /// <summary>
    /// Associates a tenant with this client for OAuth2/OIDC authentication.
    /// Tenant's URLs will be used to calculate redirect URIs.
    /// </summary>
    /// <param name="tenantId">Tenant ID to associate (cannot be empty)</param>
    /// <exception cref="ArgumentException">Thrown when tenantId is empty</exception>
    public void AssociateTenant(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));

        if (!_associatedTenantIds.Contains(tenantId))
        {
            _associatedTenantIds.Add(tenantId);
        }
    }

    /// <summary>
    /// Removes tenant association from this client.
    /// </summary>
    /// <param name="tenantId">Tenant ID to dissociate</param>
    public void DissociateTenant(string tenantId)
    {
        _associatedTenantIds.Remove(tenantId);
    }

    /// <summary>
    /// Deactivates the client, preventing authentication.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Reactivates a previously deactivated client.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Updates the allowed OAuth2 scopes for this client.
    /// </summary>
    /// <param name="scopes">Array of scope names (e.g., ["openid", "profile", "api"])</param>
    public void UpdateAllowedScopes(string[] scopes)
    {
        AllowedScopes = scopes ?? Array.Empty<string>();
    }

    /// <summary>
    /// Enables multi-factor authentication requirement for all users of this client.
    /// Users from associated tenants must enroll TOTP before login.
    /// </summary>
    public void EnableMfa()
    {
        RequireMfa = true;
    }

    /// <summary>
    /// Disables multi-factor authentication requirement.
    /// </summary>
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
