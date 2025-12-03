using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Johodp.Domain.Users.Aggregates;
using Johodp.Application.Common.Interfaces;
using System.Security.Claims;

namespace Johodp.Api.Controllers;

/// <summary>
/// Account Controller - Does NOT use Mediator pattern
/// Handles ASP.NET Identity infrastructure operations (authentication, password management, tokens)
/// Direct access to UserManager, SignInManager required for session/identity management
/// </summary>
[ApiController]
[Route("api/auth")]
public class AccountController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ILogger<AccountController> _logger;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;

    public AccountController(
        UserManager<User> userManager, 
        SignInManager<User> signInManager,
        ILogger<AccountController> logger,
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IEmailService emailService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _emailService = emailService;
    }

    // ========== AUTHENTICATION ==========

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, [FromQuery] string? acr_values = null)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request" });

        var tenantName = ExtractTenantName(acr_values, request.TenantName);
        _logger.LogInformation("API login attempt for email: {Email}, tenant: {TenantName}", request.Email, tenantName);

        if (string.IsNullOrEmpty(tenantName))
        {
            _logger.LogWarning("Login failed - tenant required: {Email}", request.Email);
            return BadRequest(new { error = "Tenant name is required" });
        }

        // Validate tenant
        var tenantResult = await ValidateActiveTenantAsync(tenantName);
        if (tenantResult.error != null)
            return tenantResult.error;
        var tenant = tenantResult.tenant!;

        // Find user by email + tenant (composite key)
        var user = await _unitOfWork.Users.GetByEmailAndTenantAsync(request.Email, tenant.Id);
        if (user == null || !user.BelongsToTenant(tenant.Id))
        {
            _logger.LogWarning("Login failed - invalid credentials: {Email}", request.Email);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        // Validate acr_values if provided
        if (acr_values?.StartsWith("tenant:", StringComparison.OrdinalIgnoreCase) == true)
        {
            var acrTenant = acr_values.Substring(7);
            if (!tenant.IsValidForAcrValue(acrTenant))
            {
                _logger.LogWarning("Login failed - invalid acr_values: {Email}, {AcrValues}", request.Email, acrTenant);
                return Unauthorized(new { error = "Invalid acr_values for this tenant" });
            }
        }

        // Verify password
        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            _logger.LogWarning("Login failed - invalid password: {Email}", request.Email);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        // Success - sign in with tenant claims
        _logger.LogInformation("Login successful: {Email}, tenant: {TenantName}", request.Email, tenantName);
        await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, new[]
        {
            new Claim("tenant_id", tenant.Id.Value.ToString()),
            new Claim("tenant_name", tenant.Name)
        });

        return Ok(new { message = "Login successful", email = request.Email });
    }

    /// <summary>
    /// Logout current user
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        _logger.LogInformation("API logout for user: {UserEmail}", User?.Identity?.Name);
        
        await _signInManager.SignOutAsync();
        
        return Ok(new { message = "Logout successful" });
    }

    // ========== REGISTRATION FLOW ==========

    /// <summary>
    /// Register new user (sends request to external app for validation)
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        _logger.LogInformation("Registration attempt: {Email}, tenant: {TenantName}", request.Email, request.TenantName);

        // Check if user exists
        if (await _userManager.FindByEmailAsync(request.Email) != null)
        {
            _logger.LogWarning("Registration failed - user exists: {Email}", request.Email);
            return Conflict(new { error = "An account with this email already exists" });
        }

        // Validate tenant
        if (string.IsNullOrEmpty(request.TenantName))
            return BadRequest(new { error = "Tenant name is required" });

        var tenantResult = await ValidateActiveTenantAsync(request.TenantName);
        if (tenantResult.error != null)
            return tenantResult.error;

        try
        {
            var requestId = Guid.NewGuid().ToString();

            // Send notification (fire-and-forget)
            _ = _notificationService.NotifyAccountRequestAsync(
                request.TenantName,
                request.Email,
                request.FirstName,
                request.LastName,
                requestId);

            _logger.LogInformation("Registration notification sent: {Email}, RequestId: {RequestId}", request.Email, requestId);

            return Accepted(new
            {
                message = "Registration request submitted. Awaiting validation.",
                requestId,
                email = request.Email,
                tenantName = request.TenantName,
                status = "pending"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration error: {Email}", request.Email);
            return StatusCode(500, new { error = "An error occurred during registration" });
        }
    }

    /// <summary>
    /// Activate user account with password (called after email validation)
    /// </summary>
    [HttpPost("activate")]
    [AllowAnonymous]
    public async Task<IActionResult> Activate([FromBody] ActivateRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        _logger.LogInformation("Activation attempt: {UserId}", request.UserId);

        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            _logger.LogWarning("Activation failed - user not found: {UserId}", request.UserId);
            return BadRequest(new { error = "Invalid user" });
        }

        // Verify token
        var tokenValid = await _userManager.VerifyUserTokenAsync(
            user,
            _userManager.Options.Tokens.EmailConfirmationTokenProvider,
            "EmailConfirmation",
            request.Token);

        if (!tokenValid)
        {
            _logger.LogWarning("Activation failed - invalid token: {UserId}", request.UserId);
            return BadRequest(new { error = "Invalid or expired activation token" });
        }

        // Get domain user
        var domainUser = await _userRepository.GetByIdAsync(Johodp.Domain.Users.ValueObjects.UserId.From(Guid.Parse(user.Id.Value.ToString())));
        if (domainUser == null)
        {
            _logger.LogError("Activation failed - domain user not found: {UserId}", request.UserId);
            return BadRequest(new { error = "User not found" });
        }

        // Set password and activate
        var passwordHash = _userManager.PasswordHasher.HashPassword(user, request.NewPassword);
        domainUser.SetPasswordHash(passwordHash);
        domainUser.Activate();
        await _unitOfWork.SaveChangesAsync();

        // Confirm email
        var confirmResult = await _userManager.ConfirmEmailAsync(user, request.Token);
        if (!confirmResult.Succeeded)
        {
            var errors = string.Join(", ", confirmResult.Errors.Select(e => e.Description));
            _logger.LogError("Activation failed - email confirmation: {UserId}, {Errors}", request.UserId, errors);
            return BadRequest(new { error = "Activation failed", details = errors });
        }

        _logger.LogInformation("Activation successful: {UserId}", user.Id);

        // Send welcome email (fire-and-forget)
        var tenantName = domainUser.TenantId != null
            ? (await _tenantRepository.GetByIdAsync(domainUser.TenantId))?.Name
            : null;
        _ = _emailService.SendWelcomeEmailAsync(user.Email.Value, domainUser.FirstName, domainUser.LastName, tenantName);

        return Ok(new
        {
            message = "Account activated successfully",
            userId = user.Id.Value,
            email = user.Email.Value,
            status = "Active"
        });
    }

    // ========== PASSWORD RESET FLOW ==========

    /// <summary>
    /// Request password reset (sends email with token)
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request" });

        _logger.LogInformation("Password reset requested: {Email}", request.Email);

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Password reset - user not found: {Email}", request.Email);
            return Ok(new { message = "If the email exists, a password reset link has been sent" });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        // Send email (fire-and-forget)
        var domainUser = await _userRepository.GetByIdAsync(Johodp.Domain.Users.ValueObjects.UserId.From(user.Id.Value));
        var firstName = domainUser?.FirstName ?? "User";
        _ = _emailService.SendPasswordResetEmailAsync(user.Email.Value, firstName, token, user.Id.Value);

        _logger.LogWarning("[DEV] Password reset token: {Email} -> {Token}", user.Email, token);

#if DEBUG
        return Ok(new { message = "Password reset token generated", email = request.Email, token, resetUrl = $"{Request.Scheme}://{Request.Host}/api/auth/reset-password" });
#else
        return Ok(new { message = "If the email exists, a password reset link has been sent" });
#endif
    }

    /// <summary>
    /// Reset password with token
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        _logger.LogInformation("Password reset attempt: {Email}", request.Email);

        if (request.Password != request.ConfirmPassword)
        {
            _logger.LogWarning("Password reset failed - mismatch: {Email}", request.Email);
            return BadRequest(new { error = "Passwords do not match" });
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Password reset failed - user not found: {Email}", request.Email);
            return BadRequest(new { error = "Invalid reset token or email" });
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, request.Token, request.Password);
        if (!resetResult.Succeeded)
        {
            var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
            _logger.LogError("Password reset failed: {Email}, {Errors}", request.Email, errors);
            return BadRequest(new { error = "Password reset failed", details = errors });
        }

        _logger.LogInformation("Password reset successful: {Email}", request.Email);
        return Ok(new { message = "Password reset successful", email = request.Email });
    }

    // ========== PRIVATE HELPERS ==========

    /// <summary>
    /// Extract tenant name from acr_values or request body
    /// </summary>
    private static string? ExtractTenantName(string? acrValues, string? requestTenantName)
    {
        if (!string.IsNullOrEmpty(acrValues) && acrValues.StartsWith("tenant:", StringComparison.OrdinalIgnoreCase))
            return acrValues.Substring(7);
        return requestTenantName;
    }

    /// <summary>
    /// Validate tenant exists and is active (reusable helper)
    /// </summary>
    private async Task<(IActionResult? error, Domain.Tenants.Aggregates.Tenant? tenant)> ValidateActiveTenantAsync(string tenantName)
    {
        var tenant = await _tenantRepository.GetByNameAsync(tenantName);
        if (tenant == null || !tenant.IsActive)
        {
            _logger.LogWarning("Invalid or inactive tenant: {TenantName}", tenantName);
            return (BadRequest(new { error = "Invalid or inactive tenant" }), null);
        }
        return (null, tenant);
    }
}

// ========== REQUEST/RESPONSE MODELS ==========

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? TenantName { get; set; }
    }public class RegisterRequest
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

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
