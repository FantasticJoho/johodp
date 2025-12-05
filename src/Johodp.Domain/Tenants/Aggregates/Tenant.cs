namespace Johodp.Domain.Tenants.Aggregates;

using Johodp.Domain.Common;
using Johodp.Domain.Tenants.ValueObjects;
using Johodp.Domain.Clients.ValueObjects;
using Johodp.Domain.CustomConfigurations.ValueObjects;

/// <summary>
/// Tenant aggregate root - represents an isolated customer/organization in the multi-tenant system.
/// Each tenant has its own branding, configuration, users, and URLs.
/// </summary>
/// <remarks>
/// <para><strong>Multi-Tenancy Architecture:</strong></para>
/// <list type="bullet">
/// <item>Tenants provide data isolation for different customers/organizations</item>
/// <item>Each tenant has unique URLs for accessing the application (e.g., "acme-corp-example-com")</item>
/// <item>Users belong to exactly one tenant (no cross-tenant access)</item>
/// <item>Tenants can share a Client for OAuth2/OIDC authentication</item>
/// <item>Each tenant references one CustomConfiguration for branding/localization</item>
/// </list>
/// 
/// <para><strong>Business Rules:</strong></para>
/// <list type="bullet">
/// <item>Tenant name must be unique, lowercase, alphanumeric with hyphens only</item>
/// <item>Maximum 100 characters for name, 200 for display name</item>
/// <item>CustomConfigurationId is required (cannot create tenant without branding)</item>
/// <item>Tenants can have multiple URLs and CORS origins</item>
/// <item>Notification configuration (webhook) is optional</item>
/// </list>
/// 
/// <para><strong>URL Format Examples:</strong></para>
/// <code>
/// "acme-corp-example-com"  // Production: https://acme-corp.example.com
/// "acme-corp-fr"           // French locale: https://acme-corp.fr
/// "dev-acme-corp"          // Development environment
/// </code>
/// </remarks>
public class Tenant : AggregateRoot
{
    public TenantId Id { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Reference to CustomConfiguration for branding and localization (required)
    public CustomConfigurationId CustomConfigurationId { get; private set; } = null!;

    // Notification configuration (pour l'application tierce)
    public string? NotificationUrl { get; private set; }
    public string? ApiKey { get; private set; }
    public bool NotifyOnAccountRequest { get; private set; }

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
    public ClientId? ClientId { get; private set; }

    private Tenant() { }

    /// <summary>
    /// Factory method to create a new Tenant.
    /// Validates name format and associates required custom configuration.
    /// </summary>
    /// <param name="name">Unique tenant name (lowercase, alphanumeric, hyphens only, max 100 chars)</param>
    /// <param name="displayName">Human-readable tenant name (max 200 chars)</param>
    /// <param name="customConfigurationId">Required reference to branding/localization configuration</param>
    /// <returns>New Tenant instance in Active status</returns>
    /// <exception cref="ArgumentNullException">Thrown when customConfigurationId is null</exception>
    /// <exception cref="ArgumentException">Thrown when validation fails (empty name, invalid format, length exceeded)</exception>
    public static Tenant Create(
        string name,
        string displayName,
        CustomConfigurationId customConfigurationId)
    {
        if (customConfigurationId == null)
            throw new ArgumentNullException(nameof(customConfigurationId), "CustomConfigurationId is required");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tenant name cannot be empty", nameof(name));
        
        if (name.Length > 100)
            throw new ArgumentException("Tenant name cannot exceed 100 characters", nameof(name));
        
        // Validate tenant name format (lowercase alphanumeric and hyphens only)
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-z0-9-]+$"))
            throw new ArgumentException("Tenant name must contain only lowercase letters, numbers, and hyphens", nameof(name));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty", nameof(displayName));
        
        if (displayName.Length > 200)
            throw new ArgumentException("Display name cannot exceed 200 characters", nameof(displayName));

        var tenant = new Tenant
        {
            Id = TenantId.Create(),
            Name = name.ToLowerInvariant(), // Normalize tenant name
            DisplayName = displayName,
            CustomConfigurationId = customConfigurationId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        return tenant;
    }

    public void SetCustomConfiguration(CustomConfigurationId customConfigurationId)
    {
        if (customConfigurationId == null)
            throw new ArgumentNullException(nameof(customConfigurationId), "CustomConfigurationId is required");

        CustomConfigurationId = customConfigurationId;
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

    public void SetClient(ClientId? clientId)
    {
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
