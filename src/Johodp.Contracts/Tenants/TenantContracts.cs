namespace Johodp.Contracts.Tenants;

/// <summary>
/// Tenant data transfer object
/// </summary>
public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CustomConfigurationId { get; set; }
    public List<string> AllowedReturnUrls { get; set; } = new();
    public List<string> AllowedCorsOrigins { get; set; } = new();
    public string? ClientId { get; set; }
}

/// <summary>
/// DTO to create a new tenant
/// </summary>
public class CreateTenantDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid CustomConfigurationId { get; set; }
    public List<string>? AllowedReturnUrls { get; set; }
    public List<string>? AllowedCorsOrigins { get; set; }
    public string ClientId { get; set; } = string.Empty;
}

/// <summary>
/// DTO to update an existing tenant
/// </summary>
public class UpdateTenantDto
{
    public string? DisplayName { get; set; }
    public Guid? CustomConfigurationId { get; set; }
    public List<string>? AllowedReturnUrls { get; set; }
    public List<string>? AllowedCorsOrigins { get; set; }
    public string? ClientId { get; set; }
    public bool? IsActive { get; set; }
}
