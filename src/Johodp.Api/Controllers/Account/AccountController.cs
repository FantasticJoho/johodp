using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Johodp.Domain.Users.Aggregates;

namespace Johodp.Api.Controllers.Account;

public class AccountController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<User> userManager, 
        SignInManager<User> signInManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
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
        //  - User has wildcard tenant access (user.TenantId == "*")
        //  - User's tenant matches the requested tenant
        if (tenantId != "*" && user.TenantId != "*" && user.TenantId != tenantId)
        {
            _logger.LogWarning("Tenant access denied for user {Email}. User tenant: {UserTenant}, Requested tenant: {RequestedTenant}", 
                model.Email, user.TenantId, tenantId);
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
        //  - User has wildcard tenant access (user.TenantId == "*")
        //  - User's tenant matches the requested tenant
        if (tenantId != "*" && user.TenantId != "*" && user.TenantId != tenantId)
        {
            _logger.LogWarning("API: Tenant access denied for user {Email}. User tenant: {UserTenant}, Requested tenant: {RequestedTenant}", 
                request.Email, user.TenantId, tenantId);
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

    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
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
