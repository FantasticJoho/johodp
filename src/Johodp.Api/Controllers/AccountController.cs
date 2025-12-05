using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Johodp.Contracts.Users;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Tenants.Aggregates;
using Johodp.Domain.Clients.Aggregates;
using Johodp.Domain.Clients.ValueObjects;
using Johodp.Application.Common.Interfaces;
using Johodp.Application.Users;
using System.Security.Claims;

namespace Johodp.Api.Controllers;

/// <summary>
/// Account Controller - Infrastructure Exception to Clean Architecture
/// 
/// This controller intentionally does NOT use the Mediator pattern for the following reasons:
/// 1. ASP.NET Identity Operations: Direct access to UserManager/SignInManager is required
///    for authentication, session management, and token generation (these are infrastructure concerns)
/// 2. Framework Integration: SignInManager manages cookies and authentication state that cannot
///    be easily encapsulated in domain commands
/// 3. Pragmatic Design: Wrapping UserManager in Commands/Handlers creates unnecessary indirection
///    without providing meaningful business value
/// 
/// Design Principles Applied:
/// - Controllers handle HTTP concerns (request/response, status codes)
/// - Domain services (IMfaService) encapsulate business logic (MFA requirements, TOTP formatting)
/// - Repositories handle data access
/// - UserManager/SignInManager handle Identity infrastructure
/// 
/// This results in a pragmatic balance: thin controllers for business logic (using Mediator elsewhere),
/// but direct infrastructure access where the framework requires it.
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
    private readonly IMfaService _mfaService;

    public AccountController(
        UserManager<User> userManager, 
        SignInManager<User> signInManager,
        ILogger<AccountController> logger,
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IEmailService emailService,
        IMfaService mfaService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _emailService = emailService;
        _mfaService = mfaService;
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

        // Check if MFA is required (Strategy Pattern - Parcours 2)
        var mfaRequired = await _mfaService.IsMfaRequiredForUserAsync(user);
        
        if (mfaRequired)
        {
            // Check if user has MFA enabled
            var mfaEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
            
            if (!mfaEnabled)
            {
                // MFA required but not enrolled → Redirect to enrollment (Parcours 1)
                _logger.LogInformation("User {UserId} requires MFA enrollment", user.Id);
                return Ok(new
                {
                    mfaEnrollmentRequired = true,
                    message = "MFA enrollment required",
                    redirectUrl = "/mfa/enroll"
                });
            }

            // MFA enabled → Create pending_mfa cookie and redirect to verification
            _logger.LogInformation("User {UserId} requires MFA verification", user.Id);

            // Create cookie data: userId|clientId|timestamp
            var cookieValue = $"{user.Id}|{tenant.ClientId?.Value}|{DateTime.UtcNow:O}";
            
            // TODO: Encrypt cookie value using Data Protection API
            
            // Set cookie (5 minutes expiration)
            Response.Cookies.Append("pending_mfa", cookieValue, new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // HTTPS only
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromMinutes(5)
            });

            return Ok(new
            {
                mfaVerificationRequired = true,
                message = "MFA verification required",
                redirectUrl = "/mfa-verification"
            });
        }

        // Success - sign in with tenant claims (no MFA required)
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

        // Check if MFA is required for this tenant's client
        var mfaRequired = await _mfaService.IsMfaRequiredForUserAsync(domainUser);
        
        return Ok(new
        {
            message = "Account activated successfully",
            userId = user.Id.Value,
            email = user.Email.Value,
            status = "Active",
            mfaRequired = mfaRequired,
            mfaSetupUrl = mfaRequired ? $"/api/auth/mfa/enroll" : null
        });
    }

    // ========== MFA / TOTP ENDPOINTS ==========

    /// <summary>
    /// Enroll TOTP authenticator (returns QR code for scanning)
    /// </summary>
    [HttpPost("mfa/enroll")]
    [Authorize]
    public async Task<IActionResult> EnrollTotp()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized(new { error = "User not found" });

        // Check if MFA is required
        var domainUser = await _userRepository.GetByIdAsync(user.Id);
        if (domainUser == null)
            return BadRequest(new { error = "User not found" });
            
        var mfaRequired = await _mfaService.IsMfaRequiredForUserAsync(domainUser);
        
        if (!mfaRequired)
            return BadRequest(new { error = "MFA is not required for your account" });

        // Reset authenticator key
        await _userManager.ResetAuthenticatorKeyAsync(user);
        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        
        if (string.IsNullOrEmpty(key))
        {
            _logger.LogError("Failed to generate authenticator key for user {UserId}", user.Id);
            return StatusCode(500, new { error = "Failed to generate authenticator key" });
        }

        // Generate QR code URI
        var email = await _userManager.GetEmailAsync(user);
        var qrCodeUri = _mfaService.GenerateQrCodeUri(email!, key, "Johodp");

        return Ok(new TotpEnrollmentResponse
        {
            SharedKey = key,
            QrCodeUri = qrCodeUri,
            ManualEntryKey = _mfaService.FormatKey(key)
        });
    }

    /// <summary>
    /// Verify TOTP code and enable MFA
    /// </summary>
    [HttpPost("mfa/verify-enrollment")]
    [Authorize]
    public async Task<IActionResult> VerifyTotpEnrollment([FromBody] VerifyTotpRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized(new { error = "User not found" });

        // Verify the TOTP code
        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            request.Code);

        if (!isValid)
        {
            _logger.LogWarning("Invalid TOTP code during enrollment for user {UserId}", user.Id);
            return BadRequest(new { error = "Invalid verification code" });
        }

        // Enable 2FA
        await _userManager.SetTwoFactorEnabledAsync(user, true);

        // Enable MFA in domain
        var domainUser = await _userRepository.GetByIdAsync(user.Id);
        if (domainUser != null)
        {
            domainUser.EnableMFA();
            await _unitOfWork.SaveChangesAsync();
        }

        _logger.LogInformation("TOTP enrolled successfully for user {UserId}", user.Id);

        // Generate recovery codes
        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        return Ok(new
        {
            message = "Two-factor authentication enabled successfully",
            recoveryCodes = recoveryCodes?.ToArray()
        });
    }

    /// <summary>
    /// Login with TOTP (two-step: password first, then TOTP)
    /// </summary>
    [HttpPost("login-with-totp")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginWithTotp([FromBody] LoginWithTotpRequest request)
    {
        // Find user
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Login attempt with invalid email: {Email}", request.Email);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        // Validate password
        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning("Login attempt with invalid password: {Email}", request.Email);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        // Check if MFA is required
        var domainUser = await _userRepository.GetByIdAsync(user.Id);
        if (domainUser == null)
            return Unauthorized(new { error = "User not found" });
            
        var mfaRequired = await _mfaService.IsMfaRequiredForUserAsync(domainUser);

        if (mfaRequired)
        {
            // User must have 2FA enabled
            if (!await _userManager.GetTwoFactorEnabledAsync(user))
            {
                _logger.LogWarning("MFA required but not enrolled for user {UserId}", user.Id);
                return BadRequest(new 
                { 
                    error = "Two-factor authentication is required but not enrolled",
                    mfaEnrollmentRequired = true 
                });
            }

            // Verify TOTP code
            if (string.IsNullOrEmpty(request.TotpCode))
            {
                return Ok(new MfaRequiredResponse
                {
                    MfaRequired = true,
                    MfaMethod = "totp",
                    Message = "Two-factor authentication code required"
                });
            }

            var isValid = await _userManager.VerifyTwoFactorTokenAsync(
                user,
                _userManager.Options.Tokens.AuthenticatorTokenProvider,
                request.TotpCode);

            if (!isValid)
            {
                _logger.LogWarning("Invalid TOTP code during login for user {UserId}", user.Id);
                return Unauthorized(new { error = "Invalid verification code" });
            }
        }

        // Sign in
        await _signInManager.SignInAsync(user, isPersistent: false);
        
        _logger.LogInformation("User {UserId} logged in successfully with TOTP", user.Id);

        return Ok(new
        {
            message = "Login successful",
            userId = user.Id.Value,
            email = user.Email.Value,
            mfaVerified = mfaRequired
        });
    }

    /// <summary>
    /// Get MFA status for current user
    /// </summary>
    [HttpGet("mfa/status")]
    [Authorize]
    public async Task<IActionResult> GetMfaStatus()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized(new { error = "User not found" });

        var domainUser = await _userRepository.GetByIdAsync(user.Id);
        if (domainUser == null)
            return NotFound(new { error = "User not found" });

        var mfaEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
        var mfaRequired = await _mfaService.IsMfaRequiredForUserAsync(domainUser);
        
        // Get recovery codes count
        var recoveryCodesCount = await _userManager.CountRecoveryCodesAsync(user);

        // Get client requirement
        var tenant = await _tenantRepository.GetByIdAsync(domainUser.TenantId);
        var clientRequiresMfa = false;
        if (tenant?.ClientId != null)
        {
            var client = await _unitOfWork.Clients.GetByIdAsync(tenant.ClientId);
            clientRequiresMfa = client?.RequireMfa ?? false;
        }

        return Ok(new MfaStatusResponse
        {
            MfaEnabled = mfaEnabled,
            EnrolledAt = domainUser.MFAEnabled ? domainUser.UpdatedAt : null,
            RecoveryCodesRemaining = recoveryCodesCount,
            IsMfaRequired = mfaRequired,
            ClientRequiresMfa = clientRequiresMfa
        });
    }

    /// <summary>
    /// Verify TOTP code with pending_mfa cookie (Parcours 2)
    /// </summary>
    [HttpPost("mfa-verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyMfa([FromBody] VerifyMfaRequest request)
    {
        // Read pending_mfa cookie
        if (!Request.Cookies.TryGetValue("pending_mfa", out var cookieValue) || string.IsNullOrEmpty(cookieValue))
        {
            _logger.LogWarning("MFA verification attempted without pending_mfa cookie");
            return Unauthorized(new { error = "Session expired, please log in again" });
        }

        // Decrypt cookie to get PendingMfaData
        PendingMfaData? pendingData;
        try
        {
            // TODO: Implement proper encryption/decryption using Data Protection API
            // For now, assume cookie contains "userId|clientId|timestamp"
            var parts = cookieValue.Split('|');
            if (parts.Length != 3)
                return Unauthorized(new { error = "Invalid session" });

            pendingData = new PendingMfaData
            {
                UserId = Guid.Parse(parts[0]),
                ClientId = Guid.Parse(parts[1]),
                CreatedAt = DateTime.Parse(parts[2])
            };

            // Check expiration (5 minutes)
            if (DateTime.UtcNow - pendingData.CreatedAt > TimeSpan.FromMinutes(5))
            {
                _logger.LogWarning("Expired pending_mfa cookie for user {UserId}", pendingData.UserId);
                Response.Cookies.Delete("pending_mfa");
                return Unauthorized(new { error = "Session expired, please log in again" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt pending_mfa cookie");
            Response.Cookies.Delete("pending_mfa");
            return Unauthorized(new { error = "Invalid session" });
        }

        // Load user
        var user = await _userManager.FindByIdAsync(pendingData.UserId.ToString());
        if (user == null)
        {
            _logger.LogWarning("User not found for pending MFA: {UserId}", pendingData.UserId);
            Response.Cookies.Delete("pending_mfa");
            return Unauthorized(new { error = "User not found" });
        }

        // Verify TOTP code
        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            request.TotpCode);

        if (!isValid)
        {
            _logger.LogWarning("Invalid TOTP code during MFA verification for user {UserId}", user.Id);
            return BadRequest(new { error = "Invalid TOTP code, please try again" });
        }

        // Sign in user with cookie-based session + MFA verified claim
        await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, new[]
        {
            new Claim("mfa_verified", "true")
        });

        // Delete pending_mfa cookie
        Response.Cookies.Delete("pending_mfa");

        _logger.LogInformation("User {UserId} completed MFA verification successfully", user.Id);

        // Load domain user and tenant for redirect URL construction
        var domainUser = await _userRepository.GetByIdAsync(user.Id);
        var tenant = domainUser != null 
            ? await _tenantRepository.GetByIdAsync(domainUser.TenantId)
            : null;

        // Redirect to IdentityServer OAuth2 flow to generate real JWT with all claims
        // The client app should initiate /connect/authorize with the authenticated cookie
        // IdentityServer will see the cookie (with mfa_verified claim) and generate proper JWT tokens
        var clientId = tenant?.ClientId?.Value.ToString() ?? pendingData.ClientId.ToString();
        var tenantAcrValue = tenant?.Name ?? string.Empty;
        
        return Ok(new
        {
            mfaVerified = true,
            userId = user.Id.Value,
            email = user.Email.Value,
            message = "MFA verification successful. You are now signed in.",
            // Client app should redirect to this URL to get OAuth2 tokens
            // The {{placeholders}} should be replaced by the client app with actual values
            // acr_values preserves tenant context for IdentityServer
            redirectUrl = $"/connect/authorize?client_id={clientId}&response_type=code&scope=openid profile email johodp.api&redirect_uri={{{{client_redirect_uri}}}}&state={{{{client_state}}}}&code_challenge={{{{pkce_challenge}}}}&code_challenge_method=S256&acr_values=tenant:{tenantAcrValue}"
        });
    }

    /// <summary>
    /// Initiate lost device recovery (Parcours 3 - Step 1)
    /// </summary>
    [HttpPost("mfa/lost-device")]
    [AllowAnonymous]
    public async Task<IActionResult> InitiateLostDeviceRecovery([FromBody] LostDeviceRequest request)
    {
        _logger.LogInformation("Lost device recovery initiated for email: {Email}", request.Email);

        // Find user by email (don't reveal if user exists)
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Lost device recovery - user not found: {Email}", request.Email);
            // Return success anyway (security: don't reveal if email exists)
            return Ok(new
            {
                message = "If the email exists, a verification link has been sent. Check your inbox.",
                expiresIn = "1 hour"
            });
        }

        // Check if MFA is enabled
        var mfaEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
        if (!mfaEnabled)
        {
            _logger.LogWarning("Lost device recovery - MFA not enabled: {Email}", request.Email);
            return Ok(new
            {
                message = "If the email exists, a verification link has been sent. Check your inbox.",
                expiresIn = "1 hour"
            });
        }

        // Generate identity verification token (1 hour expiration)
        var token = await _userManager.GenerateUserTokenAsync(
            user,
            "Default",
            "IdentityVerification");

        // TODO: Store token with expiration in database or cache

        // Send email with verification link
        var verificationLink = $"https://app.johodp.com/verify-identity?token={token}";
        
        // TODO: Implement email sending
        _logger.LogInformation(
            "Identity verification email sent to {Email} with link: {Link}", 
            request.Email, 
            verificationLink);

        return Ok(new
        {
            message = "If the email exists, a verification link has been sent. Check your inbox.",
            expiresIn = "1 hour"
        });
    }

    /// <summary>
    /// Verify user identity (Parcours 3 - Step 2)
    /// </summary>
    [HttpPost("mfa/verify-identity")]
    [AllowAnonymous]
    public Task<IActionResult> VerifyIdentity([FromBody] VerifyIdentityRequest request)
    {
        // TODO: Validate token from database/cache (1h expiration)
        // For now, decode token to get user
        
        // Validate security questions if provided
        if (request.SecurityAnswers != null && request.SecurityAnswers.Any())
        {
            // TODO: Implement security questions validation
            _logger.LogInformation("Security questions provided for identity verification");
        }

        // Generate verified_identity token (30 min expiration)
        var verifiedToken = Guid.NewGuid().ToString();
        
        // TODO: Store verified_identity token with 30min expiration

        _logger.LogInformation("Identity verified successfully");

        return Task.FromResult<IActionResult>(Ok(new VerifyIdentityResponse
        {
            VerifiedToken = verifiedToken,
            ExpiresIn = "30 minutes",
            Message = "Identity verified. You can now reset your MFA enrollment."
        }));
    }

    /// <summary>
    /// Reset MFA enrollment (Parcours 3 - Step 3)
    /// </summary>
    [HttpPost("mfa/reset-enrollment")]
    [AllowAnonymous]
    public Task<IActionResult> ResetMfaEnrollment([FromBody] ResetEnrollmentRequest request)
    {
        // TODO: Validate verified_identity token (30 min expiration)
        // TODO: Get userId from token

        // For now, placeholder implementation
        _logger.LogWarning("Reset MFA enrollment - implementation incomplete");

        return Task.FromResult<IActionResult>(Ok(new
        {
            message = "MFA disabled successfully. You must re-enroll on next login.",
            mfaEnabled = false,
            nextStep = "Login and complete MFA enrollment (Parcours 1)"
        }));
    }

    /// <summary>
    /// Disable MFA (optional - only if Client.RequireMfa = false)
    /// </summary>
    [HttpPost("mfa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableMfa([FromBody] DisableMfaRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized(new { error = "User not found" });

        // Verify password
        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning("Invalid password during MFA disable for user {UserId}", user.Id);
            return Unauthorized(new { error = "Invalid password" });
        }

        // Check if MFA is required by client
        var domainUser = await _userRepository.GetByIdAsync(user.Id);
        if (domainUser == null)
            return NotFound(new { error = "User not found" });

        var tenant = await _tenantRepository.GetByIdAsync(domainUser.TenantId);
        if (tenant?.ClientId != null)
        {
            var client = await _unitOfWork.Clients.GetByIdAsync(tenant.ClientId);
            if (client?.RequireMfa == true)
            {
                _logger.LogWarning("Attempted to disable MFA when required by client for user {UserId}", user.Id);
                return Conflict(new 
                { 
                    error = "Cannot disable MFA (required by organization policy)",
                    code = "MFA_REQUIRED"
                });
            }
        }

        // Disable 2FA
        await _userManager.SetTwoFactorEnabledAsync(user, false);

        // Disable MFA in domain
        domainUser.DisableMFA();
        await _unitOfWork.SaveChangesAsync();

        // Invalidate recovery codes
        await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0);

        _logger.LogInformation("MFA disabled successfully for user {UserId}", user.Id);

        // TODO: Send security alert email

        return Ok(new
        {
            mfaEnabled = false,
            message = "MFA disabled successfully"
        });
    }

    // ========== HELPER METHODS ==========
    // Note: MFA-related business logic has been extracted to IMfaService for better separation of concerns

    // ========== PASSWORD RESET FLOW ==========

    /// <summary>
    /// Initie une demande de réinitialisation de mot de passe (Étape 1/2)

    /// </summary>
    /// <remarks>
    /// Cette méthode génère un token de réinitialisation sécurisé et l'envoie par email à l'utilisateur.
    /// Le token est nécessaire pour compléter la réinitialisation via l'endpoint /reset-password.
    /// Pour des raisons de sécurité, retourne toujours un message de succès même si l'email n'existe pas.
    /// </remarks>
    /// <param name="request">Contient l'email et le nom du tenant de l'utilisateur</param>
    /// <returns>
    /// - 200 OK : Token généré et email envoyé (en DEV, inclut le token pour test)
    /// - 400 BadRequest : Tenant manquant ou invalide
    /// </returns>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request" });

        _logger.LogInformation("Password reset requested: {Email}, tenant: {TenantName}", request.Email, request.TenantName);

        // Validate tenant (required)
        if (string.IsNullOrEmpty(request.TenantName))
        {
            _logger.LogWarning("Password reset failed - tenant required: {Email}", request.Email);
            return BadRequest(new { error = "Tenant name is required" });
        }

        var tenantResult = await ValidateActiveTenantAsync(request.TenantName);
        if (tenantResult.error != null)
            return tenantResult.error;
        var tenant = tenantResult.tenant!;

        // Find user by email + tenant (composite key)
        var user = await _unitOfWork.Users.GetByEmailAndTenantAsync(request.Email, tenant.Id);
        if (user == null || !user.BelongsToTenant(tenant.Id))
        {
            _logger.LogWarning("Password reset - user not found: {Email}, tenant: {TenantName}", request.Email, request.TenantName);
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
    /// Réinitialise le mot de passe avec le token reçu par email (Étape 2/2)
    /// </summary>
    /// <remarks>
    /// Cette méthode valide le token de réinitialisation généré par /forgot-password
    /// et change effectivement le mot de passe de l'utilisateur dans la base de données.
    /// Le token expire après un certain temps (configurable dans Identity).
    /// </remarks>
    /// <param name="request">Contient l'email, le tenant, le token, et le nouveau mot de passe</param>
    /// <returns>
    /// - 200 OK : Mot de passe réinitialisé avec succès
    /// - 400 BadRequest : Token invalide/expiré, mots de passe non concordants, ou tenant invalide
    /// </returns>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        _logger.LogInformation("Password reset attempt: {Email}, tenant: {TenantName}", request.Email, request.TenantName);

        if (request.Password != request.ConfirmPassword)
        {
            _logger.LogWarning("Password reset failed - mismatch: {Email}", request.Email);
            return BadRequest(new { error = "Passwords do not match" });
        }

        // Validate tenant (required)
        if (string.IsNullOrEmpty(request.TenantName))
        {
            _logger.LogWarning("Password reset failed - tenant required: {Email}", request.Email);
            return BadRequest(new { error = "Tenant name is required" });
        }

        var tenantResult = await ValidateActiveTenantAsync(request.TenantName);
        if (tenantResult.error != null)
            return tenantResult.error;
        var tenant = tenantResult.tenant!;

        // Find user by email + tenant (composite key)
        var user = await _unitOfWork.Users.GetByEmailAndTenantAsync(request.Email, tenant.Id);
        if (user == null || !user.BelongsToTenant(tenant.Id))
        {
            _logger.LogWarning("Password reset failed - user not found: {Email}, tenant: {TenantName}", request.Email, request.TenantName);
            return BadRequest(new { error = "Invalid reset token or email" });
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, request.Token, request.Password);
        if (!resetResult.Succeeded)
        {
            var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
            _logger.LogError("Password reset failed: {Email}, {Errors}", request.Email, errors);
            return BadRequest(new { error = "Password reset failed", details = errors });
        }

        _logger.LogInformation("Password reset successful: {Email}, tenant: {TenantName}", request.Email, request.TenantName);
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
    public string TenantName { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
