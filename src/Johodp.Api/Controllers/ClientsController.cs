namespace Johodp.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Clients.Commands;
using Johodp.Application.Clients.Queries;
using Johodp.Application.Clients.DTOs;
using Johodp.Domain.Clients.ValueObjects;

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
        _logger.LogInformation("Creating new client: {ClientName}", dto.ClientName);
        
        try
        {
            var command = new CreateClientCommand { Data = dto };
            var client = await _sender.Send(command);
            
            _logger.LogInformation(
                "Successfully created client {ClientId} with {UriCount} redirect URIs",
                client.Id, client.AllowedRedirectUris.Count);

            return CreatedAtAction(nameof(GetClient), new { clientId = client.Id }, client);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create client {ClientName}: {Message}", dto.ClientName, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid client data for {ClientName}: {Message}", dto.ClientName, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating client {ClientName}", dto.ClientName);
            return StatusCode(500, "An error occurred while creating the client");
        }
    }

    /// <summary>
    /// Update an existing client
    /// </summary>
    [HttpPut("{clientId}")]
    public async Task<ActionResult<ClientDto>> Update(Guid clientId, [FromBody] UpdateClientDto dto)
    {
        _logger.LogInformation("Updating client: {ClientId}", clientId);
        
        try
        {
            var command = new UpdateClientCommand { ClientId = clientId, Data = dto };
            var client = await _sender.Send(command);
            
            _logger.LogInformation(
                "Successfully updated client {ClientId}. Redirect URIs: {UriCount}, Active: {IsActive}",
                client.Id, client.AllowedRedirectUris.Count, client.IsActive);

            return Ok(client);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to update client {ClientId}: {Message}", clientId, ex.Message);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating client {ClientId}", clientId);
            return StatusCode(500, "An error occurred while updating the client");
        }
    }

    /// <summary>
    /// Get a client by ID
    /// </summary>
    [HttpGet("{clientId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDto>> GetClient(Guid clientId)
    {
        _logger.LogInformation("Getting client by ID: {ClientId}", clientId);
        
        try
        {
            var client = await _sender.Send(new GetClientByIdQuery { ClientId = clientId });
            
            if (client == null)
            {
                _logger.LogWarning("Client not found: {ClientId}", clientId);
                return NotFound(new { error = "Client not found" });
            }

            _logger.LogInformation("Retrieved client: {ClientName}", client.ClientName);
            return Ok(client);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client {ClientId}", clientId);
            return StatusCode(500, "An error occurred while retrieving the client");
        }
    }

    /// <summary>
    /// Get a client by name
    /// </summary>
    [HttpGet("by-name/{clientName}")]
    public async Task<ActionResult<ClientDto>> GetByName(string clientName)
    {
        _logger.LogInformation("Getting client by name: {ClientName}", clientName);
        
        try
        {
            var client = await _sender.Send(new GetClientByNameQuery { ClientName = clientName });
            
            if (client == null)
            {
                _logger.LogWarning("Client not found: {ClientName}", clientName);
                return NotFound(new { error = "Client not found" });
            }

            _logger.LogInformation("Retrieved client: {ClientId}", client.Id);
            return Ok(client);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client {ClientName}", clientName);
            return StatusCode(500, "An error occurred while retrieving the client");
        }
    }

    /// <summary>
    /// Delete a client
    /// </summary>
    [HttpDelete("{clientId}")]
    public async Task<IActionResult> Delete(Guid clientId)
    {
        _logger.LogInformation("Deleting client: {ClientId}", clientId);
        
        try
        {
            var deleted = await _clientRepository.DeleteAsync(ClientId.From(clientId));
            
            if (!deleted)
            {
                _logger.LogWarning("Client not found for deletion: {ClientId}", clientId);
                return NotFound(new { error = "Client not found" });
            }

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Successfully deleted client: {ClientId}", clientId);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting client {ClientId}", clientId);
            return StatusCode(500, "An error occurred while deleting the client");
        }
    }

    /// <summary>
    /// Add a redirect URI to a client
    /// </summary>
    [HttpPost("{clientId}/redirect-uris")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddRedirectUri(Guid clientId, [FromBody] AddRedirectUriRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RedirectUri))
            return BadRequest(new { error = "RedirectUri is required" });

        var client = await _clientRepository.GetByIdAsync(ClientId.From(clientId));
        
        if (client == null)
            return NotFound(new { error = "Client not found" });

        try
        {
            client.AddRedirectUri(request.RedirectUri);
            await _clientRepository.UpdateAsync(client);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Added redirect URI {Uri} to client {ClientId}", request.RedirectUri, clientId);

            return Ok(new
            {
                message = "Redirect URI added successfully",
                allowedRedirectUris = client.AllowedRedirectUris
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove a redirect URI from a client
    /// </summary>
    [HttpDelete("{clientId}/redirect-uris")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRedirectUri(Guid clientId, [FromBody] RemoveRedirectUriRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RedirectUri))
            return BadRequest(new { error = "RedirectUri is required" });

        var client = await _clientRepository.GetByIdAsync(ClientId.From(clientId));
        
        if (client == null)
            return NotFound(new { error = "Client not found" });

        try
        {
            client.RemoveRedirectUri(request.RedirectUri);
            await _clientRepository.UpdateAsync(client);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Removed redirect URI {Uri} from client {ClientId}", request.RedirectUri, clientId);

            return Ok(new
            {
                message = "Redirect URI removed successfully",
                allowedRedirectUris = client.AllowedRedirectUris
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record AddRedirectUriRequest(string RedirectUri);
public record RemoveRedirectUriRequest(string RedirectUri);
