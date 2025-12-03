using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Johodp.Contracts.Clients;
using Johodp.IntegrationTests.Infrastructure;
using Xunit;

namespace Johodp.IntegrationTests.WorkflowTests;

/// <summary>
/// Integration tests for Client API endpoints (Step 1 from complete-workflow.http).
/// Tests client creation, retrieval, update, and management operations.
/// </summary>
/// <remarks>
/// TODO - Tests restants à implémenter:
/// 1. Implémenter l'endpoint GET /api/clients pour lister tous les clients
/// 2. Activer le test GetAllClients_ShouldReturnAllCreatedClients une fois l'endpoint implémenté
/// </remarks>
public class ClientApiTests : IntegrationTestBase
{
    public ClientApiTests(JohodpWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateClient_ShouldReturnCreated_WithValidRequest()
    {
        // Arrange
        var request = new CreateClientDto
        {
            ClientName = $"test-client-{Guid.NewGuid()}",
            AllowedScopes = new List<string> { "openid", "profile", "email" },
            RequireConsent = false,
            RequireMfa = false
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/clients", request);

        // Assert
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Request failed with {response.StatusCode}. Response: {errorContent}");
        }
        
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content.ReadFromJsonAsync<ClientDto>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
        result.ClientName.Should().Be(request.ClientName);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetClientById_ShouldReturnClient_WhenExists()
    {
        // Arrange
        var created = await CreateClientAsync();

        // Act
        var response = await Client.GetAsync($"/api/clients/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ClientDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.ClientName.Should().Be(created.ClientName);
    }

    [Fact]
    public async Task GetClientById_ShouldReturnNotFound_WhenDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();

        // Act
        var response = await Client.GetAsync($"/api/clients/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateClient_ShouldModifyClient_WhenValid()
    {
        // Arrange
        var created = await CreateClientAsync();
        var updateRequest = new UpdateClientDto
        {
            AllowedScopes = new List<string> { "openid", "profile", "email", "custom.scope" },
            RequireConsent = true,
            RequireMfa = true,
            IsActive = true
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/clients/{created.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ClientDto>();
        result.Should().NotBeNull();
        result!.AllowedScopes.Should().Contain("custom.scope");
        result.RequireMfa.Should().BeTrue();
        result.RequireConsent.Should().BeTrue();
    }

    // TODO: Implement GET /api/clients endpoint first
    // [Fact]
    // public async Task GetAllClients_ShouldReturnAllCreatedClients()
    // {
    //     // Arrange
    //     await CreateClientAsync();
    //     await CreateClientAsync();
    //     await CreateClientAsync();
    //
    //     // Act
    //     var response = await Client.GetAsync("/api/clients");
    //
    //     // Assert
    //     response.StatusCode.Should().Be(HttpStatusCode.OK);
    //     
    //     var result = await response.Content.ReadFromJsonAsync<List<ClientDto>>();
    //     result.Should().NotBeNull();
    //     result!.Should().HaveCountGreaterThanOrEqualTo(3);
    // }

    [Fact]
    public async Task CreateClient_WithMfaRequired_ShouldSetFlagCorrectly()
    {
        // Arrange & Act
        var client = await CreateClientAsync(requireMfa: true);

        // Assert
        client.RequireMfa.Should().BeTrue();
    }
}
