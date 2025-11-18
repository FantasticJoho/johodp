# Account Management & Password Flows

This document describes the account management flows available in the Johodp Identity Provider.

## Overview

Johodp provides a complete account management system built on ASP.NET Core Identity, integrated with the domain-driven design architecture. Users can register, log in, reset passwords, and manage their accounts through intuitive web forms.

## Endpoints

### Authentication Flows

#### Login (`/account/login`)
- **GET** — Display login form
- **POST** — Authenticate user by email and password
  - Creates a new user if email does not exist (demo mode)
  - Verifies password hash via `UserManager.CheckPasswordAsync`
  - Enforces MFA if user's roles require it (via `CustomSignInManager`)
  - Sets secure session cookie (7-day sliding expiration)

#### Register (`/account/register`)
- **GET** — Display registration form
- **POST** — Create new account
  - Validates email uniqueness
  - Creates domain `User` aggregate via `User.Create(email, firstName, lastName)`
  - Hashes password via `UserManager.CreateAsync(user, password)`
  - Signs user in automatically on success
  - Returns 409 Conflict if email already registered

#### Logout (`/account/logout`)
- **GET** — Sign out and clear session
- Clears both "Cookies" and "oidc" schemes
- Redirects to login page

### Password Recovery

#### Forgot Password (`/account/forgot-password`)
- **GET** — Display email input form
- **POST** — Initiate password reset
  - Accepts email address
  - Generates password reset token via `UserManager.GeneratePasswordResetTokenAsync(user)`
  - **Development mode:** Prints token to console for manual testing
  - **Production:** Should send email with reset link containing token
  - Returns confirmation page (doesn't reveal if email exists for security)

#### Reset Password (`/account/reset-password`)
- **GET** — Display password reset form (requires `token` query param)
  - Example: `/account/reset-password?token=<resettoken>`
- **POST** — Apply new password
  - Validates password confirmation match
  - Resets password via `UserManager.ResetPasswordAsync(user, token, newPassword)`
  - Returns confirmation on success
  - Returns error if token is invalid or expired

### Confirmation Pages

- **ForgotPasswordConfirmation** (`/account/forgot-password-confirmation`) — Informs user to check their email
- **ResetPasswordConfirmation** (`/account/reset-password-confirmation`) — Confirms password has been reset; user can now log in

## Session Management

### Cookie Authentication

- **Scheme:** "Cookies"
- **Duration:** 7 days from last activity (sliding expiration)
- **HttpOnly:** Yes (secure against XSS)
- **Secure:** Yes (HTTPS only in production)
- **SameSite:** Lax (CSRF protection)
- **LoginPath:** `/account/login` (redirect on 401)
- **LogoutPath:** `/account/logout`
- **AccessDeniedPath:** `/account/accessdenied`

### Claims in Session

The session cookie carries claims including:
- `sub` — Subject (user ID)
- `email` — Email address
- `given_name` — First name
- `family_name` — Last name
- `role` — User roles (from domain aggregate)
- `permission` — User permissions (from domain aggregate)
- `scope` — User scope/organization

## Domain Integration

### User Aggregate

```csharp
// src/Johodp.Domain/Users/Aggregates/User.cs

public class User : AggregateRoot
{
    public Email Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string? PasswordHash { get; private set; }  // ← Set by UserStore
    public bool IsActive { get; private set; }
    public bool EmailConfirmed { get; private set; }
    
    public static User Create(string email, string firstName, string lastName)
    {
        var user = new User
        {
            Id = UserId.CreateUnique(),
            Email = Email.Create(email),
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            EmailConfirmed = false,
        };
        
        user.AddDomainEvent(new UserRegisteredEvent(user.Id, user.Email, user.FirstName, user.LastName));
        return user;
    }
    
    public void SetPasswordHash(string? hash)
    {
        PasswordHash = hash;
    }
    
    public bool RequiresMFA() => Roles.Any(r => r.RequiresMFA);
}
```

### UserStore Implementation

The `UserStore` (in `src/Johodp.Infrastructure/Identity/UserStore.cs`) implements ASP.NET Identity stores to persist user data in the domain:

```csharp
public class UserStore : 
    IUserStore<User>,
    IUserPasswordStore<User>,
    IUserEmailStore<User>
{
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.CommitAsync();
        return IdentityResult.Success;
    }
    
    public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        _unitOfWork.Users.Update(user);
        await _unitOfWork.CommitAsync();
        return IdentityResult.Success;
    }
    
    public async Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.SetPasswordHash(passwordHash);
    }
    
    public async Task<string?> GetPasswordHashAsync(User user, CancellationToken cancellationToken)
    {
        return user.PasswordHash;
    }
    
    // Additional methods for email, confirmation, etc.
}
```

### CustomSignInManager

The `CustomSignInManager` (in `src/Johodp.Infrastructure/Identity/CustomSignInManager.cs`) extends the standard SignInManager to enforce MFA:

```csharp
public class CustomSignInManager : SignInManager<User>
{
    public override async Task<SignInResult> PasswordSignInAsync(
        string userName, string password, bool isPersistent, bool lockoutOnFailure)
    {
        var user = await UserManager.FindByEmailAsync(userName);
        if (user == null)
            return SignInResult.Failed;
        
        if (!await UserManager.CheckPasswordAsync(user, password))
            return SignInResult.Failed;
        
        // Enforce MFA if required by user's roles
        if (user.RequiresMFA())
            return SignInResult.TwoFactorRequired;
        
        await SignInAsync(user, isPersistent);
        return SignInResult.Success;
    }
}
```

## Testing Account Flows

### Local Testing (Development Mode)

```bash
# Run the application
dotnet run --project src/Johodp.Api

# Navigate to login page
# http://localhost:5000/account/login
```

#### Test Registration
1. Click "Register" link on login page
2. Enter email, first name, last name, password
3. Submit — user is created and automatically signed in
4. Verify session cookie is set (check browser DevTools > Application > Cookies)

#### Test Login
1. Go to logout or open incognito window
2. Go to `/account/login`
3. Enter the email and password from registration
4. Submit — user is signed in, session cookie created

#### Test Password Reset (Development)
1. Go to `/account/forgot-password`
2. Enter email of registered user
3. Look at console output for the reset token (printed line like `Password reset token for user@email.com: <token>`)
4. Navigate to `/account/reset-password?token=<token>`
5. Enter new password and confirm
6. Submit — password is reset
7. Try logging in with the new password

#### Test MFA Enforcement
1. Register a user
2. Assign user to a role with `RequiresMFA = true` (e.g., via database or admin endpoint)
3. Log out
4. Try logging in with that user's credentials
5. Expect `SignInResult.RequiresTwoFactor` — UI should redirect to 2FA challenge (not yet implemented)

## Email Notifications (Future)

Currently, password reset tokens are logged to console in development. To enable email notifications:

1. Inject an `IEmailService` into `AccountController`
2. In `ForgotPassword` POST, call:
   ```csharp
   var resetLink = Url.Action("ResetPassword", "Account", 
       new { token = token }, Request.Scheme);
   await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);
   ```
3. Similarly, implement email confirmation tokens in the registration flow

## Security Considerations

- **Password Hashing:** Uses `IPasswordHasher<TUser>` (PBKDF2 by default, can be customized)
- **Token Expiration:** Password reset tokens expire after a default period (configurable in Identity options)
- **CSRF Protection:** SameSite=Lax cookie; Anti-forgery tokens on forms (implement if forms added)
- **HTTPS Only:** Secure flag set in production
- **Session Timeout:** 7 days sliding expiration (customizable)
- **MFA Support:** Can be enforced per role; 2FA challenge flow should be implemented
- **Email Enumeration:** Forgot password intentionally doesn't reveal if email exists (security best practice)

## Configuration

All Identity configuration is in `src/Johodp.Api/Extensions/ServiceCollectionExtensions.cs`:

```csharp
services.AddIdentityCore<User>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireDigit = false;
    options.SignIn.RequireConfirmedEmail = false;  // Set to true to enforce email confirmation
})
.AddSignInManager<CustomSignInManager>()
.AddUserStore<UserStore>()
.AddDefaultTokenProviders();

services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/accessdenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });
```

## References

- [ASP.NET Core Identity Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity/)
- [Password Hashing in ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration/)
- [Cookie Authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie/)
- [OWASP Authentication Best Practices](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
