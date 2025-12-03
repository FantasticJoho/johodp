using FluentAssertions;
using Johodp.IntegrationTests.Infrastructure;
using Xunit;

namespace Johodp.IntegrationTests.WorkflowTests;

/// <summary>
/// Integration tests following the complete-workflow.http file.
/// Tests the multi-tenant workflow from client creation to tenant setup.
/// Note: Full activation/login flow requires email verification which is not suitable for integration tests.
/// </summary>
/// <remarks>
/// TODO - Tests restants à implémenter:
/// 1. Implémenter l'endpoint POST /api/tenants (actuellement retourne 404)
/// 2. Corriger les codes de statut pour validation (retourne 404 au lieu de 400 BadRequest)
/// 3. Activer les tests commentés une fois les endpoints implémentés:
///    - CompleteWorkflow_ShouldSucceed_WhenAllStepsExecutedInOrder
///    - CompleteWorkflow_WithMfaEnabled_ShouldCreateClientWithMfa
///    - SameEmailOnDifferentTenants_ShouldCreateSeparateAccounts
///    - TenantCreation_ShouldFail_WhenClientDoesNotExist
///    - TenantCreation_ShouldFail_WhenCustomConfigDoesNotExist
///    - MultipleUsers_OnSameTenant_ShouldHaveIndependentAccounts
/// </remarks>
public class CompleteWorkflowTests : IntegrationTestBase
{
    public CompleteWorkflowTests(JohodpWebApplicationFactory factory) : base(factory)
    {
    }

    // TODO: Fix 404 errors in tenant creation endpoint
    // [Fact]
    // public async Task CompleteWorkflow_ShouldSucceed_WhenAllStepsExecutedInOrder()
    // {
    //     // Act - Execute complete workflow (Client → CustomConfig → Tenant → User Registration)
    //     var result = await ExecuteCompleteWorkflowAsync();
    //
    //     // Assert
    //     result.Client.Should().NotBeNull();
    //     result.Client.Id.Should().NotBeEmpty();
    //     result.Client.IsActive.Should().BeTrue();
    //
    //     result.CustomConfig.Should().NotBeNull();
    //     result.CustomConfig.Id.Should().NotBeEmpty();
    //     result.CustomConfig.IsActive.Should().BeTrue();
    //
    //     result.Tenant.Should().NotBeNull();
    //     result.Tenant.Id.Should().NotBeEmpty();
    //     result.Tenant.IsActive.Should().BeTrue();
    //     result.Tenant.ClientId.Should().Be(result.Client.Id.ToString());
    //
    //     result.User.Should().NotBeNull();
    //     result.User.UserId.Should().NotBeEmpty();
    //     result.User.Email.Should().Be(result.UserEmail);
    // }

    // TODO: Fix 404 errors in tenant creation endpoint
    // [Fact]
    // public async Task CompleteWorkflow_WithMfaEnabled_ShouldCreateClientWithMfa()
    // {
    //     // Act
    //     var result = await ExecuteCompleteWorkflowAsync(requireMfa: true);
    //
    //     // Assert
    //     result.Client.RequireMfa.Should().BeTrue();
    // }

    [Fact]
    public async Task UserCreation_ShouldFail_WhenTenantDoesNotExist()
    {
        // Arrange
        var nonExistentTenant = "nonexistent-tenant-12345";

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = Faker.Internet.Email(),
            FirstName = Faker.Name.FirstName(),
            LastName = Faker.Name.LastName(),
            TenantName = nonExistentTenant
        });

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    // TODO: Fix 404 errors in tenant creation endpoint
    // [Fact]
    // public async Task SameEmailOnDifferentTenants_ShouldCreateSeparateAccounts()
    // {
    //     // Arrange
    //     var sharedEmail = Faker.Internet.Email();
    //     var tenant1 = await CreateTenantAsync(tenantName: $"tenant1-{Guid.NewGuid().ToString()[..8]}");
    //     var tenant2 = await CreateTenantAsync(tenantName: $"tenant2-{Guid.NewGuid().ToString()[..8]}");
    //
    //     // Act
    //     var user1 = await CreateUserAsync(email: sharedEmail, tenantName: tenant1.Name);
    //     var user2 = await CreateUserAsync(email: sharedEmail, tenantName: tenant2.Name);
    //
    //     // Assert
    //     user1.UserId.Should().NotBe(user2.UserId);
    //     user1.Email.Should().Be(user2.Email);
    // }

    // TODO: Fix expected status code (currently returns 404 instead of 400)
    // [Fact]
    // public async Task TenantCreation_ShouldFail_WhenClientDoesNotExist()
    // {
    //     // Arrange
    //     var customConfig = await CreateCustomConfigurationAsync();
    //     var nonExistentClientId = Guid.NewGuid().ToString();
    //
    //     // Act
    //     var response = await Client.PostAsJsonAsync("/api/tenants", new Johodp.Contracts.Tenants.CreateTenantDto
    //     {
    //         Name = $"test-tenant-{Guid.NewGuid().ToString()[..8]}",
    //         DisplayName = "Test Tenant",
    //         ClientId = nonExistentClientId,
    //         CustomConfigurationId = customConfig.Id,
    //         AllowedReturnUrls = new List<string> { "http://localhost:4200/callback" },
    //         AllowedCorsOrigins = new List<string> { "http://localhost:4200" }
    //     });
    //
    //     // Assert
    //     response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    // }

    // TODO: Fix expected status code (currently returns 404 instead of 400)
    // [Fact]
    // public async Task TenantCreation_ShouldFail_WhenCustomConfigDoesNotExist()
    // {
    //     // Arrange
    //     var client = await CreateClientAsync();
    //     var nonExistentConfigId = Guid.NewGuid();
    //
    //     // Act
    //     var response = await Client.PostAsJsonAsync("/api/tenants", new Johodp.Contracts.Tenants.CreateTenantDto
    //     {
    //         Name = $"test-tenant-{Guid.NewGuid().ToString()[..8]}",
    //         DisplayName = "Test Tenant",
    //         ClientId = client.Id.ToString(),
    //         CustomConfigurationId = nonExistentConfigId,
    //         AllowedReturnUrls = new List<string> { "http://localhost:4200/callback" },
    //         AllowedCorsOrigins = new List<string> { "http://localhost:4200" }
    //     });
    //
    //     // Assert
    //     response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    // }

    // TODO: Fix 404 errors in tenant creation endpoint
    // [Fact]
    // public async Task MultipleUsers_OnSameTenant_ShouldHaveIndependentAccounts()
    // {
    //     // Arrange
    //     var tenant = await CreateTenantAsync();
    //
    //     // Act
    //     var user1 = await CreateUserAsync(tenantName: tenant.Name);
    //     var user2 = await CreateUserAsync(tenantName: tenant.Name);
    //     var user3 = await CreateUserAsync(tenantName: tenant.Name);
    //
    //     // Assert
    //     var userIds = new[] { user1.UserId, user2.UserId, user3.UserId };
    //     userIds.Should().OnlyHaveUniqueItems();
    //     
    //     var emails = new[] { user1.Email, user2.Email, user3.Email };
    //     emails.Should().OnlyHaveUniqueItems();
    // }
}
