using System.Net.Http.Json;
using Bogus;
using Johodp.Contracts.Clients;
using Johodp.Contracts.CustomConfigurations;
using Johodp.Contracts.Tenants;
using Johodp.Contracts.Users;
using Xunit;

namespace Johodp.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests with common setup and helper methods.
/// Follows the complete-workflow.http file structure.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<JohodpWebApplicationFactory>, IAsyncLifetime
{
    protected readonly JohodpWebApplicationFactory Factory;
    protected readonly HttpClient Client;
    protected readonly Faker Faker = new();

    // Workflow state (like variables in .http file)
    protected string? ClientId;
    protected string? ClientName;
    protected string? CustomConfigId;
    protected string? CustomConfigName;
    protected string? TenantId;
    protected string? TenantName;
    protected string? UserId;
    protected string? ActivationToken;

    protected IntegrationTestBase(JohodpWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;
    public virtual Task DisposeAsync() => Task.CompletedTask;

    #region Helper Methods - Following complete-workflow.http

    /// <summary>
    /// STEP 1: Create OAuth2 Client
    /// </summary>
    protected async Task<ClientDto> CreateClientAsync(
        string? clientName = null,
        bool requireMfa = false)
    {
        var request = new CreateClientDto
        {
            ClientName = clientName ?? $"test-client-{Guid.NewGuid()}",
            AllowedScopes = new List<string> { "openid", "profile", "email", "johodp.identity", "johodp.api", "offline_access" },
            RequireConsent = false,
            RequireMfa = requireMfa
        };

        var response = await Client.PostAsJsonAsync("/api/clients", request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Request failed with {response.StatusCode}. Response: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<ClientDto>();
        
        ClientId = result!.Id.ToString();
        ClientName = result.ClientName;
        
        return result;
    }

    /// <summary>
    /// STEP 2: Create CustomConfiguration
    /// </summary>
    protected async Task<CustomConfigurationDto> CreateCustomConfigurationAsync(
        string? name = null)
    {
        var request = new CreateCustomConfigurationDto
        {
            Name = name ?? $"test-config-{Guid.NewGuid()}",
            Description = "Test branding configuration",
            PrimaryColor = "#007bff",
            SecondaryColor = "#6c757d",
            LogoUrl = "https://example.com/logo.png",
            DefaultLanguage = "en-US",
            AdditionalLanguages = new List<string> { "fr-FR" }
        };

        var response = await Client.PostAsJsonAsync("/api/custom-configurations", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CustomConfigurationDto>();
        
        CustomConfigId = result!.Id.ToString();
        CustomConfigName = result.Name;
        
        return result;
    }

    /// <summary>
    /// STEP 3: Create Tenant
    /// </summary>
    protected async Task<TenantDto> CreateTenantAsync(
        string? tenantName = null,
        string? clientId = null,
        string? customConfigId = null)
    {
        // Ensure dependencies exist
        clientId ??= ClientId ?? (await CreateClientAsync()).Id.ToString();
        customConfigId ??= CustomConfigId ?? (await CreateCustomConfigurationAsync()).Id.ToString();

        var request = new CreateTenantDto
        {
            Name = tenantName ?? $"test-tenant-{Guid.NewGuid().ToString()[..8]}",
            DisplayName = "Test Tenant Corporation",
            ClientId = clientId,
            CustomConfigurationId = Guid.Parse(customConfigId),
            AllowedReturnUrls = new List<string> { "http://localhost:4200/callback" },
            AllowedCorsOrigins = new List<string> { "http://localhost:4200" }
        };

        var response = await Client.PostAsJsonAsync("/api/tenants", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TenantDto>();
        
        TenantId = result!.Id.ToString();
        TenantName = result.Name;
        
        return result;
    }

    /// <summary>
    /// STEP 7: Create User (External app approval) - Using /api/auth/register endpoint
    /// </summary>
    protected async Task<RegisterUserResponse> CreateUserAsync(
        string? email = null,
        string? tenantName = null,
        string? firstName = null,
        string? lastName = null)
    {
        // Ensure tenant exists
        tenantName ??= TenantName ?? (await CreateTenantAsync()).Name;

        var request = new RegisterRequest
        {
            Email = email ?? Faker.Internet.Email(),
            FirstName = firstName ?? Faker.Name.FirstName(),
            LastName = lastName ?? Faker.Name.LastName(),
            TenantName = tenantName
        };

        var response = await Client.PostAsJsonAsync("/api/auth/register", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RegisterUserResponse>();
        
        UserId = result!.UserId.ToString();
        
        // Note: In real flow, activation token would be sent via email
        // For tests, we'll need to mock or use a test endpoint to get the token
        
        return result;
    }

    /// <summary>
    /// STEP 9: Activate User Account
    /// </summary>
    protected async Task<ActivateResponse> ActivateUserAsync(
        string? userId = null,
        string? token = null,
        string password = "SecureP@ssw0rd123!")
    {
        userId ??= UserId ?? throw new InvalidOperationException("UserId is required");
        token ??= ActivationToken ?? throw new InvalidOperationException("ActivationToken is required");

        var request = new ActivateRequest
        {
            UserId = userId,
            Token = token,
            NewPassword = password,
            ConfirmPassword = password
        };

        var response = await Client.PostAsJsonAsync("/api/auth/activate", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ActivateResponse>();
        return result ?? throw new InvalidOperationException("Failed to deserialize activation response");
    }

    /// <summary>
    /// STEP 10: Login User
    /// </summary>
    protected async Task<LoginResponse> LoginUserAsync(
        string email,
        string password,
        string? tenantName = null)
    {
        tenantName ??= TenantName ?? throw new InvalidOperationException("TenantName is required");

        var request = new LoginRequest
        {
            Email = email,
            Password = password,
            TenantName = tenantName
        };

        var response = await Client.PostAsJsonAsync("/api/auth/login", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return result ?? throw new InvalidOperationException("Failed to deserialize login response");
    }

    /// <summary>
    /// Complete workflow: Create all entities and activate user
    /// </summary>
    protected async Task<CompleteWorkflowResult> ExecuteCompleteWorkflowAsync(
        bool requireMfa = false,
        string userPassword = "SecureP@ssw0rd123!")
    {
        // Step 1: Create Client
        var client = await CreateClientAsync(requireMfa: requireMfa);

        // Step 2: Create CustomConfiguration
        var customConfig = await CreateCustomConfigurationAsync();

        // Step 3: Create Tenant
        var tenant = await CreateTenantAsync(
            clientId: client.Id.ToString(),
            customConfigId: customConfig.Id.ToString());

        // Step 7: Create User
        var userEmail = Faker.Internet.Email();
        var user = await CreateUserAsync(
            email: userEmail,
            tenantName: tenant.Name);

        // Step 9: Activate User (Note: requires activation token from email)
        // For integration tests, activation would need to be mocked or use a test endpoint

        return new CompleteWorkflowResult
        {
            Client = client,
            CustomConfig = customConfig,
            Tenant = tenant,
            User = user,
            UserEmail = userEmail,
            UserPassword = userPassword
        };
    }

    #endregion
}

/// <summary>
/// Result of complete workflow execution
/// </summary>
public record CompleteWorkflowResult
{
    public required ClientDto Client { get; init; }
    public required CustomConfigurationDto CustomConfig { get; init; }
    public required TenantDto Tenant { get; init; }
    public required RegisterUserResponse User { get; init; }
    public required string UserEmail { get; init; }
    public required string UserPassword { get; init; }
}

// Request/Response models matching AccountController
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? TenantName { get; set; }
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
}

public class ActivateRequest
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Message { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class ActivateResponse
{
    public string Message { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}
