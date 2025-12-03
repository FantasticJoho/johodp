namespace Johodp.Contracts.Clients;

/// <summary>
/// OAuth2/OIDC Client data transfer object
/// </summary>
public class ClientDto
{
    public Guid Id { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public List<string> AllowedScopes { get; set; } = new();
    public List<string> AssociatedTenantIds { get; set; } = new();
    public bool RequireClientSecret { get; set; }
    public bool RequireConsent { get; set; }
    public bool RequireMfa { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO to create a new OAuth2/OIDC client
/// </summary>
public class CreateClientDto
{
    public string ClientName { get; set; } = string.Empty;
    public List<string> AllowedScopes { get; set; } = new();
    public bool RequireConsent { get; set; } = true;
    public bool RequireMfa { get; set; } = false;
}

/// <summary>
/// DTO to update an existing OAuth2/OIDC client
/// </summary>
public class UpdateClientDto
{
    public List<string>? AllowedScopes { get; set; }
    public List<string>? AssociatedTenantIds { get; set; }
    public bool? RequireClientSecret { get; set; }
    public bool? RequireConsent { get; set; }
    public bool? RequireMfa { get; set; }
    public bool? IsActive { get; set; }
}
