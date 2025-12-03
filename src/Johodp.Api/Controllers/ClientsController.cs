namespace Johodp.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Johodp.Application.Common.Interfaces;
using Johodp.Messaging.Mediator;
using Johodp.Application.Clients.Commands;
using Johodp.Application.Clients.Queries;
using Johodp.Contracts.Clients;
using Johodp.Domain.Clients.ValueObjects;
using Johodp.Application.Common.Results;
using Johodp.Api.Extensions;

/// <summary>
/// Clients Controller - Uses Mediator pattern for CRUD business logic
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class ClientsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IClientRepository _clientRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ClientsController> _logger;

    public ClientsController(
        ISender sender,
        IClientRepository clientRepository,
        IUnitOfWork unitOfWork,
        ILogger<ClientsController> logger)
    {
        _sender = sender;
        _clientRepository = clientRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Create a new OAuth2/OIDC client
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ClientDto>> Create([FromBody] CreateClientDto dto)
    {
        _logger.LogInformation("Creating client: {ClientName}", dto.ClientName);

        var result = await _sender.Send(new CreateClientCommand { Data = dto });
        if (!result.IsSuccess)
            return result.ToActionResult();

        _logger.LogInformation("Created client {ClientId} with {TenantCount} tenant(s)",
            result.Value.Id, result.Value.AssociatedTenantIds.Count);

        return CreatedAtAction(nameof(GetClient), new { clientId = result.Value.Id }, result.Value);
    }

    /// <summary>
    /// Update an existing client
    /// </summary>
    [HttpPut("{clientId}")]
    public async Task<ActionResult<ClientDto>> Update(Guid clientId, [FromBody] UpdateClientDto dto)
    {
        _logger.LogInformation("Updating client: {ClientId}", clientId);

        var result = await _sender.Send(new UpdateClientCommand { ClientId = clientId, Data = dto });
        if (!result.IsSuccess)
            return result.ToActionResult();

        _logger.LogInformation("Updated client {ClientId}. Tenants: {TenantCount}, Active: {IsActive}",
            result.Value.Id, result.Value.AssociatedTenantIds.Count, result.Value.IsActive);

        return result.ToActionResult();
    }

    /// <summary>
    /// Get a client by ID
    /// </summary>
    [HttpGet("{clientId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDto>> GetClient(Guid clientId)
    {
        var result = await _sender.Send(new GetClientByIdQuery { ClientId = clientId });
        if (result.IsSuccess)
            _logger.LogInformation("Retrieved client: {ClientName}", result.Value.ClientName);
        return result.ToActionResult();
    }

    /// <summary>
    /// Get a client by name
    /// </summary>
    [HttpGet("by-name/{clientName}")]
    public async Task<ActionResult<ClientDto>> GetByName(string clientName)
    {
        var result = await _sender.Send(new GetClientByNameQuery { ClientName = clientName });
        if (result.IsSuccess)
            _logger.LogInformation("Retrieved client: {ClientId}", result.Value.Id);
        return result.ToActionResult();
    }

    /// <summary>
    /// Delete a client
    /// </summary>
    [HttpDelete("{clientId}")]
    public async Task<IActionResult> Delete(Guid clientId)
    {
        var deleted = await _clientRepository.DeleteAsync(ClientId.From(clientId));
        if (!deleted)
        {
            _logger.LogWarning("Client not found for deletion: {ClientId}", clientId);
            return NotFound(new { error = "Client not found" });
        }

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Deleted client: {ClientId}", clientId);
        return NoContent();
    }
}
