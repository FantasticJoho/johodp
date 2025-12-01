namespace Johodp.Application.Tenants.DTOs;

public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Reference to CustomConfiguration (required)
    public Guid CustomConfigurationId { get; set; }

    // OAuth2/OIDC
    public List<string> AllowedReturnUrls { get; set; } = new();
    public List<string> AllowedCorsOrigins { get; set; } = new();
    public string? ClientId { get; set; }
}

public class CreateTenantDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid CustomConfigurationId { get; set; } // Required reference to CustomConfiguration
    public List<string>? AllowedReturnUrls { get; set; }
    public List<string>? AllowedCorsOrigins { get; set; }
    public string ClientId { get; set; } = string.Empty;
}

public class UpdateTenantDto
{
    public string? DisplayName { get; set; }
    public Guid? CustomConfigurationId { get; set; } // Can be updated, but if provided must be valid
    public List<string>? AllowedReturnUrls { get; set; }
    public List<string>? AllowedCorsOrigins { get; set; }
    public string? ClientId { get; set; }
    public bool? IsActive { get; set; }
}
