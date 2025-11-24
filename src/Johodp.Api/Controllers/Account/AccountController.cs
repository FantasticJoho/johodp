using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Johodp.Domain.Users.Aggregates;
using Johodp.Application.Common.Interfaces;
using Johodp.Api.Models.ViewModels;
using Johodp.Application.Users.Commands;
using Johodp.Application.Common.Mediator;

namespace Johodp.Api.Controllers.Account;

public class AccountController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ILogger<AccountController> _logger;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISender _sender;
    private readonly INotificationService _notificationService;

    public AccountController(
        UserManager<User> userManager, 
        SignInManager<User> signInManager,
        ILogger<AccountController> logger,
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ISender sender,
        INotificationService notificationService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _sender = sender;
        _notificationService = notificationService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        _logger.LogInformation("Login page requested. ReturnUrl: {ReturnUrl}", returnUrl);
        ViewData["ReturnUrl"] = returnUrl;
        
        // Extract tenantId from acr_values if present in the authorize request
        string? tenantId = null;
        if (!string.IsNullOrEmpty(returnUrl))
        {
            var uri = new Uri(returnUrl, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
            {
                uri = new Uri("http://localhost" + returnUrl);
            }
            
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var acrValues = query["acr_values"];
            
            if (!string.IsNullOrEmpty(acrValues))
            {
                tenantId = acrValues.Split(' ')
                    .FirstOrDefault(x => x.StartsWith("tenant:"))
                    ?.Replace("tenant:", "");
                _logger.LogDebug("Extracted tenant from acr_values: {TenantId}", tenantId);
            }
        }
        
        ViewData["TenantId"] = tenantId;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        _logger.LogInformation("Login attempt for email: {Email}", model.Email);
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Login failed - invalid model state for email: {Email}", model.Email);
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        // Extract tenantId from acr_values in returnUrl
        string tenantId = "*"; // Default to wildcard tenant
        if (!string.IsNullOrEmpty(returnUrl))
        {
            var uri = new Uri(returnUrl, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
            {
                uri = new Uri("http://localhost" + returnUrl);
            }
            
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var acrValues = query["acr_values"];
            
            if (!string.IsNullOrEmpty(acrValues))
            {
                var extractedTenant = acrValues.Split(' ')
                    .FirstOrDefault(x => x.StartsWith("tenant:"))
                    ?.Replace("tenant:", "");
                
                if (!string.IsNullOrEmpty(extractedTenant))
                {
                    tenantId = extractedTenant;
                }
            }
        }

        // Try to find existing user
        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user == null)
        {
            _logger.LogInformation("Creating new user during login: {Email} with tenant: {TenantId}", model.Email, tenantId);
            // Create domain user and persist with password, set tenantId
            user = Johodp.Domain.Users.Aggregates.User.Create(model.Email, "User", "Login", tenantId);
            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                _logger.LogError("Failed to create user {Email}: {Errors}", model.Email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                foreach (var err in createResult.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);

                ViewData["ReturnUrl"] = returnUrl;
                ViewData["TenantId"] = tenantId;
                return View(model);
            }
        }

        // Refuse authentication if user does not have rights on the requested tenant
        // Allow if:
        //  - No specific tenant requested (tenantId == "*")
        //  - User has wildcard tenant access (TenantIds contains "*")
        //  - User's tenants include the requested tenant
        bool hasWildcardAccess = user.TenantIds.Contains("*");
        bool hasTenantAccess = user.TenantIds.Contains(tenantId) || hasWildcardAccess || tenantId == "*";
        
        if (!hasTenantAccess)
        {
            _logger.LogWarning("Tenant access denied for user {Email}. User tenants: {UserTenants}, Requested tenant: {RequestedTenant}", 
                model.Email, string.Join(", ", user.TenantIds), tenantId);
            ModelState.AddModelError(string.Empty, "User does not have access to this tenant.");
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["TenantId"] = tenantId;
            return View(model);
        }

        // Attempt sign-in
        var signInResult = await _signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: false, lockoutOnFailure: false);

        if (signInResult.Succeeded)
        {
            _logger.LogInformation("Successful login for user: {Email}, tenant: {TenantId}", model.Email, tenantId);
            if (string.IsNullOrEmpty(returnUrl))
                returnUrl = "/";

            return Redirect(returnUrl);
        }

        if (signInResult.RequiresTwoFactor)
        {
            _logger.LogInformation("MFA required for user: {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "Two-factor authentication required.");
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        _logger.LogWarning("Failed login attempt for user: {Email}", model.Email);
        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        ViewData["ReturnUrl"] = returnUrl;
        return View(model);
    }

    [HttpPost("api/auth/login")]
    [Produces("application/json")]
    public async Task<IActionResult> LoginApi([FromBody] LoginApiRequest request, [FromQuery] string? acr_values = null)
    {
        _logger.LogInformation("API login attempt for email: {Email}, acr_values: {AcrValues}", request.Email, acr_values);
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("API login failed - invalid model state for email: {Email}", request.Email);
            return BadRequest(new { error = "Invalid request" });
        }

        // Extract tenantId from acr_values parameter
        string tenantId = "*"; // Default to wildcard tenant
        if (!string.IsNullOrEmpty(acr_values))
        {
            var extractedTenant = acr_values.Split(' ')
                .FirstOrDefault(x => x.StartsWith("tenant:"))
                ?.Replace("tenant:", "");
            
            if (!string.IsNullOrEmpty(extractedTenant))
            {
                tenantId = extractedTenant;
            }
        }

        // Try to find existing user
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            _logger.LogInformation("Creating new user via API: {Email} with tenant: {TenantId}", request.Email, tenantId);
            // Create domain user and persist with password, set tenantId
            user = Johodp.Domain.Users.Aggregates.User.Create(request.Email, "User", "Login", tenantId);
            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create user via API {Email}: {Errors}", request.Email, errors);
                return BadRequest(new { error = "Registration failed", details = errors });
            }

            _logger.LogInformation("User created and signed in via API: {Email}", request.Email);
            // If user was just created, sign them in so the auth cookie is issued
            await _signInManager.SignInAsync(user, isPersistent: false);
            return Ok(new { message = "Login successful", email = request.Email });
        }

        // Refuse authentication if user does not have rights on the requested tenant
        // Allow if:
        //  - No specific tenant requested (tenantId == "*")
        //  - User has wildcard tenant access (TenantIds contains "*")
        //  - User's tenants include the requested tenant
        bool hasWildcardAccess = user.TenantIds.Contains("*");
        bool hasTenantAccess = user.TenantIds.Contains(tenantId) || hasWildcardAccess || tenantId == "*";
        
        if (!hasTenantAccess)
        {
            _logger.LogWarning("API: Tenant access denied for user {Email}. User tenants: {UserTenants}, Requested tenant: {RequestedTenant}", 
                request.Email, string.Join(", ", user.TenantIds), tenantId);
            return Unauthorized(new { error = "User does not have access to this tenant" });
        }

        // Attempt sign-in for existing user: verify password and explicitly sign in
        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (passwordValid)
        {
            _logger.LogInformation("Successful API login for user: {Email}, tenant: {TenantId}", request.Email, tenantId);
            await _signInManager.SignInAsync(user, isPersistent: false);
            return Ok(new { message = "Login successful", email = request.Email });
        }

        // If MFA would be required, surface that result (the CustomSignInManager checks user.RequiresMFA())
        if (user.RequiresMFA())
        {
            _logger.LogInformation("API: MFA required for user: {Email}", request.Email);
            return Unauthorized(new { error = "Two-factor authentication required" });
        }

        _logger.LogWarning("API: Failed login attempt for user: {Email}", request.Email);
        return Unauthorized(new { error = "Invalid email or password" });
    }

    [HttpGet]
    public IActionResult Logout()
    {
        return SignOut("Cookies", "oidc");
    }

    [HttpPost("api/auth/logout")]
    [Produces("application/json")]
    public async Task<IActionResult> LogoutApi()
    {
        _logger.LogInformation("API logout for user: {UserEmail}", User?.Identity?.Name);
        
        await _signInManager.SignOutAsync();
        
        return Ok(new { message = "Logout successful" });
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        _logger.LogInformation("Registration attempt for email: {Email}", model.Email);
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Registration failed - invalid model state for email: {Email}", model.Email);
            return View(model);
        }

        if (model.Password != model.ConfirmPassword)
        {
            _logger.LogWarning("Registration failed - password mismatch for email: {Email}", model.Email);
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            return View(model);
        }

        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            _logger.LogWarning("Registration failed - user already exists: {Email}", model.Email);
            ModelState.AddModelError("Email", "An account with this email already exists.");
            return View(model);
        }

        // Create domain user
        var user = Johodp.Domain.Users.Aggregates.User.Create(model.Email, model.FirstName, model.LastName);

        // Create user with password
        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            _logger.LogError("Failed to register user {Email}: {Errors}", model.Email, 
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            foreach (var err in createResult.Errors)
                ModelState.AddModelError(string.Empty, err.Description);

            return View(model);
        }

        _logger.LogInformation("User successfully registered and signed in: {Email}", model.Email);
        // Sign in the user
        await _signInManager.SignInAsync(user, isPersistent: false);
        return Redirect("/");
    }

    [HttpPost("api/auth/register")]
    [Produces("application/json")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterApi([FromBody] RegisterApiRequest request)
    {
        _logger.LogInformation("API registration attempt for email: {Email}", request.Email);
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("API registration failed - invalid model state for email: {Email}", request.Email);
            return BadRequest(new { error = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }

        if (request.Password != request.ConfirmPassword)
        {
            _logger.LogWarning("API registration failed - password mismatch for email: {Email}", request.Email);
            return BadRequest(new { error = "Passwords do not match" });
        }

        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            _logger.LogWarning("API registration failed - user already exists: {Email}", request.Email);
            return Conflict(new { error = "An account with this email already exists" });
        }

        // Create domain user
        var user = Johodp.Domain.Users.Aggregates.User.Create(request.Email, request.FirstName, request.LastName);

        // Create user with password
        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            _logger.LogError("API registration failed for {Email}: {Errors}", request.Email, errors);
            return BadRequest(new { error = "Registration failed", details = errors });
        }

        _logger.LogInformation("User successfully registered via API: {Email}", request.Email);
        
        return Created($"/api/users/{user.Id}", new
        {
            userId = user.Id.Value,
            email = user.Email.Value,
            message = "Registration successful"
        });
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    [Authorize]
    public IActionResult Claims()
    {
        // Pass the current user's claims to the view
        var claims = User?.Claims ?? Enumerable.Empty<Claim>();
        return View(claims);
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        _logger.LogInformation("Password reset requested for email: {Email}", model.Email);
        
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            _logger.LogWarning("Password reset requested for non-existent user: {Email}", model.Email);
            // Don't reveal if user exists for security reasons
            return RedirectToAction("ForgotPasswordConfirmation");
        }

        // Generate password reset token
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        // TODO: Send email with reset link
        // var resetLink = Url.Action("ResetPassword", "Account", new { userId = user.Id, token = token }, Request.Scheme);
        // await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);

        // For development: log the token to console
        _logger.LogWarning("[DEV ONLY] Password reset token for {Email}: {Token}", user.Email, token);
        System.Console.WriteLine($"Password reset token for {user.Email}: {token}");

        return RedirectToAction("ForgotPasswordConfirmation");
    }

    [HttpPost("api/auth/forgot-password")]
    [Produces("application/json")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPasswordApi([FromBody] ForgotPasswordApiRequest request)
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
            resetUrl = $"{Request.Scheme}://{Request.Host}/account/reset-password?token={Uri.EscapeDataString(token)}"
        });
#else
        // TODO: Send email with reset link in production
        return Ok(new { message = "If the email exists, a password reset link has been sent" });
#endif
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ResetPassword(string? token = null)
    {
        if (token == null)
        {
            return BadRequest("Token is required");
        }

        var model = new ResetPasswordViewModel { Token = token };
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        _logger.LogInformation("Password reset attempt for email: {Email}", model.Email);
        
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Password != model.ConfirmPassword)
        {
            _logger.LogWarning("Password reset failed - password mismatch for email: {Email}", model.Email);
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            _logger.LogWarning("Password reset attempted for non-existent user: {Email}", model.Email);
            return RedirectToAction("ResetPasswordConfirmation");
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (!resetResult.Succeeded)
        {
            _logger.LogError("Password reset failed for user {Email}: {Errors}", model.Email, 
                string.Join(", ", resetResult.Errors.Select(e => e.Description)));
            foreach (var err in resetResult.Errors)
                ModelState.AddModelError(string.Empty, err.Description);

            return View(model);
        }

        _logger.LogInformation("Password successfully reset for user: {Email}", model.Email);
        return RedirectToAction("ResetPasswordConfirmation");
    }

    [HttpPost("api/auth/reset-password")]
    [Produces("application/json")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPasswordApi([FromBody] ResetPasswordApiRequest request)
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

    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }

    // ========== ONBOARDING FLOW ==========

    /// <summary>
    /// GET: /account/onboarding - Affiche le formulaire d'onboarding avec le branding du tenant
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Onboarding(
        [FromQuery] string? acr_values,
        [FromQuery] string? return_url)
    {
        // Extraire le tenant depuis acr_values
        var tenantId = ExtractTenantFromAcrValues(acr_values);

        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("No tenant specified in onboarding request");
            return BadRequest("Tenant is required for onboarding");
        }

        var tenant = await _tenantRepository.GetByNameAsync(tenantId);

        if (tenant == null || !tenant.IsActive)
        {
            _logger.LogWarning("Invalid or inactive tenant: {TenantId}", tenantId);
            return BadRequest("Invalid or inactive tenant");
        }

        var model = new OnboardingViewModel
        {
            TenantId = tenantId,
            TenantDisplayName = tenant.DisplayName,
            LogoUrl = tenant.LogoUrl,
            ReturnUrl = return_url
        };

        return View(model);
    }

    /// <summary>
    /// POST: /account/onboarding - Traite la demande d'onboarding
    /// Envoie une notification à l'app tierce (fire-and-forget)
    /// L'app tierce décidera si elle appelle /api/users/register ou non
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Onboarding(OnboardingViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var tenant = await _tenantRepository.GetByNameAsync(model.TenantId);
            model.TenantDisplayName = tenant?.DisplayName ?? model.TenantId;
            model.LogoUrl = tenant?.LogoUrl;
            return View(model);
        }

        // Vérifier que l'email n'existe pas déjà
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "Un compte existe déjà avec cette adresse email");
            var tenant = await _tenantRepository.GetByNameAsync(model.TenantId);
            model.TenantDisplayName = tenant?.DisplayName ?? model.TenantId;
            model.LogoUrl = tenant?.LogoUrl;
            return View(model);
        }

        try
        {
            var requestId = Guid.NewGuid().ToString();

            // Envoyer notification à l'application tierce (fire-and-forget)
            // L'app tierce validera et appellera POST /api/users/register si OK
            await _notificationService.NotifyAccountRequestAsync(
                tenantId: model.TenantId,
                email: model.Email,
                firstName: model.FirstName,
                lastName: model.LastName,
                requestId: requestId);

            _logger.LogInformation(
                "Onboarding notification sent for {Email} on tenant {TenantId}, RequestId: {RequestId}",
                model.Email,
                model.TenantId,
                requestId);

            // Afficher page "en attente de validation"
            // L'utilisateur recevra un email SEULEMENT si l'app tierce valide
            var pendingModel = new OnboardingPendingViewModel
            {
                Email = model.Email,
                ReturnUrl = model.ReturnUrl ?? "/"
            };

            return View("OnboardingPending", pendingModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during onboarding for {Email}", model.Email);
            ModelState.AddModelError("", "Une erreur est survenue lors de la demande");
            var tenant = await _tenantRepository.GetByNameAsync(model.TenantId);
            model.TenantDisplayName = tenant?.DisplayName ?? model.TenantId;
            model.LogoUrl = tenant?.LogoUrl;
            return View(model);
        }
    }

    [HttpPost("api/account/onboarding")]
    [Produces("application/json")]
    [AllowAnonymous]
    public async Task<IActionResult> OnboardingApi([FromBody] OnboardingApiRequest request)
    {
        _logger.LogInformation("API onboarding request for email: {Email}, tenant: {TenantId}", request.Email, request.TenantId);
        
        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }

        // Vérifier que l'email n'existe pas déjà
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            _logger.LogWarning("API onboarding failed - user already exists: {Email}", request.Email);
            return Conflict(new { error = "An account with this email already exists" });
        }

        // Vérifier que le tenant existe et est actif
        var tenant = await _tenantRepository.GetByNameAsync(request.TenantId);
        if (tenant == null || !tenant.IsActive)
        {
            _logger.LogWarning("API onboarding failed - invalid or inactive tenant: {TenantId}", request.TenantId);
            return BadRequest(new { error = "Invalid or inactive tenant" });
        }

        try
        {
            var requestId = Guid.NewGuid().ToString();

            // Envoyer notification à l'application tierce (fire-and-forget)
            await _notificationService.NotifyAccountRequestAsync(
                tenantId: request.TenantId,
                email: request.Email,
                firstName: request.FirstName,
                lastName: request.LastName,
                requestId: requestId);

            _logger.LogInformation(
                "API onboarding notification sent for {Email} on tenant {TenantId}, RequestId: {RequestId}",
                request.Email,
                request.TenantId,
                requestId);

            return Accepted(new
            {
                message = "Onboarding request submitted. Awaiting validation.",
                requestId = requestId,
                email = request.Email,
                tenantId = request.TenantId,
                status = "pending"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API onboarding error for {Email}", request.Email);
            return StatusCode(500, new { error = "An error occurred during onboarding" });
        }
    }

    // ========== ACTIVATION FLOW ==========

    /// <summary>
    /// GET: /account/activate - Affiche le formulaire d'activation avec le token
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Activate(
        [FromQuery] string token,
        [FromQuery] string userId,
        [FromQuery] string tenant)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId))
        {
            return BadRequest("Invalid activation link");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return BadRequest("Invalid activation link");
        }

        // Vérifier que le user est bien en statut PendingActivation
        if (user.Status != UserStatus.PendingActivation)
        {
            return BadRequest("This account is not pending activation");
        }

        var tenantEntity = await _tenantRepository.GetByNameAsync(tenant);

        var model = new ActivateViewModel
        {
            Token = token,
            UserId = userId,
            TenantId = tenant,
            MaskedEmail = MaskEmail(user.Email.Value),
            TenantDisplayName = tenantEntity?.DisplayName ?? tenant,
            LogoUrl = tenantEntity?.LogoUrl
        };

        return View(model);
    }

    /// <summary>
    /// POST: /account/activate - Active le compte en définissant le mot de passe
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(ActivateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            ModelState.AddModelError("", "Utilisateur invalide");
            return View(model);
        }

        // Vérifier le token
        var tokenValid = await _userManager.VerifyUserTokenAsync(
            user,
            _userManager.Options.Tokens.EmailConfirmationTokenProvider,
            "EmailConfirmation",
            model.Token);

        if (!tokenValid)
        {
            ModelState.AddModelError("", "Le lien d'activation est invalide ou expiré");
            return View(model);
        }

        // Définir le mot de passe
        var passwordHash = _userManager.PasswordHasher.HashPassword(user, model.NewPassword);
        user.SetPasswordHash(passwordHash);

        // Confirmer l'email
        var confirmResult = await _userManager.ConfirmEmailAsync(user, model.Token);
        if (!confirmResult.Succeeded)
        {
            foreach (var error in confirmResult.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        // Activer le compte (domain logic)
        var domainUser = await _userRepository.GetByIdAsync(Johodp.Domain.Users.ValueObjects.UserId.From(Guid.Parse(user.Id.Value.ToString())));
        if (domainUser != null)
        {
            domainUser.Activate();
            await _unitOfWork.SaveChangesAsync();
        }

        // Connecter automatiquement l'utilisateur
        await _signInManager.SignInAsync(user, isPersistent: true);

        _logger.LogInformation("User {UserId} activated successfully", user.Id);

        // Rediriger vers page de succès ou vers l'application
        var successModel = new ActivateSuccessViewModel
        {
            ReturnUrl = model.ReturnUrl ?? "/"
        };

        return View("ActivateSuccess", successModel);
    }

    /// <summary>
    /// POST: /api/account/activate - API endpoint for user activation (no antiforgery token required)
    /// </summary>
    [HttpPost("api/account/activate")]
    [AllowAnonymous]
    public async Task<IActionResult> ActivateApi([FromBody] ActivateApiRequest request)
    {
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

        // Vérifier le token
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

        // Récupérer le domain user AVANT les modifications
        var domainUser = await _userRepository.GetByIdAsync(Johodp.Domain.Users.ValueObjects.UserId.From(Guid.Parse(user.Id.Value.ToString())));
        if (domainUser == null)
        {
            _logger.LogError("Activation failed: Domain user not found for {UserId}", request.UserId);
            return BadRequest(new { error = "User not found" });
        }

        // Définir le mot de passe sur le domain user
        var passwordHash = _userManager.PasswordHasher.HashPassword(user, request.NewPassword);
        domainUser.SetPasswordHash(passwordHash);

        // Activer le compte (domain logic) - ceci change le statut à Active
        domainUser.Activate();
        
        // Sauvegarder les changements du domain
        await _unitOfWork.SaveChangesAsync();

        // Confirmer l'email avec ASP.NET Identity
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

    // ========== HELPER METHODS ==========

    private string ExtractTenantFromAcrValues(string? acrValues)
    {
        if (string.IsNullOrEmpty(acrValues))
            return string.Empty;

        return acrValues.Split(' ')
            .FirstOrDefault(x => x.StartsWith("tenant:"))?
            .Replace("tenant:", "") ?? string.Empty;
    }

    private string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2)
            return email;

        var username = parts[0];
        var domain = parts[1];

        if (username.Length <= 2)
            return $"{username[0]}***@{domain}";

        return $"{username[0]}***{username[^1]}@{domain}";
    }
}

public class LoginViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginApiRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterViewModel
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ForgotPasswordViewModel
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public class ActivateApiRequest
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class RegisterApiRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ForgotPasswordApiRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordApiRequest
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class OnboardingApiRequest
{
    public string TenantId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
