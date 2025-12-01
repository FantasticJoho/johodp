namespace Johodp.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Tenants.Commands;
using Johodp.Application.Tenants.Queries;
using Johodp.Application.Tenants.DTOs;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class TenantController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<TenantController> _logger;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TenantController(
        ISender sender,
        ILogger<TenantController> logger,
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork)
    {
        _sender = sender;
        _logger = logger;
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Get all tenants
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TenantDto>>> GetAll()
    {
        _logger.LogInformation("Getting all tenants");
        
        try
        {
            var tenants = await _sender.Send(new GetAllTenantsQuery());
            _logger.LogInformation("Retrieved {Count} tenants", tenants.Count());
            return Ok(tenants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all tenants");
            return StatusCode(500, "An error occurred while retrieving tenants");
        }
    }

    /// <summary>
    /// Get tenant by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TenantDto>> GetById(Guid id)
    {
        _logger.LogInformation("Getting tenant by ID: {TenantId}", id);
        
        try
        {
            var tenant = await _sender.Send(new GetTenantByIdQuery { TenantId = id });
            
            if (tenant == null)
            {
                _logger.LogWarning("Tenant not found: {TenantId}", id);
                return NotFound($"Tenant with ID '{id}' not found");
            }

            _logger.LogInformation("Retrieved tenant: {TenantName}", tenant.Name);
            return Ok(tenant);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant {TenantId}", id);
            return StatusCode(500, "An error occurred while retrieving the tenant");
        }
    }

    /// <summary>
    /// Get tenant by name
    /// </summary>
    [HttpGet("by-name/{name}")]
    public async Task<ActionResult<TenantDto>> GetByName(string name)
    {
        _logger.LogInformation("Getting tenant by name: {TenantName}", name);
        
        try
        {
            var tenant = await _sender.Send(new GetTenantByNameQuery { Name = name });
            
            if (tenant == null)
            {
                _logger.LogWarning("Tenant not found: {TenantName}", name);
                return NotFound($"Tenant with name '{name}' not found");
            }

            _logger.LogInformation("Retrieved tenant: {TenantId}", tenant.Id);
            return Ok(tenant);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant {TenantName}", name);
            return StatusCode(500, "An error occurred while retrieving the tenant");
        }
    }

    /// <summary>
    /// Create a new tenant
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TenantDto>> Create([FromBody] CreateTenantDto dto)
    {
        _logger.LogInformation("Creating new tenant: {TenantName}", dto.Name);
        
        try
        {
            var command = new CreateTenantCommand { Data = dto };
            var tenant = await _sender.Send(command);
            
            _logger.LogInformation(
                "Successfully created tenant {TenantId} with client '{ClientId}' and {UrlCount} return URLs",
                tenant.Id, tenant.ClientId ?? "(none)", tenant.AllowedReturnUrls.Count);

            return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create tenant {TenantName}: {Message}", dto.Name, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tenant {TenantName}", dto.Name);
            return StatusCode(500, "An error occurred while creating the tenant");
        }
    }

    /// <summary>
    /// Update an existing tenant
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<TenantDto>> Update(Guid id, [FromBody] UpdateTenantDto dto)
    {
        _logger.LogInformation("Updating tenant: {TenantId}", id);
        
        try
        {
            var command = new UpdateTenantCommand { TenantId = id, Data = dto };
            var tenant = await _sender.Send(command);
            
            _logger.LogInformation(
                "Successfully updated tenant {TenantId}. Client: '{ClientId}', Return URLs: {UrlCount}",
                tenant.Id, tenant.ClientId ?? "(none)", tenant.AllowedReturnUrls.Count);

            return Ok(tenant);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to update tenant {TenantId}: {Message}", id, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tenant {TenantId}", id);
            return StatusCode(500, "An error occurred while updating the tenant");
        }
    }

    /// <summary>
    /// Delete a tenant
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        _logger.LogInformation("Deleting tenant: {TenantId}", id);
        
        try
        {
            var tenantId = Johodp.Domain.Tenants.ValueObjects.TenantId.From(id);
            var deleted = await _tenantRepository.DeleteAsync(tenantId);
            
            if (!deleted)
            {
                _logger.LogWarning("Tenant not found for deletion: {TenantId}", id);
                return NotFound($"Tenant with ID '{id}' not found");
            }

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Successfully deleted tenant: {TenantId}", id);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tenant {TenantId}", id);
            return StatusCode(500, "An error occurred while deleting the tenant");
        }
    }

    /// <summary>
    /// Get branding CSS for a specific tenant
    /// </summary>
    /// <param name="tenantId">The tenant identifier</param>
    /// <returns>CSS file with tenant-specific branding variables</returns>
    [HttpGet("{tenantId}/branding.css")]
    [AllowAnonymous]
    [Produces("text/css")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBrandingFromTenantId(string tenantId)
    {
        _logger.LogInformation("Getting branding CSS for tenant: {TenantId}", tenantId);

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest("/* TenantId is required */");
        }

        try
        {
            var tenant = await _sender.Send(new GetTenantByNameQuery { Name = tenantId });
            
            if (tenant == null)
            {
                _logger.LogWarning("Tenant not found for branding: {TenantId}", tenantId);
                return NotFound("/* Tenant not found */");
            }

            // Branding is now managed via CustomConfiguration
            // Return a basic CSS or redirect to CustomConfiguration endpoint
            var css = $@"/* Branding CSS for Tenant: {tenantId} */
/* Branding is managed via CustomConfiguration. Please use the CustomConfiguration API. */

:root {{
    --primary-color: #667eea;
    --secondary-color: #764ba2;
}}
";

            return Content(css, "text/css");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving branding for tenant {TenantId}", tenantId);
            return StatusCode(500, "/* Internal server error */");
        }
    }

    /// <summary>
    /// Get language preferences for a specific tenant
    /// </summary>
    /// <param name="tenantId">The tenant identifier</param>
    /// <returns>Language and localization settings</returns>
    [HttpGet("{tenantId}/language")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLanguageFromTenantId(string tenantId)
    {
        _logger.LogInformation("Getting language settings for tenant: {TenantId}", tenantId);

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { error = "TenantId is required" });
        }

        try
        {
            var tenant = await _sender.Send(new GetTenantByNameQuery { Name = tenantId });
            
            if (tenant == null)
            {
                _logger.LogWarning("Tenant not found for language: {TenantId}", tenantId);
                return NotFound(new { error = "Tenant not found" });
            }

            // Language preferences are now managed via CustomConfiguration
            var language = new
            {
                tenantId = tenantId,
                customConfigurationId = tenant.CustomConfigurationId,
                message = "Language settings are now managed via CustomConfiguration. Please use the CustomConfiguration API."
            };

            return Ok(language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving language settings for tenant {TenantId}", tenantId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
