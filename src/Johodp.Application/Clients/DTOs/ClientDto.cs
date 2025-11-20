namespace Johodp.Application.Clients.DTOs;

public class ClientDto
{
    public Guid Id { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public List<string> AllowedScopes { get; set; } = new();
    public List<string> AllowedRedirectUris { get; set; } = new();
    public List<string> AllowedCorsOrigins { get; set; } = new();
    public bool RequireClientSecret { get; set; }
    public bool RequireConsent { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateClientDto
{
    public string ClientName { get; set; } = string.Empty;
    public List<string> AllowedScopes { get; set; } = new();
    public List<string> AllowedRedirectUris { get; set; } = new();
    public List<string> AllowedCorsOrigins { get; set; } = new();
    public bool RequireConsent { get; set; } = true;
}

public class UpdateClientDto
{
    public List<string>? AllowedScopes { get; set; }
    public List<string>? AllowedRedirectUris { get; set; }
    public List<string>? AllowedCorsOrigins { get; set; }
    public bool? RequireConsent { get; set; }
    public bool? IsActive { get; set; }
}
