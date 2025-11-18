namespace Johodp.Api.Controllers;

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class TenantController : ControllerBase
{
    private readonly ILogger<TenantController> _logger;

    public TenantController(ILogger<TenantController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get branding CSS for a specific tenant
    /// </summary>
    /// <param name="tenantId">The tenant identifier</param>
    /// <returns>CSS file with tenant-specific branding variables</returns>
    [HttpGet("{tenantId}/branding.css")]
    [Produces("text/css")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetBrandingFromTenantId(string tenantId)
    {
        _logger.LogInformation("Getting branding CSS for tenant: {TenantId}", tenantId);

        // TODO: Replace with actual database lookup
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest("/* TenantId is required */");
        }

        // TODO: Fetch from database
        var primaryColor = "#667eea";
        var secondaryColor = "#764ba2";
        var fontPrimaryColor = "#333333";
        var fontSecondaryColor = "#666666";
        
        // Mock base64 images (1x1 transparent pixel for demo)
        var logoBase64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var imageBase64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

        // Generate CSS with tenant-specific branding
        var css = $@"/* Branding CSS for Tenant: {tenantId} */

:root {{
    --primary-color: {primaryColor};
    --secondary-color: {secondaryColor};
    --font-primary-color: {fontPrimaryColor};
    --font-secondary-color: {fontSecondaryColor};
    --logo-base64: url('{logoBase64}');
    --image-base64: url('{imageBase64}');
}}

/* Apply branding */
body {{
    background: linear-gradient(135deg, var(--primary-color) 0%, var(--secondary-color) 100%);
    color: var(--font-primary-color);
}}

.login-logo {{
    background-image: var(--logo-base64);
    background-size: contain;
    background-repeat: no-repeat;
    background-position: center;
}}

.login-background {{
    background-image: var(--image-base64);
    background-size: cover;
    background-position: center;
}}

.btn-primary {{
    background-color: var(--primary-color);
    border-color: var(--primary-color);
    color: #ffffff;
}}

.btn-primary:hover {{
    background-color: var(--secondary-color);
    border-color: var(--secondary-color);
}}

.text-primary {{
    color: var(--font-primary-color);
}}

.text-secondary {{
    color: var(--font-secondary-color);
}}

a {{
    color: var(--primary-color);
}}

a:hover {{
    color: var(--secondary-color);
}}
";

        return Content(css, "text/css");
    }

    /// <summary>
    /// Get language preferences for a specific tenant
    /// </summary>
    /// <param name="tenantId">The tenant identifier</param>
    /// <returns>Language and localization settings</returns>
    [HttpGet("{tenantId}/language")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetLanguageFromTenantId(string tenantId)
    {
        _logger.LogInformation("Getting language settings for tenant: {TenantId}", tenantId);

        // TODO: Replace with actual database lookup
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { error = "TenantId is required" });
        }

        // Mock language data
        var language = new
        {
            tenantId = tenantId,
            defaultLanguage = "fr-FR",
            supportedLanguages = new[] { "fr-FR", "en-US", "es-ES" },
            dateFormat = "dd/MM/yyyy",
            timeFormat = "HH:mm",
            timezone = "Europe/Paris",
            currency = "EUR"
        };

        return Ok(language);
    }
}
