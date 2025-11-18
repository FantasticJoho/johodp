namespace Johodp.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Clients.ValueObjects;

[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly IClientRepository _clientRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ClientsController> _logger;

    public ClientsController(
        IClientRepository clientRepository,
        IUnitOfWork unitOfWork,
        ILogger<ClientsController> logger)
    {
        _clientRepository = clientRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Get a client by ID
    /// </summary>
    [HttpGet("{clientId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClient(Guid clientId)
    {
        var client = await _clientRepository.GetByIdAsync(ClientId.From(clientId));
        
        if (client == null)
            return NotFound(new { error = "Client not found" });

        return Ok(new
        {
            id = client.Id.Value,
            clientName = client.ClientName,
            allowedScopes = client.AllowedScopes,
            allowedRedirectUris = client.AllowedRedirectUris,
            allowedCorsOrigins = client.AllowedCorsOrigins,
            requireClientSecret = client.RequireClientSecret,
            requireConsent = client.RequireConsent,
            isActive = client.IsActive,
            createdAt = client.CreatedAt
        });
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
