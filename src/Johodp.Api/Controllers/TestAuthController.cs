using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Johodp.Api.Controllers;

/// <summary>
/// Test controller for OAuth2/OIDC flow testing
/// FOR DEVELOPMENT ONLY - Remove in production
/// </summary>
[ApiController]
[Route("test")]
[AllowAnonymous]
public class TestAuthController : ControllerBase
{
    private readonly ILogger<TestAuthController> _logger;

    public TestAuthController(ILogger<TestAuthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Test page that redirects to /connect/authorize with proper parameters
    /// Access this in a browser after logging in via /api/auth/login
    /// </summary>
    [HttpGet("authorize")]
    public IActionResult TestAuthorize(
        [FromQuery] string? client_id = "johodp-spa",
        [FromQuery] string? redirect_uri = "http://localhost:4200/callback",
        [FromQuery] string? tenant = "acme-corp")
    {
        _logger.LogInformation("Test authorize called. User authenticated: {IsAuthenticated}", User?.Identity?.IsAuthenticated);
        
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new 
            { 
                error = "not_authenticated",
                message = "You must login first. Call POST /api/auth/login before accessing this endpoint.",
                login_endpoint = $"{Request.Scheme}://{Request.Host}/api/auth/login"
            });
        }

        // Generate PKCE challenge (in production, client generates this)
        // Code verifier: dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk
        var codeChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";
        
        var authorizeUrl = $"{Request.Scheme}://{Request.Host}/connect/authorize" +
            $"?response_type=code" +
            $"&client_id={Uri.EscapeDataString(client_id!)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirect_uri!)}" +
            $"&scope=openid%20profile%20email%20johodp.identity%20johodp.api" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256" +
            $"&state={Guid.NewGuid():N}" +
            $"&nonce={Guid.NewGuid():N}" +
            $"&acr_values=tenant:{Uri.EscapeDataString(tenant!)}";

        _logger.LogInformation("Redirecting to: {AuthorizeUrl}", authorizeUrl);
        
        return Redirect(authorizeUrl);
    }

    /// <summary>
    /// Callback endpoint for testing (simulates your SPA callback)
    /// </summary>
    [HttpGet("callback")]
    public IActionResult TestCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            return Ok(new
            {
                success = false,
                error,
                message = "Authorization failed"
            });
        }

        if (string.IsNullOrEmpty(code))
        {
            return BadRequest(new { error = "missing_code", message = "Authorization code not received" });
        }

        return Ok(new
        {
            success = true,
            authorization_code = code,
            state,
            message = "Authorization successful! Use this code to exchange for tokens.",
            next_step = "POST /connect/token with grant_type=authorization_code"
        });
    }
}
