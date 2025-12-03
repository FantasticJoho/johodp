namespace Johodp.Domain.CustomConfigurations.Aggregates;

using Johodp.Domain.Common;
using Johodp.Messaging.Events;
using Johodp.Domain.CustomConfigurations.ValueObjects;
using Johodp.Domain.CustomConfigurations.Events;

/// <summary>
/// CustomConfiguration aggregate representing branding and localization settings
/// that can be shared across multiple tenants.
/// CustomConfiguration is independent and can be used by multiple tenants.
/// </summary>
public class CustomConfiguration : AggregateRoot
{
    public CustomConfigurationId Id { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Branding
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? BackgroundImageUrl { get; private set; }
    public string? CustomCss { get; private set; }

    // Languages - simple list of BCP47 language codes
    private readonly List<string> _supportedLanguages = new();
    public IReadOnlyList<string> SupportedLanguages => _supportedLanguages.AsReadOnly();
    public string DefaultLanguage { get; private set; } = "fr-FR";

    private CustomConfiguration() { }

    public static CustomConfiguration Create(
        string name,
        string? description = null,
        string? defaultLanguage = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("CustomConfiguration name cannot be empty", nameof(name));
        
        if (name.Length > 100)
            throw new ArgumentException("CustomConfiguration name cannot exceed 100 characters", nameof(name));

        if (description?.Length > 500)
            throw new ArgumentException("Description cannot exceed 500 characters", nameof(description));

        var language = defaultLanguage ?? "fr-FR";

        var customConfig = new CustomConfiguration
        {
            Id = CustomConfigurationId.Create(),
            Name = name,
            Description = description,
            DefaultLanguage = language,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Add default supported language
        customConfig._supportedLanguages.Add(language);

        customConfig.AddDomainEvent(new CustomConfigurationCreatedEvent(
            customConfig.Id,
            customConfig.Name));

        return customConfig;
    }

    public void UpdateBranding(
        string? primaryColor = null,
        string? secondaryColor = null,
        string? logoUrl = null,
        string? backgroundImageUrl = null,
        string? customCss = null)
    {
        if (primaryColor != null)
            PrimaryColor = primaryColor;

        if (secondaryColor != null)
            SecondaryColor = secondaryColor;

        if (logoUrl != null)
            LogoUrl = logoUrl;

        if (backgroundImageUrl != null)
            BackgroundImageUrl = backgroundImageUrl;

        if (customCss != null)
            CustomCss = customCss;

        UpdatedAt = DateTime.UtcNow;
    }

    public void AddSupportedLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("Language code cannot be empty", nameof(languageCode));

        if (!_supportedLanguages.Contains(languageCode))
        {
            _supportedLanguages.Add(languageCode);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void RemoveSupportedLanguage(string languageCode)
    {
        if (languageCode == DefaultLanguage)
            throw new InvalidOperationException("Cannot remove the default language");

        if (_supportedLanguages.Remove(languageCode))
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void SetDefaultLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("Language code cannot be empty", nameof(languageCode));

        // Ensure the language is supported
        if (!_supportedLanguages.Contains(languageCode))
            throw new InvalidOperationException($"Language '{languageCode}' is not in the supported languages list. Add it first.");

        DefaultLanguage = languageCode;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDescription(string? description)
    {
        if (description?.Length > 500)
            throw new ArgumentException("Description cannot exceed 500 characters", nameof(description));

        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
