namespace Johodp.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Johodp.Application.Common.Interfaces;
using Johodp.Application.Tenants.Commands;
using Johodp.Application.Tenants.Queries;
using Johodp.Application.Tenants.DTOs;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantController : ControllerBase
{
    private readonly ILogger<TenantController> _logger;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreateTenantCommandHandler _createTenantHandler;
    private readonly UpdateTenantCommandHandler _updateTenantHandler;
    private readonly GetTenantByIdQueryHandler _getTenantByIdHandler;
    private readonly GetAllTenantsQueryHandler _getAllTenantsHandler;
    private readonly GetTenantByNameQueryHandler _getTenantByNameHandler;

    public TenantController(
        ILogger<TenantController> logger,
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        CreateTenantCommandHandler createTenantHandler,
        UpdateTenantCommandHandler updateTenantHandler,
        GetTenantByIdQueryHandler getTenantByIdHandler,
        GetAllTenantsQueryHandler getAllTenantsHandler,
        GetTenantByNameQueryHandler getTenantByNameHandler)
    {
        _logger = logger;
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
        _createTenantHandler = createTenantHandler;
        _updateTenantHandler = updateTenantHandler;
        _getTenantByIdHandler = getTenantByIdHandler;
        _getAllTenantsHandler = getAllTenantsHandler;
        _getTenantByNameHandler = getTenantByNameHandler;
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
            var tenants = await _getAllTenantsHandler.Handle(new GetAllTenantsQuery());
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
            var tenant = await _getTenantByIdHandler.Handle(new GetTenantByIdQuery { TenantId = id });
            
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
            var tenant = await _getTenantByNameHandler.Handle(new GetTenantByNameQuery { Name = name });
            
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
            var tenant = await _createTenantHandler.Handle(command);
            
            _logger.LogInformation(
                "Successfully created tenant {TenantId} with {ClientCount} associated clients and {UrlCount} return URLs",
                tenant.Id, tenant.AssociatedClientIds.Count, tenant.AllowedReturnUrls.Count);

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
            var tenant = await _updateTenantHandler.Handle(command);
            
            _logger.LogInformation(
                "Successfully updated tenant {TenantId}. Associated clients: {ClientCount}, Return URLs: {UrlCount}",
                tenant.Id, tenant.AssociatedClientIds.Count, tenant.AllowedReturnUrls.Count);

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
            var tenant = await _getTenantByNameHandler.Handle(new GetTenantByNameQuery { Name = tenantId });
            
            if (tenant == null)
            {
                _logger.LogWarning("Tenant not found for branding: {TenantId}", tenantId);
                return NotFound("/* Tenant not found */");
            }

            var css = $@"/* Branding CSS for Tenant: {tenantId} */

:root {{
    --primary-color: {tenant.PrimaryColor ?? "#667eea"};
    --secondary-color: {tenant.SecondaryColor ?? "#764ba2"};
    --font-primary-color: #333333;
    --font-secondary-color: #666666;
    --logo-base64: url('{tenant.LogoUrl ?? "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="}');
    --image-base64: url('{tenant.BackgroundImageUrl ?? "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="}');
}}

{tenant.CustomCss ?? @"/* Apply branding */
body {
    background: linear-gradient(135deg, var(--primary-color) 0%, var(--secondary-color) 100%);
    color: var(--font-primary-color);
}

.login-logo {
    background-image: var(--logo-base64);
    background-size: contain;
    background-repeat: no-repeat;
    background-position: center;
}

.btn-primary {
    background-color: var(--primary-color);
    border-color: var(--primary-color);
    color: #ffffff;
}

.btn-primary:hover {
    background-color: var(--secondary-color);
    border-color: var(--secondary-color);
}"}
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
            var tenant = await _getTenantByNameHandler.Handle(new GetTenantByNameQuery { Name = tenantId });
            
            if (tenant == null)
            {
                _logger.LogWarning("Tenant not found for language: {TenantId}", tenantId);
                return NotFound(new { error = "Tenant not found" });
            }

            var language = new
            {
                tenantId = tenantId,
                defaultLanguage = tenant.DefaultLanguage,
                supportedLanguages = tenant.SupportedLanguages,
                dateFormat = "dd/MM/yyyy",
                timeFormat = "HH:mm",
                timezone = tenant.Timezone,
                currency = tenant.Currency
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
