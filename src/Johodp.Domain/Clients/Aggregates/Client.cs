namespace Johodp.Domain.Clients.Aggregates;

using Common;
using Events;
using ValueObjects;

public class Client : AggregateRoot
{
    public ClientId Id { get; private set; } = null!;
    public string ClientName { get; private set; } = null!;
    public string[] AllowedScopes { get; private set; } = Array.Empty<string>();
    public string[] AllowedRedirectUris { get; private set; } = Array.Empty<string>();
    public string[] AllowedCorsOrigins { get; private set; } = Array.Empty<string>();
    public bool RequireClientSecret { get; private set; }
    public bool RequireConsent { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Client() { }

    public static Client Create(
        string clientName,
        string[] allowedScopes,
        string[] redirectUris,
        string[] corsOrigins,
        bool requireConsent = true)
    {
        if (string.IsNullOrWhiteSpace(clientName))
            throw new ArgumentException("Client name cannot be empty", nameof(clientName));

        if (redirectUris == null || redirectUris.Length == 0)
            throw new ArgumentException("At least one redirect URI is required", nameof(redirectUris));

        var client = new Client
        {
            Id = ClientId.Create(),
            ClientName = clientName,
            AllowedScopes = allowedScopes ?? Array.Empty<string>(),
            AllowedRedirectUris = redirectUris,
            AllowedCorsOrigins = corsOrigins ?? Array.Empty<string>(),
            RequireClientSecret = true,
            RequireConsent = requireConsent,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        client.AddDomainEvent(new ClientCreatedEvent(
            client.Id.Value,
            client.ClientName,
            client.AllowedRedirectUris
        ));

        return client;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void UpdateAllowedScopes(string[] scopes)
    {
        AllowedScopes = scopes ?? Array.Empty<string>();
    }
}
