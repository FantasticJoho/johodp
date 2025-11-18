using Microsoft.AspNetCore.Mvc;
using Johodp.Application.Users.Commands;
using MediatR;

namespace Johodp.Api.Controllers.Account;

public class AccountController : Controller
{
    private readonly IMediator _mediator;

    public AccountController(IMediator mediator)
    {
        _mediator = mediator;
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

        try
        {
            // Register user if login attempt (for demo, we just register/retrieve)
            var command = new RegisterUserCommand
            {
                Email = model.Email,
                FirstName = "User",
                LastName = "Login"
            };

            var result = await _mediator.Send(command);

            // In a real scenario, you would verify password here
            // For now, we redirect to authorization endpoint
            if (string.IsNullOrEmpty(returnUrl))
                returnUrl = "/";

            return Redirect(returnUrl);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Login failed: {ex.Message}");
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Logout()
    {
        return SignOut("Cookies", "oidc");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}

public class LoginViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
