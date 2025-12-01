namespace Johodp.Application.CustomConfigurations.DTOs;

public class CustomConfigurationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Branding
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? CustomCss { get; set; }

    // Languages - simple list of BCP47 language codes
    public List<string> SupportedLanguages { get; set; } = new();
    public string DefaultLanguage { get; set; } = "fr-FR";
}

public class CreateCustomConfigurationDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Optional branding
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? CustomCss { get; set; }
    
    // Languages - simple language codes (BCP47 format: fr-FR, en-US, etc.)
    public string? DefaultLanguage { get; set; }
    public List<string>? AdditionalLanguages { get; set; }
}

public class UpdateCustomConfigurationDto
{
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
    
    // Branding
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? CustomCss { get; set; }
    
    // Languages
    public List<string>? SupportedLanguages { get; set; }
    public string? DefaultLanguage { get; set; }
}
