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

    public AccountController(UserManager<User> userManager, SignInManager<User> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        // Try to find existing user
        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user == null)
        {
            // Create domain user and persist with password
            user = Johodp.Domain.Users.Aggregates.User.Create(model.Email, "User", "Login");
            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                foreach (var err in createResult.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);

                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }
        }

        // Attempt sign-in
        var signInResult = await _signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: false, lockoutOnFailure: false);

        if (signInResult.Succeeded)
        {
            if (string.IsNullOrEmpty(returnUrl))
                returnUrl = "/";

            return Redirect(returnUrl);
        }

        if (signInResult.RequiresTwoFactor)
        {
            ModelState.AddModelError(string.Empty, "Two-factor authentication required.");
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        ViewData["ReturnUrl"] = returnUrl;
        return View(model);
    }

    [HttpPost("api/auth/login")]
    [Produces("application/json")]
    public async Task<IActionResult> LoginApi([FromBody] LoginApiRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid request" });
        }

        // Try to find existing user
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            // Create domain user and persist with password
            user = Johodp.Domain.Users.Aggregates.User.Create(request.Email, "User", "Login");
            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return BadRequest(new { error = "Registration failed", details = errors });
            }
        }

        // Attempt sign-in
        var signInResult = await _signInManager.PasswordSignInAsync(request.Email, request.Password, isPersistent: false, lockoutOnFailure: false);

        if (signInResult.Succeeded)
        {
            return Ok(new { message = "Login successful", email = request.Email });
        }

        if (signInResult.RequiresTwoFactor)
        {
            return Unauthorized(new { error = "Two-factor authentication required" });
        }

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
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            return View(model);
        }

        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "An account with this email already exists.");
            return View(model);
        }

        // Create domain user
        var user = Johodp.Domain.Users.Aggregates.User.Create(model.Email, model.FirstName, model.LastName);

        // Create user with password
        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var err in createResult.Errors)
                ModelState.AddModelError(string.Empty, err.Description);

            return View(model);
        }

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
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            // Don't reveal if user exists for security reasons
            return RedirectToAction("ForgotPasswordConfirmation");
        }

        // Generate password reset token
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        // TODO: Send email with reset link
        // var resetLink = Url.Action("ResetPassword", "Account", new { userId = user.Id, token = token }, Request.Scheme);
        // await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);

        // For development: log the token to console
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
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return RedirectToAction("ResetPasswordConfirmation");
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (!resetResult.Succeeded)
        {
            foreach (var err in resetResult.Errors)
                ModelState.AddModelError(string.Empty, err.Description);

            return View(model);
        }

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
