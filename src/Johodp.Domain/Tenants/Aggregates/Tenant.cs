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

    // Notification configuration (pour l'application tierce)
    public string? NotificationUrl { get; private set; }
    public string? ApiKey { get; private set; }
    public bool NotifyOnAccountRequest { get; private set; }

    // Languages and localization
    private readonly List<string> _supportedLanguages = new();
    public IReadOnlyList<string> SupportedLanguages => _supportedLanguages.AsReadOnly();
    public string DefaultLanguage { get; private set; } = "fr-FR";
    public string Timezone { get; private set; } = "Europe/Paris";
    public string Currency { get; private set; } = "EUR";

    // URLs associated with this tenant (e.g., "acme-corp-example-com", "acme-corp-fr")
    private readonly List<string> _urls = new();
    public IReadOnlyList<string> Urls => _urls.AsReadOnly();

    // Allowed return URLs for OAuth2/OIDC
    private readonly List<string> _allowedReturnUrls = new();
    public IReadOnlyList<string> AllowedReturnUrls => _allowedReturnUrls.AsReadOnly();

    // Allowed CORS origins for this tenant's frontend application
    private readonly List<string> _allowedCorsOrigins = new();
    public IReadOnlyList<string> AllowedCorsOrigins => _allowedCorsOrigins.AsReadOnly();

    // Associated client (a tenant can only be associated with one client)
    public string? ClientId { get; private set; }

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

    public void AddUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be empty", nameof(url));

        var normalizedUrl = url.ToLowerInvariant().Trim();
        if (!_urls.Contains(normalizedUrl))
        {
            _urls.Add(normalizedUrl);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void RemoveUrl(string url)
    {
        var normalizedUrl = url.ToLowerInvariant().Trim();
        _urls.Remove(normalizedUrl);
        UpdatedAt = DateTime.UtcNow;
    }

    public bool HasUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var normalizedUrl = url.ToLowerInvariant().Trim();
        return _urls.Contains(normalizedUrl);
    }

    public bool IsValidForAcrValue(string acrValue)
    {
        if (string.IsNullOrWhiteSpace(acrValue))
            return false;

        var normalizedAcrValue = acrValue.ToLowerInvariant().Trim();
        
        // Check if acr_value matches any tenant URL
        if (_urls.Contains(normalizedAcrValue))
            return true;

        // Check if acr_value is part of any allowed return URL
        return _allowedReturnUrls.Any(returnUrl => 
        {
            if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri))
            {
                var host = uri.Host.Replace(".", "-").ToLowerInvariant();
                return host == normalizedAcrValue || normalizedAcrValue.Contains(host);
            }
            return false;
        });
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

    public void AddAllowedCorsOrigin(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            throw new ArgumentException("CORS origin cannot be empty", nameof(origin));

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            throw new ArgumentException("CORS origin must be a valid absolute URI", nameof(origin));

        // Validate that it's just origin (no path)
        if (!string.IsNullOrEmpty(uri.PathAndQuery) && uri.PathAndQuery != "/")
            throw new ArgumentException("CORS origin should not contain a path", nameof(origin));

        var normalizedOrigin = uri.GetLeftPart(UriPartial.Authority);
        if (!_allowedCorsOrigins.Contains(normalizedOrigin))
        {
            _allowedCorsOrigins.Add(normalizedOrigin);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void RemoveAllowedCorsOrigin(string origin)
    {
        _allowedCorsOrigins.Remove(origin);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetClient(string? clientId)
    {
        if (clientId != null && string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be empty", nameof(clientId));

        ClientId = clientId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveClient()
    {
        ClientId = null;
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

    public void ConfigureNotifications(string url, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Notification URL cannot be empty", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            throw new ArgumentException("Notification URL must be a valid absolute URI", nameof(url));

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be empty", nameof(apiKey));

        NotificationUrl = url;
        ApiKey = apiKey;
        NotifyOnAccountRequest = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DisableNotifications()
    {
        NotifyOnAccountRequest = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RegenerateApiKey()
    {
        ApiKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        UpdatedAt = DateTime.UtcNow;
    }
}
