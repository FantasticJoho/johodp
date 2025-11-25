namespace Johodp.Tests.Domain;

using Xunit;
using Johodp.Domain.Tenants.Aggregates;

public class TenantAggregateTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateTenant()
    {
        // Arrange
        var name = "test-tenant";
        var displayName = "Test Tenant";
        var defaultLanguage = "fr-FR";

        // Act
        var tenant = Tenant.Create(name, displayName, defaultLanguage);

        // Assert
        Assert.NotNull(tenant);
        Assert.Equal(name, tenant.Name);
        Assert.Equal(displayName, tenant.DisplayName);
        Assert.Equal(defaultLanguage, tenant.DefaultLanguage);
        Assert.True(tenant.IsActive);
        Assert.NotEqual(Guid.Empty, tenant.Id.Value);
    }

    [Fact]
    public void UpdateBranding_ShouldSetBrandingProperties()
    {
        // Arrange
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "fr-FR");
        var primaryColor = "#FF5733";
        var secondaryColor = "#33FF57";
        var logoUrl = "https://example.com/logo.png";

        // Act
        tenant.UpdateBranding(primaryColor, secondaryColor, logoUrl, null, null);

        // Assert
        Assert.Equal(primaryColor, tenant.PrimaryColor);
        Assert.Equal(secondaryColor, tenant.SecondaryColor);
        Assert.Equal(logoUrl, tenant.LogoUrl);
    }

    [Fact]
    public void AddSupportedLanguage_ShouldAddLanguage()
    {
        // Arrange
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "fr-FR");

        // Act
        tenant.AddSupportedLanguage("en-US");
        tenant.AddSupportedLanguage("es-ES");

        // Assert
        Assert.Contains("en-US", tenant.SupportedLanguages);
        Assert.Contains("es-ES", tenant.SupportedLanguages);
        Assert.Contains("fr-FR", tenant.SupportedLanguages); // Default language should still be there
    }

    [Fact]
    public void AddSupportedLanguage_WhenAlreadyExists_ShouldNotDuplicate()
    {
        // Arrange
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "fr-FR");
        tenant.AddSupportedLanguage("en-US");

        // Act
        tenant.AddSupportedLanguage("en-US"); // Adding again

        // Assert
        var count = tenant.SupportedLanguages.Count(l => l == "en-US");
        Assert.Equal(1, count);
    }

    [Fact]
    public void RemoveSupportedLanguage_ShouldRemoveLanguage()
    {
        // Arrange
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "fr-FR");
        tenant.AddSupportedLanguage("en-US");
        tenant.AddSupportedLanguage("es-ES");

        // Act
        tenant.RemoveSupportedLanguage("en-US");

        // Assert
        Assert.DoesNotContain("en-US", tenant.SupportedLanguages);
        Assert.Contains("es-ES", tenant.SupportedLanguages);
    }

    [Fact]
    public void RemoveSupportedLanguage_WhenDefaultLanguage_ShouldThrow()
    {
        // Arrange
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "fr-FR");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => tenant.RemoveSupportedLanguage("fr-FR"));
    }

    [Fact]
    public void AddAllowedReturnUrl_ShouldAddUrl()
    {
        // Arrange
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "fr-FR");
        var returnUrl = "https://example.com/callback";

        // Act
        tenant.AddAllowedReturnUrl(returnUrl);

        // Assert
        Assert.Contains(returnUrl, tenant.AllowedReturnUrls);
    }

    [Fact]
    public void AddAllowedCorsOrigin_ShouldAddOrigin()
    {
        // Arrange
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "fr-FR");
        var origin = "https://app.example.com";

        // Act
        tenant.AddAllowedCorsOrigin(origin);

        // Assert
        Assert.Contains(origin, tenant.AllowedCorsOrigins);
    }

    [Fact]
    public void RemoveAllowedCorsOrigin_ShouldRemoveOrigin()
    {
        // Arrange
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "fr-FR");
        var origin = "https://app.example.com";
        tenant.AddAllowedCorsOrigin(origin);

        // Act
        tenant.RemoveAllowedCorsOrigin(origin);

        // Assert
        Assert.DoesNotContain(origin, tenant.AllowedCorsOrigins);
    }

    [Fact]
    public void UpdateLocalization_ShouldSetTimezoneAndCurrency()
    {
        // Arrange
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "fr-FR");

        // Act
        tenant.UpdateLocalization("Europe/Paris", "EUR");

        // Assert
        Assert.Equal("Europe/Paris", tenant.Timezone);
        Assert.Equal("EUR", tenant.Currency);
    }

    [Fact]
    public void SetClient_ShouldAssociateClient()
    {
        // Arrange
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "fr-FR");
        var clientId = "test-client";

        // Act
        tenant.SetClient(clientId);

        // Assert
        Assert.Equal(clientId, tenant.ClientId);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        // Arrange
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "fr-FR");

        // Act
        tenant.Deactivate();

        // Assert
        Assert.False(tenant.IsActive);
    }

    [Fact]
    public void Activate_ShouldSetIsActiveTrue()
    {
        // Arrange
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "fr-FR");
        tenant.Deactivate();

        // Act
        tenant.Activate();

        // Assert
        Assert.True(tenant.IsActive);
    }
}
