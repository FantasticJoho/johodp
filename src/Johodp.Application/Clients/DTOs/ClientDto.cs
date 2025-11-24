namespace Johodp.Application.Clients.DTOs;

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

public class CreateClientDto
{
    public string ClientName { get; set; } = string.Empty;
    public List<string> AllowedScopes { get; set; } = new();
    public bool RequireConsent { get; set; } = true;
    public bool RequireMfa { get; set; } = false;
}

public class UpdateClientDto
{
    public List<string>? AllowedScopes { get; set; }
    public List<string>? AssociatedTenantIds { get; set; }
    public bool? RequireClientSecret { get; set; }
    public bool? RequireConsent { get; set; }
    public bool? RequireMfa { get; set; }
    public bool? IsActive { get; set; }
}
