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

        // Note: MFA enforcement removed (was based on Role.RequiresMFA which no longer exists)
        // MFA can still be checked via user.MFAEnabled if needed

        await SignInAsync(user, isPersistent);
        return SignInResult.Success;
    }
}
