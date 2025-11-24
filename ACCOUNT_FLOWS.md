# Account Management & Password Flows

This document describes the account management flows available in the Johodp Identity Provider.

## Overview

Johodp provides a complete account management system built on ASP.NET Core Identity, integrated with the domain-driven design architecture.

**Registration Flow:**
- **API-only Registration** — Third-party applications validate registration requests and trigger user creation via REST API
- Users are created in `PendingActivation` status and receive an activation email automatically (event-driven)
- No web forms or UI provided by the Identity Provider (headless/API-first architecture)

All user creation triggers automatic activation email sending via an event-driven architecture.

## Endpoints

### User Registration & Activation Flow

#### Register via API (`/api/users/register`)
**Primary flow for third-party application integration**

- **POST** — Create new user account (pending activation)
  - External application calls this endpoint after receiving and validating a registration request
  - Request body:
    ```json
    {
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "tenantId": "acme-corp",
      "requestId": "optional-tracking-id"
    }
    ```
  - Creates domain `User` aggregate with `Status = PendingActivation`
  - Triggers `UserPendingActivationEvent` (domain event)
  - Event handler (`SendActivationEmailHandler`) automatically:
    - Generates activation token via `UserManager.GenerateEmailConfirmationTokenAsync()`
    - Sends activation email via `IEmailService` (currently logged to console)
  - Returns:
    ```json
    {
      "userId": "guid",
      "email": "user@example.com",
      "status": "PendingActivation",
      "message": "User created successfully. Activation email will be sent."
    }
    ```

#### Activate Account (`/api/auth/activate`)
- **POST** — Activate user account with token and set password
  - Request body:
    ```json
    {
      "token": "activation-token-from-email",
      "userId": "user-guid",
      "newPassword": "SecureP@ssw0rd",
      "confirmPassword": "SecureP@ssw0rd"
    }
    ```
  - Confirms email via `UserManager.ConfirmEmailAsync(user, token)`
  - Sets password via `UserManager.AddPasswordAsync(user, newPassword)`
  - Updates user status to `Active`
  - Returns 200 OK on success
  - Token expires after 24 hours (configurable)

### Authentication Flows

#### Login (`/api/auth/login`)
- **POST** — Authenticate user by email and password (JSON API)
  - Request body:
    ```json
    {
      "email": "user@example.com",
      "password": "SecureP@ssw0rd",
      "tenantId": "acme-corp"  // Optional: defaults to "*" (wildcard)
    }
    ```
  - Verifies password hash via `UserManager.CheckPasswordAsync`
  - Enforces MFA if client requires it (via `IMfaAuthenticationService`)
  - Sets secure session cookie (`.AspNetCore.Identity.Application`)
  - Cookie settings: HttpOnly, Secure (production), SameSite=Lax, 7-day sliding expiration
  - Returns 200 OK with success message on authentication
  - Returns 401 Unauthorized if credentials invalid

#### Logout (`/api/auth/logout`)
- **POST** — Sign out and clear session
- Clears authentication cookies
- Returns 200 OK

### IdentityServer Configuration

**Architecture:** Headless Identity Provider with API-only endpoints.

**User Interaction Configuration:**
```csharp
services.AddIdentityServer(options =>
{
    options.UserInteraction.LoginUrl = "/api/auth/login";
    options.UserInteraction.LoginReturnUrlParameter = "returnUrl";
});
```

When IdentityServer detects an unauthenticated user during an OAuth2 authorization request, it redirects to `/api/auth/login?returnUrl={authorize_url}`. 

**Flow:**
1. Client navigates to `/connect/authorize` (not authenticated)
2. IdentityServer redirects to `/api/auth/login?returnUrl=...`
3. Client application handles login UI (can be SPA, mobile app, etc.)
4. After successful login, client redirects back to `returnUrl`
5. IdentityServer completes authorization and returns code/tokens

**Current Implementation:**
- Login endpoint: `/api/auth/login` (JSON API)
- Clients provide their own login UI
- No consent required (`RequireConsent = false` on all clients)
- No error pages (errors returned as JSON responses)

**Note:** Your client application must:
- Detect the `returnUrl` query parameter
- Show login form to user
- Call `POST /api/auth/login` to authenticate
- Redirect to `returnUrl` after successful authentication

### Password Recovery

#### Forgot Password (`/account/forgot-password`)
- **GET** — Display email input form
- **POST** — Initiate password reset
  - Accepts email address
  - Generates password reset token via `UserManager.GeneratePasswordResetTokenAsync(user)`
  - **Development mode:** Emails are logged to console (via `IEmailService`)
  - **Production:** Configure `IEmailService` with SMTP/SendGrid/AWS SES for real email delivery
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

## Email Service Architecture

### IEmailService Interface

Located in `src/Johodp.Application/Common/Interfaces/IEmailService.cs`:

```csharp
public interface IEmailService
{
    /// Sends activation email with token
    Task<bool> SendActivationEmailAsync(
        string email, string firstName, string lastName, 
        string activationToken, Guid userId, string? tenantId = null);
    
    /// Sends password reset email
    Task<bool> SendPasswordResetEmailAsync(
        string email, string firstName, 
        string resetToken, Guid userId);
    
    /// Sends welcome email after activation
    Task<bool> SendWelcomeEmailAsync(
        string email, string firstName, string lastName, 
        string? tenantName = null);
    
    /// Generic email sender
    Task<bool> SendEmailAsync(
        string email, string subject, string body);
}
```

### EmailService Implementation

Located in `src/Johodp.Infrastructure/Services/EmailService.cs`:

**Current behavior (Development):**
- Logs all email details to console:
  - Email recipient
  - Subject line
  - Activation/reset URL
  - Full HTML body with professional template
- Returns `true` (simulates successful send)

**To enable real email sending:**
1. Add email provider package (e.g., `MailKit`, `SendGrid`, `AWS.SimpleEmail`)
2. Update `EmailService` constructor to inject email client
3. Replace `await Task.CompletedTask` with actual SMTP/API call
4. Configure credentials in `appsettings.json`

Example template structure:
```html
<html>
  <body style="gradient background">
    <h1>Activate Your Account</h1>
    <p>Hello {firstName} {lastName},</p>
    <p>Click the button below to activate:</p>
    <a href="{activationUrl}" class="button">Activate</a>
    <p>Link expires in 24 hours.</p>
  </body>
</html>
```

### IUserActivationService

Located in `src/Johodp.Application/Common/Interfaces/IUserActivationService.cs`:

Bridges the Application layer and Infrastructure (ASP.NET Identity):

```csharp
public interface IUserActivationService
{
    /// Generates activation token and sends email
    Task<bool> SendActivationEmailAsync(
        Guid userId, string email, string firstName, 
        string lastName, string? tenantId = null);
    
    /// Activates user account with token
    Task<bool> ActivateUserAsync(
        Guid userId, string activationToken, string newPassword);
}
```

### UserActivationService Implementation

Located in `src/Johodp.Infrastructure/Services/UserActivationService.cs`:

**Responsibilities:**
1. Retrieves user from `UserManager<User>`
2. Generates activation token via `GenerateEmailConfirmationTokenAsync()`
3. Calls `IEmailService.SendActivationEmailAsync()`
4. For activation: confirms email, sets password, activates user

**Architecture benefits:**
- **Clean separation:** Application layer doesn't depend on ASP.NET Identity
- **Testable:** Can mock `IUserActivationService` in tests
- **Reusable:** Any part of the system can trigger activation emails

## Event-Driven Email Flow

### Registration Flow (Complete)

```
1. POST /api/users/register
   ↓
2. RegisterUserCommandHandler
   ↓
3. User.Create() → User aggregate created (Status: PendingActivation)
   ↓
4. UserPendingActivationEvent added to aggregate
   ↓
5. DomainEventPublisher publishes event to EventBus
   ↓
6. DomainEventProcessor processes events asynchronously
   ↓
7. SendActivationEmailHandler.HandleAsync()
   ↓
8. IUserActivationService.SendActivationEmailAsync()
   ↓
9. UserManager generates activation token
   ↓
10. IEmailService.SendActivationEmailAsync()
   ↓
11. [EMAIL] Logs to console (dev) or sends via SMTP (prod)
```

### Key Events

**UserPendingActivationEvent** (Domain layer):
```csharp
public class UserPendingActivationEvent : DomainEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string? TenantId { get; set; }
}
```

**SendActivationEmailHandler** (Application layer):
```csharp
public class SendActivationEmailHandler : IEventHandler<UserPendingActivationEvent>
{
    private readonly IUserActivationService _userActivationService;
    
    public async Task HandleAsync(UserPendingActivationEvent @event, ...)
    {
        await _userActivationService.SendActivationEmailAsync(
            @event.UserId,
            @event.Email, 
            @event.FirstName,
            @event.LastName,
            @event.TenantId);
    }
}
```

**Benefits of this architecture:**
- ✅ Automatic email sending when user created from any source
- ✅ Decoupled: Controllers don't need to know about emails
- ✅ Testable: Mock event handlers in tests
- ✅ Extensible: Add more handlers for user creation (analytics, webhooks, etc.)

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

### User Aggregate (Updated)

```csharp
// src/Johodp.Domain/Users/Aggregates/User.cs

public class User : AggregateRoot
{
    public UserId Id { get; private set; }
    public Email Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string? PasswordHash { get; private set; }
    public UserStatus Status { get; private set; }  // NEW: PendingActivation, Active, Suspended, Deleted
    public bool EmailConfirmed { get; private set; }
    public DateTime CreatedAt { get; private set; }
    
    private readonly List<string> _tenantIds = new();
    public IReadOnlyList<string> TenantIds => _tenantIds.AsReadOnly();
    
    /// Creates user in pending activation state
    public static User Create(
        string email, 
        string firstName, 
        string lastName,
        string? tenantId = null,
        bool createAsPending = true)
    {
        var user = new User
        {
            Id = UserId.CreateUnique(),
            Email = Email.Create(email),
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = false,
            Status = createAsPending ? UserStatus.PendingActivation : UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            user._tenantIds.Add(tenantId);
        }
        
        if (createAsPending)
        {
            // Event triggers email sending automatically
            user.AddDomainEvent(new UserPendingActivationEvent(
                user.Id.Value,
                user.Email.Value,
                user.FirstName,
                user.LastName,
                tenantId
            ));
        }
        else
        {
            user.AddDomainEvent(new UserRegisteredEvent(
                user.Id.Value,
                user.Email.Value,
                user.FirstName,
                user.LastName
            ));
        }
        
        return user;
    }
    
    public void SetPasswordHash(string? hash)
    {
        PasswordHash = hash;
    }
    
    public void Activate()
    {
        Status = UserStatus.Active;
        EmailConfirmed = true;
        AddDomainEvent(new UserActivatedEvent(Id.Value, Email.Value));
    }
    
    public void Suspend()
    {
        Status = UserStatus.Suspended;
    }
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

### CustomSignInManager (Updated)

The `CustomSignInManager` (in `src/Johodp.Infrastructure/Identity/CustomSignInManager.cs`) extends the standard SignInManager to integrate with the domain and enforce client-specific MFA:

```csharp
public class CustomSignInManager : SignInManager<User>
{
    private readonly IMfaAuthenticationService _mfaService;
    private readonly ITenantRepository _tenantRepository;
    
    public override async Task<SignInResult> PasswordSignInAsync(
        string userName, string password, bool isPersistent, bool lockoutOnFailure)
    {
        var user = await UserManager.FindByEmailAsync(userName);
        if (user == null)
            return SignInResult.Failed;
        
        // Check if user is active
        if (user.Status != UserStatus.Active)
            return SignInResult.NotAllowed;
        
        if (!await UserManager.CheckPasswordAsync(user, password))
            return SignInResult.Failed;
        
        // Client-specific MFA enforcement is handled separately
        // via IMfaAuthenticationService in AccountController
        
        await SignInAsync(user, isPersistent);
        return SignInResult.Success;
    }
}
```

### MFA Integration

MFA is enforced **per client**, not per user role. The flow:

1. User logs in via `/api/auth/login` with optional `tenantId`
2. `AccountController` checks if client requires MFA:
   ```csharp
   var client = await _clientRepository.GetByNameAsync(clientId);
   if (client?.RequireMfa == true)
   {
       var mfaResult = await _mfaService.AuthenticateAsync(user, client, tenantId);
       if (!mfaResult.Success)
           return Unauthorized("MFA required");
   }
   ```
3. If MFA required, client must implement 2FA challenge
4. Current implementation: MFA placeholder (returns success)

## Testing Account Flows

### Testing User Registration & Activation (Current Implementation)

```bash
# Run the application
dotnet run --project src/Johodp.Api

# The API is now running on http://localhost:5000
```

#### Test User Registration via API
```powershell
# Create a new user
$body = @{
    email = 'newuser@example.com'
    firstName = 'John'
    lastName = 'Doe'
    tenantId = 'acme-corp'
} | ConvertTo-Json

Invoke-WebRequest -Uri "http://localhost:5000/api/users/register" `
    -Method POST `
    -Body $body `
    -ContentType 'application/json'

# Response:
# {
#   "userId": "guid",
#   "email": "newuser@example.com",
#   "status": "PendingActivation",
#   "message": "User created successfully. Activation email will be sent."
# }

# Check console logs for email details:
# [EMAIL] Sending activation email to newuser@example.com
# [EMAIL] Subject: Activez votre compte
# [EMAIL] Activation URL: http://localhost:5000/account/activate?token=...
# [EMAIL] Body: <full HTML email>
# [EMAIL] ✅ Activation email logged successfully
```

#### Test Account Activation
```powershell
# Extract the activation token from console logs
$activationBody = @{
    token = 'ACTIVATION_TOKEN_FROM_LOGS'
    userId = 'USER_GUID_FROM_REGISTRATION'
    newPassword = 'SecureP@ssw0rd123!'
    confirmPassword = 'SecureP@ssw0rd123!'
} | ConvertTo-Json

Invoke-WebRequest -Uri "http://localhost:5000/api/auth/activate" `
    -Method POST `
    -Body $activationBody `
    -ContentType 'application/json'

# Response: 200 OK
# User is now Active and can log in
```

#### Test Login
```powershell
# Login with activated user
$loginBody = @{
    email = 'newuser@example.com'
    password = 'SecureP@ssw0rd123!'
    tenantId = 'acme-corp'
} | ConvertTo-Json

$session = $null
$response = Invoke-WebRequest -Uri "http://localhost:5000/api/auth/login" `
    -Method POST `
    -Body $loginBody `
    -ContentType 'application/json' `
    -SessionVariable session

# Cookie is set in $session
$session.Cookies.GetCookies("http://localhost:5000")
# Output: .AspNetCore.Identity.Application cookie
```

#### Test Complete OAuth2 PKCE Flow
```powershell
# After login, test authorization
$authUrl = "http://localhost:5000/connect/authorize?" + 
    "response_type=code&" +
    "client_id=johodp-spa&" +
    "redirect_uri=http://localhost:4200/callback&" +
    "scope=openid profile email johodp.identity johodp.api&" +
    "code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&" +
    "code_challenge_method=S256&" +
    "state=random-state&" +
    "nonce=random-nonce"

$authResponse = Invoke-WebRequest -Uri $authUrl `
    -WebSession $session `
    -MaximumRedirection 0 `
    -ErrorAction SilentlyContinue

# Extract authorization code from redirect Location header
$code = ($authResponse.Headers.Location -split 'code=')[1] -split '&' | Select-Object -First 1

# Exchange code for tokens
$tokenBody = "grant_type=authorization_code&" +
    "client_id=johodp-spa&" +
    "code=$code&" +
    "redirect_uri=http://localhost:4200/callback&" +
    "code_verifier=dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"

$tokenResponse = Invoke-WebRequest -Uri "http://localhost:5000/connect/token" `
    -Method POST `
    -Body $tokenBody `
    -ContentType 'application/x-www-form-urlencoded'

$tokens = $tokenResponse.Content | ConvertFrom-Json
# $tokens.access_token - JWT access token
# $tokens.id_token - OIDC identity token
# $tokens.refresh_token - Refresh token
```

### Local Testing (Development Mode)

### Testing Legacy Web Forms (If Enabled)

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

## Email Notifications

### Current Implementation (Development)

All emails are **logged to console** with full details:
- Recipient email address
- Subject line
- Activation/reset URL with token
- Complete HTML body (professionally styled)

**Console output example:**
```
[EMAIL] Sending activation email to user@example.com (User: John Doe, UserId: guid, Tenant: acme-corp)
[EMAIL] Subject: Activez votre compte
[EMAIL] Activation URL: http://localhost:5000/account/activate?token=CfDJ8...&userId=guid&tenant=acme-corp
[EMAIL] Body:
<!DOCTYPE html>
<html>
<head>
    <style>
        .container { max-width: 600px; margin: 0 auto; }
        .header { background: linear-gradient(135deg, #667eea, #764ba2); }
        .button { background: #667eea; color: white; padding: 12px 30px; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>Activez votre compte</h1>
        </div>
        <div class="content">
            <p>Bonjour John Doe,</p>
            <p>Cliquez sur le bouton pour activer :</p>
            <a href="..." class="button">Activer mon compte</a>
            <p>Ce lien expire dans 24 heures.</p>
        </div>
    </div>
</body>
</html>
[EMAIL] ✅ Activation email logged successfully for user@example.com
```

### Production Configuration

To enable **real email sending**, update `EmailService.cs`:

#### Option 1: SMTP (MailKit)
```csharp
// Install: dotnet add package MailKit
public class EmailService : IEmailService
{
    private readonly ISmtpClient _smtpClient;
    private readonly IConfiguration _config;
    
    public async Task<bool> SendActivationEmailAsync(...)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Johodp", "noreply@johodp.com"));
        message.To.Add(new MailboxAddress($"{firstName} {lastName}", email));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = body };
        
        await _smtpClient.ConnectAsync(_config["Smtp:Host"], 587, SecureSocketOptions.StartTls);
        await _smtpClient.AuthenticateAsync(_config["Smtp:Username"], _config["Smtp:Password"]);
        await _smtpClient.SendAsync(message);
        await _smtpClient.DisconnectAsync(true);
        
        return true;
    }
}
```

#### Option 2: SendGrid
```csharp
// Install: dotnet add package SendGrid
public class EmailService : IEmailService
{
    private readonly ISendGridClient _sendGridClient;
    
    public async Task<bool> SendActivationEmailAsync(...)
    {
        var msg = new SendGridMessage
        {
            From = new EmailAddress("noreply@johodp.com", "Johodp"),
            Subject = subject,
            HtmlContent = body
        };
        msg.AddTo(new EmailAddress(email, $"{firstName} {lastName}"));
        
        var response = await _sendGridClient.SendEmailAsync(msg);
        return response.IsSuccessStatusCode;
    }
}
```

#### Option 3: AWS SES
```csharp
// Install: dotnet add package AWSSDK.SimpleEmail
public class EmailService : IEmailService
{
    private readonly IAmazonSimpleEmailService _sesClient;
    
    public async Task<bool> SendActivationEmailAsync(...)
    {
        var request = new SendEmailRequest
        {
            Source = "noreply@johodp.com",
            Destination = new Destination { ToAddresses = new List<string> { email } },
            Message = new Message
            {
                Subject = new Content(subject),
                Body = new Body { Html = new Content(body) }
            }
        };
        
        var response = await _sesClient.SendEmailAsync(request);
        return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
    }
}
```

### Configuration (appsettings.json)

```json
{
  "Email": {
    "Provider": "SMTP",  // or "SendGrid" or "AWS"
    "BaseUrl": "https://yourapp.com",
    "From": "noreply@johodp.com",
    "FromName": "Johodp Identity Platform"
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "EnableSsl": true
  },
  "SendGrid": {
    "ApiKey": "SG.your-api-key"
  },
  "AWS": {
    "Region": "us-east-1",
    "AccessKey": "your-access-key",
    "SecretKey": "your-secret-key"
  }
}
```

## Email Notifications (Future)

Currently, password reset tokens are logged to console in development. To enable email notifications:

### Extending Email Functionality

The `IEmailService` already supports password reset emails. To use them:

1. In `ForgotPassword` POST action, call:
   ```csharp
   var token = await _userManager.GeneratePasswordResetTokenAsync(user);
   await _emailService.SendPasswordResetEmailAsync(
       user.Email.Value, 
       user.FirstName, 
       token, 
       user.Id.Value);
   ```

2. Welcome emails after activation:
   ```csharp
   // In AccountController.Activate after successful activation
   await _emailService.SendWelcomeEmailAsync(
       user.Email.Value,
       user.FirstName,
       user.LastName,
       tenantName);
   ```

All email templates are already implemented in `EmailService.cs` with professional HTML styling.

## Security Considerations

- **Password Hashing:** Uses `IPasswordHasher<TUser>` (PBKDF2 by default, can be customized)
- **Token Expiration:** 
  - Activation tokens expire after 24 hours (configured via `DataProtectionTokenProviderOptions`)
  - Password reset tokens expire after 24 hours (default)
  - Tokens are single-use and invalidated after successful use
- **CSRF Protection:** SameSite=Lax cookie; Anti-forgery tokens on forms (if forms enabled)
- **HTTPS Only:** Secure flag set in production (`CookieSecurePolicy.SameAsRequest`)
- **Session Timeout:** 7 days sliding expiration (customizable via `ExpireTimeSpan`)
- **MFA Support:** 
  - Enforced **per client** (not per user role)
  - Checked via `client.RequireMfa` flag in database
  - Integrated with `IMfaAuthenticationService`
- **Email Enumeration:** 
  - Forgot password intentionally doesn't reveal if email exists (security best practice)
  - Registration returns 201 Created even if user pending external validation
- **User Status Validation:**
  - Only `Active` users can log in
  - `PendingActivation` users blocked until activation complete
  - `Suspended` and `Deleted` users cannot authenticate
- **Cookie Security:**
  - HttpOnly: Yes (prevents XSS attacks)
  - Secure: Yes in production (HTTPS only)
  - SameSite: Lax (CSRF protection while allowing OAuth2 flows)
  - Name: `.AspNetCore.Identity.Application`
- **OAuth2 Security:**
  - PKCE required for all authorization code flows
  - Client secrets optional (public SPAs use PKCE without secrets)
  - Redirect URIs validated against tenant configuration
  - State parameter required (CSRF protection)
  - Nonce parameter recommended (replay attack prevention)

## Configuration

All Identity and authentication configuration is in `src/Johodp.Api/Extensions/ServiceCollectionExtensions.cs`:

```csharp
// ASP.NET Identity Core with domain User aggregate
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

// Configure activation token lifespan (24 hours)
services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(24);
});

// Application cookie for web sessions
services.ConfigureApplicationCookie(opts =>
{
    opts.Cookie.Name = ".AspNetCore.Identity.Application";
    opts.Cookie.SameSite = SameSiteMode.Lax;
    opts.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    opts.Cookie.HttpOnly = true;
    opts.ExpireTimeSpan = TimeSpan.FromDays(7);
    opts.SlidingExpiration = true;
});

// Register email and activation services
services.AddScoped<IEmailService, EmailService>();
services.AddScoped<IUserActivationService, UserActivationService>();

// Domain event infrastructure
services.AddSingleton<IEventBus, ChannelEventBus>();
services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
services.AddHostedService<DomainEventProcessor>();

// Event handlers (registered as scoped)
services.AddScoped<IEventHandler<UserPendingActivationEvent>, 
    SendActivationEmailHandler>();
services.AddScoped<IEventHandler<UserActivatedEvent>, 
    UserActivatedEventHandler>();

// IdentityServer with custom client store (dynamic loading from DB)
services.AddIdentityServer()
    .AddInMemoryApiScopes(IdentityServerConfig.GetApiScopes())
    .AddInMemoryApiResources(IdentityServerConfig.GetApiResources())
    .AddInMemoryIdentityResources(IdentityServerConfig.GetIdentityResources())
    .AddOperationalStore(options =>
    {
        options.ConfigureDbContext = b =>
            b.UseNpgsql(connectionString,
                sql => sql.MigrationsAssembly("Johodp.Infrastructure"));
        options.DefaultSchema = "dbo";
        options.EnableTokenCleanup = true;
        options.TokenCleanupInterval = 3600; // 1 hour
    })
    .AddAspNetIdentity<User>()
    .AddDeveloperSigningCredential();

// Custom client store (loads clients dynamically from database)
services.AddScoped<IClientStore, CustomClientStore>();

// Profile service (maps domain user to OIDC claims)
services.AddScoped<IProfileService, IdentityServerProfileService>();
```

## Architecture Summary

### Current Implementation Status

✅ **Implemented:**
- User registration via API with external app validation
- Automatic activation email generation and logging
- Event-driven architecture for email sending
- Account activation with token and password setup
- Login with tenant-aware authentication
- OAuth2/OIDC authorization code + PKCE flow
- Dynamic client loading from database
- Multi-tenant support with tenant-specific redirect URIs
- Client-specific MFA enforcement (placeholder)
- Session management with secure cookies
- Domain-driven design with proper aggregate boundaries
- Clean architecture separation (Domain → Application → Infrastructure → API)

⏳ **In Development:**
- Real email delivery (SMTP/SendGrid/AWS SES integration)
- MFA challenge flow implementation
- Password reset via email
- Welcome emails after activation

📋 **Planned:**
- Web-based registration forms (if needed)
- Admin portal for user management
- Audit logging for authentication events
- Rate limiting on auth endpoints
- Account lockout after failed attempts
- Email verification links
- Social login integration (Google, Microsoft, etc.)

## References

- [ASP.NET Core Identity Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity/)
- [Duende IdentityServer Documentation](https://docs.duendesoftware.com/identityserver/v7/)
- [OAuth 2.0 PKCE RFC 7636](https://datatracker.ietf.org/doc/html/rfc7636)
- [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html)
- [Password Hashing in ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration/)
- [Cookie Authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie/)
- [OWASP Authentication Best Practices](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [Domain-Driven Design Reference](https://www.domainlanguage.com/ddd/reference/)
- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
