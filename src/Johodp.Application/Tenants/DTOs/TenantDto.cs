namespace Johodp.Application.Tenants.DTOs;

public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Branding
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? CustomCss { get; set; }

    // Localization
    public string DefaultLanguage { get; set; } = "fr-FR";
    public List<string> SupportedLanguages { get; set; } = new();
    public string Timezone { get; set; } = "Europe/Paris";
    public string Currency { get; set; } = "EUR";

    // OAuth2/OIDC
    public List<string> AllowedReturnUrls { get; set; } = new();
    public List<string> AssociatedClientIds { get; set; } = new();
}

public class CreateTenantDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? DefaultLanguage { get; set; }
    public List<string>? SupportedLanguages { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? CustomCss { get; set; }
    public string? Timezone { get; set; }
    public string? Currency { get; set; }
    public List<string>? AllowedReturnUrls { get; set; }
    public List<string>? AssociatedClientIds { get; set; }
}

public class UpdateTenantDto
{
    public string? DisplayName { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? CustomCss { get; set; }
    public string? DefaultLanguage { get; set; }
    public List<string>? SupportedLanguages { get; set; }
    public string? Timezone { get; set; }
    public string? Currency { get; set; }
    public List<string>? AllowedReturnUrls { get; set; }
    public List<string>? AssociatedClientIds { get; set; }
    public bool? IsActive { get; set; }
}
