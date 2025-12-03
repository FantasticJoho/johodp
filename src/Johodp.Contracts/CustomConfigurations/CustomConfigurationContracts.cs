namespace Johodp.Contracts.CustomConfigurations;

/// <summary>
/// Custom configuration for branding and localization
/// </summary>
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

    // Languages
    public List<string> SupportedLanguages { get; set; } = new();
    public string DefaultLanguage { get; set; } = "fr-FR";
}

/// <summary>
/// DTO to create a new custom configuration
/// </summary>
public class CreateCustomConfigurationDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? CustomCss { get; set; }
    public string? DefaultLanguage { get; set; }
    public List<string>? AdditionalLanguages { get; set; }
}

/// <summary>
/// DTO to update an existing custom configuration
/// </summary>
public class UpdateCustomConfigurationDto
{
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? CustomCss { get; set; }
    public List<string>? SupportedLanguages { get; set; }
    public string? DefaultLanguage { get; set; }
}
