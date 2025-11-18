namespace Johodp.Infrastructure.Identity;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Johodp.Domain.Users.Aggregates;

public class CustomSignInManager : SignInManager<User>
{
    public CustomSignInManager(UserManager<User> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<User> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<User>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<User> confirmation)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
    }

    public override async Task<SignInResult> PasswordSignInAsync(string userName, string password, bool isPersistent, bool lockoutOnFailure)
    {
        var user = await UserManager.FindByEmailAsync(userName);
        if (user == null)
            return SignInResult.Failed;

        // Verify password using UserManager (which uses IPasswordHasher)
        var passwordValid = await UserManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
            return SignInResult.Failed;

        // Enforce MFA for roles that require it
        if (user.RequiresMFA())
        {
            // In a real implementation you'd initiate a 2FA flow here.
            return SignInResult.TwoFactorRequired;
        }

        await SignInAsync(user, isPersistent);
        return SignInResult.Success;
    }
}
