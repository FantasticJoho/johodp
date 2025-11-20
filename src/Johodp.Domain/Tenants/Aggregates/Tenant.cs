namespace Johodp.Domain.Tenants.Aggregates;

using Johodp.Domain.Common;
using Johodp.Domain.Tenants.ValueObjects;

/// <summary>
/// Tenant aggregate representing a multi-tenant configuration
/// </summary>
public class Tenant : AggregateRoot
{
    public TenantId Id { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Branding
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? BackgroundImageUrl { get; private set; }
    public string? CustomCss { get; private set; }

    // Languages and localization
    private readonly List<string> _supportedLanguages = new();
    public IReadOnlyList<string> SupportedLanguages => _supportedLanguages.AsReadOnly();
    public string DefaultLanguage { get; private set; } = "fr-FR";
    public string Timezone { get; private set; } = "Europe/Paris";
    public string Currency { get; private set; } = "EUR";

    // Allowed return URLs for OAuth2/OIDC
    private readonly List<string> _allowedReturnUrls = new();
    public IReadOnlyList<string> AllowedReturnUrls => _allowedReturnUrls.AsReadOnly();

    // Associated client IDs for automatic updates
    private readonly List<string> _associatedClientIds = new();
    public IReadOnlyList<string> AssociatedClientIds => _associatedClientIds.AsReadOnly();

    private Tenant() { }

    public static Tenant Create(
        string name,
        string displayName,
        string defaultLanguage = "fr-FR")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tenant name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty", nameof(displayName));

        var tenant = new Tenant
        {
            Id = TenantId.Create(),
            Name = name.ToLowerInvariant(), // Normalize tenant name
            DisplayName = displayName,
            DefaultLanguage = defaultLanguage,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Add default supported language
        tenant._supportedLanguages.Add(defaultLanguage);

        return tenant;
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

        _supportedLanguages.Remove(languageCode);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDefaultLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("Language code cannot be empty", nameof(languageCode));

        // Ensure the language is supported
        if (!_supportedLanguages.Contains(languageCode))
            AddSupportedLanguage(languageCode);

        DefaultLanguage = languageCode;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateLocalization(string? timezone = null, string? currency = null)
    {
        if (timezone != null)
            Timezone = timezone;

        if (currency != null)
            Currency = currency;

        UpdatedAt = DateTime.UtcNow;
    }

    public void AddAllowedReturnUrl(string returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            throw new ArgumentException("Return URL cannot be empty", nameof(returnUrl));

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out _))
            throw new ArgumentException("Return URL must be a valid absolute URI", nameof(returnUrl));

        if (!_allowedReturnUrls.Contains(returnUrl))
        {
            _allowedReturnUrls.Add(returnUrl);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void RemoveAllowedReturnUrl(string returnUrl)
    {
        _allowedReturnUrls.Remove(returnUrl);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddAssociatedClient(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be empty", nameof(clientId));

        if (!_associatedClientIds.Contains(clientId))
        {
            _associatedClientIds.Add(clientId);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void RemoveAssociatedClient(string clientId)
    {
        _associatedClientIds.Remove(clientId);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty", nameof(displayName));

        DisplayName = displayName;
        UpdatedAt = DateTime.UtcNow;
    }
}
