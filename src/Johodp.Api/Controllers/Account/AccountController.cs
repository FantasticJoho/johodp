using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Johodp.Domain.Users.Aggregates;
using Johodp.Application.Common.Interfaces;

namespace Johodp.Api.Controllers.Account;

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

    public AccountController(
        UserManager<User> userManager, 
        SignInManager<User> signInManager,
        ILogger<AccountController> logger,
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    // ========== AUTHENTICATION ==========

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("API login attempt for email: {Email}, tenantId: {TenantId}", request.Email, request.TenantId);
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("API login failed - invalid model state for email: {Email}", request.Email);
            return BadRequest(new { error = "Invalid request" });
        }

        // Use tenantId from request body, default to wildcard if not specified
        string tenantId = string.IsNullOrEmpty(request.TenantId) ? "*" : request.TenantId;

        // Find user
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            _logger.LogWarning("API login failed - user not found: {Email}", request.Email);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        // Check tenant access
        bool hasWildcardAccess = user.TenantIds.Contains("*");
        bool hasTenantAccess = user.TenantIds.Contains(tenantId) || hasWildcardAccess || tenantId == "*";
        
        if (!hasTenantAccess)
        {
            _logger.LogWarning("API: Tenant access denied for user {Email}. User tenants: {UserTenants}, Requested tenant: {RequestedTenant}", 
                request.Email, string.Join(", ", user.TenantIds), tenantId);
            return Unauthorized(new { error = "User does not have access to this tenant" });
        }

        // Verify password and sign in
        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (passwordValid)
        {
            _logger.LogInformation("Successful API login for user: {Email}, tenant: {TenantId}", request.Email, tenantId);
            await _signInManager.SignInAsync(user, isPersistent: false);
            return Ok(new { message = "Login successful", email = request.Email });
        }

        // Check if MFA required
        if (user.RequiresMFA())
        {
            _logger.LogInformation("API: MFA required for user: {Email}", request.Email);
            return Unauthorized(new { error = "Two-factor authentication required" });
        }

        _logger.LogWarning("API: Failed login attempt for user: {Email}", request.Email);
        return Unauthorized(new { error = "Invalid email or password" });
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
        _logger.LogInformation("API registration attempt for email: {Email}, tenant: {TenantId}", request.Email, request.TenantId);
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("API registration failed - invalid model state for email: {Email}", request.Email);
            return BadRequest(new { error = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }

        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            _logger.LogWarning("API registration failed - user already exists: {Email}", request.Email);
            return Conflict(new { error = "An account with this email already exists" });
        }

        // If tenant specified, verify it exists and is active
        if (!string.IsNullOrEmpty(request.TenantId) && request.TenantId != "*")
        {
            var tenant = await _tenantRepository.GetByNameAsync(request.TenantId);
            if (tenant == null || !tenant.IsActive)
            {
                _logger.LogWarning("API registration failed - invalid or inactive tenant: {TenantId}", request.TenantId);
                return BadRequest(new { error = "Invalid or inactive tenant" });
            }
        }

        try
        {
            var requestId = Guid.NewGuid().ToString();

            // Send notification to external app for validation (fire-and-forget)
            await _notificationService.NotifyAccountRequestAsync(
                tenantId: request.TenantId ?? "*",
                email: request.Email,
                firstName: request.FirstName,
                lastName: request.LastName,
                requestId: requestId);

            _logger.LogInformation(
                "API registration notification sent for {Email} on tenant {TenantId}, RequestId: {RequestId}",
                request.Email,
                request.TenantId,
                requestId);

            return Accepted(new
            {
                message = "Registration request submitted. Awaiting validation.",
                requestId = requestId,
                email = request.Email,
                tenantId = request.TenantId ?? "*",
                status = "pending"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API registration error for {Email}", request.Email);
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
        _logger.LogInformation("API activation attempt for user: {UserId}", request.UserId);
        
        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }

        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            _logger.LogWarning("Activation failed: User not found {UserId}", request.UserId);
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
            _logger.LogWarning("Activation failed: Invalid token for user {UserId}", request.UserId);
            return BadRequest(new { error = "Invalid or expired activation token" });
        }

        // Get domain user BEFORE modifications
        var domainUser = await _userRepository.GetByIdAsync(Johodp.Domain.Users.ValueObjects.UserId.From(Guid.Parse(user.Id.Value.ToString())));
        if (domainUser == null)
        {
            _logger.LogError("Activation failed: Domain user not found for {UserId}", request.UserId);
            return BadRequest(new { error = "User not found" });
        }

        // Set password on domain user
        var passwordHash = _userManager.PasswordHasher.HashPassword(user, request.NewPassword);
        domainUser.SetPasswordHash(passwordHash);

        // Activate account (domain logic) - changes status to Active
        domainUser.Activate();
        
        // Save domain changes
        await _unitOfWork.SaveChangesAsync();

        // Confirm email with ASP.NET Identity
        var confirmResult = await _userManager.ConfirmEmailAsync(user, request.Token);
        if (!confirmResult.Succeeded)
        {
            var errors = string.Join(", ", confirmResult.Errors.Select(e => e.Description));
            _logger.LogError("Activation failed: Email confirmation error for user {UserId}: {Errors}", request.UserId, errors);
            return BadRequest(new { error = "Activation failed", details = errors });
        }

        _logger.LogInformation("User {UserId} activated successfully via API", user.Id);

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
        _logger.LogInformation("API password reset requested for email: {Email}", request.Email);
        
        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid request" });
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("API password reset requested for non-existent user: {Email}", request.Email);
            // Don't reveal if user exists for security reasons
            return Ok(new { message = "If the email exists, a password reset link has been sent" });
        }

        // Generate password reset token
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        // For development: log the token
        _logger.LogWarning("[DEV ONLY] API password reset token for {Email}: {Token}", user.Email, token);
        System.Console.WriteLine($"[API] Password reset token for {user.Email}: {token}");

#if DEBUG
        // In development, return the token for testing purposes
        return Ok(new 
        { 
            message = "Password reset token generated",
            email = request.Email,
            token = token,
            resetUrl = $"{Request.Scheme}://{Request.Host}/api/auth/reset-password"
        });
#else
        // TODO: Send email with reset link in production
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
        _logger.LogInformation("API password reset attempt for email: {Email}", request.Email);
        
        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }

        if (request.Password != request.ConfirmPassword)
        {
            _logger.LogWarning("API password reset failed - password mismatch for email: {Email}", request.Email);
            return BadRequest(new { error = "Passwords do not match" });
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("API password reset attempted for non-existent user: {Email}", request.Email);
            return BadRequest(new { error = "Invalid reset token or email" });
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, request.Token, request.Password);
        if (!resetResult.Succeeded)
        {
            var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
            _logger.LogError("API password reset failed for user {Email}: {Errors}", request.Email, errors);
            return BadRequest(new { error = "Password reset failed", details = errors });
        }

        _logger.LogInformation("Password successfully reset via API for user: {Email}", request.Email);
        return Ok(new 
        { 
            message = "Password reset successful",
            email = request.Email 
        });
    }
}

// ========== REQUEST/RESPONSE MODELS ==========

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? TenantId { get; set; }
    }public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string TenantId { get; set; } = "*";
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
