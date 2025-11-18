# Johodp - Identity Provider DDD Architecture

Une application Identity Provider (IDP) basée sur .NET 8, utilisant les principes du Domain-Driven Design (DDD), IdentityServer4 et PostgreSQL.

## Architecture

Le projet est organisé selon les principes DDD :

### Couches

1. **Johodp.Domain** - Couche métier
   - Agrégats : `User`, `Client`
   - Value Objects : `UserId`, `Email`, `ClientId`, `ClientSecret`
   - Domain Events : `UserRegisteredEvent`, `UserEmailConfirmedEvent`, `ClientCreatedEvent`
   - Classes de base : `AggregateRoot`, `ValueObject`, `DomainEvent`

2. **Johodp.Application** - Couche application
   - Commands & Handlers (CQRS)
   - Queries & Handlers
   - DTOs (Data Transfer Objects)
   - Interfaces de dépôt

3. **Johodp.Infrastructure** - Couche infrastructure
   - Implémentation Entity Framework Core
   - Configuration de PostgreSQL
   - Repositories
   - Unit of Work
   - Services IdentityServer4

4. **Johodp.Api** - Couche présentation
   - Contrôleurs REST
   - Configuration du démarrage
   - Extensions de services

## Prérequis

- .NET 8.0 SDK
- PostgreSQL 12+
- Docker (optionnel pour PostgreSQL)

## Installation

### 1. Configuration de PostgreSQL avec Docker

```bash
docker run --name johodp-postgres \
  -e POSTGRES_PASSWORD=password \
  -e POSTGRES_DB=johodp \
  -p 5432:5432 \
  -d postgres:15
```

### 2. Restaurer les dépendances

```bash
dotnet restore
```

### 3. Appliquer les migrations

**Bash/Shell:**
```bash
dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api
```

**PowerShell:**
```powershell
dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api
```

### 4. Lancer l'application

```bash
dotnet run --project src/Johodp.Api
```

L'API sera disponible sur `https://localhost:5001`

## Utilisation

### Enregistrer un utilisateur

```bash
POST /api/users/register
Content-Type: application/json

{
  "email": "user@example.com",
  "firstName": "Jean",
  "lastName": "Dupont"
}
```

## ASP.NET Identity integration (local)

The project now includes a light integration with ASP.NET Core Identity using the domain `User` aggregate. This enables password hashing, sign-in flows and integration with the UI login page.

Key points:
- `UserStore` (in `src/Johodp.Infrastructure/Identity/UserStore.cs`) implements the minimal Identity stores required (user lookup, password hash, email).
- `CustomSignInManager` (in `src/Johodp.Infrastructure/Identity/CustomSignInManager.cs`) overrides `PasswordSignInAsync` to verify credentials via `UserManager` and enforces MFA when the user's roles require it.
- The `User` aggregate now contains a `PasswordHash` property and a `SetPasswordHash` method to persist password hashes via the store.
- Cookie-based authentication is configured with a 7-day sliding expiration window.

### Recent changes (2025-11-18)

- IdentityServer PKCE support: added PKCE-ready clients (`johodp-spa`, `swagger-ui`) using Authorization Code + PKCE. Use `RequirePkce = true` for public clients and `RequireClientSecret = false` where appropriate.
- Fixed duplicate scope configuration: identity scopes (`openid`, `profile`, `email`) are declared as IdentityResources only (not duplicated in API scopes) to avoid IdentityServer configuration errors.
- Middleware ordering: authentication is now enabled before IdentityServer (`app.UseAuthentication()` executed prior to `app.UseIdentityServer()`), and routing is enabled so IdentityServer endpoints see the authenticated principal.
- ASP.NET Identity integration: IdentityServer is wired with ASP.NET Identity (`AddAspNetIdentity<TUser>()`) and a minimal `IUserClaimsPrincipalFactory<User>` implementation (`DomainUserClaimsPrincipalFactory`) is provided to build ClaimsPrincipal from the domain `User`.
- Cookie configuration: the application cookie name and attributes are set explicitly for development (`.AspNetCore.Identity.Application`, `SameSite=Lax`, `SecurePolicy=SameAsRequest`) to make cookies visible on `http://localhost` during local testing. For cross-origin PKCE/SPAs prefer HTTPS and `SameSite=None` + `Secure`.
- Claims debug page: a new authenticated Razor page is available at `/account/claims` to display the current user's claims (useful to verify which claims are present in the cookie and what IdentityServer will emit).

#### Quick PKCE authorize URL (example)

You can test the PKCE authorize flow by pasting this authorize URL into the browser (on the IdentityServer host `http://localhost:5000`) after starting the application:

```
http://localhost:5000/connect/authorize?response_type=code&client_id=johodp-spa&redirect_uri=http%3A%2F%2Flocalhost%3A4200%2Fcallback&scope=openid%20profile%20email%20johodp.api&code_challenge=hKlNQr0lnnlMW5Yf3GdlpGJfl9SnY3CW_ktowi3c7zA&code_challenge_method=S256&state=5a223df34572489a89678a698307af5e&nonce=a1f6d229e9ee4aada5652fa77a853f46
```

- Notes: replace `code_challenge`, `state` and `nonce` with values generated for your test (or use the sample generator in `COMPLETION_SUMMARY.md`). If you start the flow from a different origin (e.g. `http://localhost:4200`), ensure your SPA sends login requests with credentials and that the server CORS policy allows credentials.

See the `COMPLETION_SUMMARY.md` for a longer changelog and testing tips (PKCE code_challenge/code_verifier flow, cookie SameSite notes).

### Testing login and cookies (SPA)

When testing a browser-based SPA that initiates PKCE flows from `http://localhost:4200`, the SPA must send its login requests with credentials and the server must allow the SPA origin and credentials (CORS). The project includes a development CORS policy `AllowSpa` which permits `http://localhost:4200` and allows credentials.

Examples (PowerShell) — register/login and inspect `Set-Cookie` header:

```powershell
# Register or login (the API creates the user if it doesn't exist in this project)
$body = '{"email":"spa.test+1@example.com","password":"P@ssw0rd!"}'
$resp = Invoke-WebRequest -Uri 'http://localhost:5000/api/auth/login' -Method POST -Body $body -ContentType 'application/json' -UseBasicParsing -ErrorAction Stop
$resp.StatusCode
$resp.Headers['Set-Cookie']
```

Examples (curl) — show response headers including `Set-Cookie`:

```bash
curl -i -X POST http://localhost:5000/api/auth/login \
   -H "Content-Type: application/json" \
   -d '{"email":"spa.test+1@example.com","password":"P@ssw0rd!"}'
```

When running the SPA from `http://localhost:4200`, make sure your fetch/XHR includes credentials so the browser stores the cookie produced by the API:

```js
fetch('http://localhost:5000/api/auth/login', {
   method: 'POST',
   credentials: 'include',
   headers: { 'Content-Type': 'application/json' },
   body: JSON.stringify({ email: 'spa.test+1@example.com', password: 'P@ssw0rd!' })
});
```

After a successful login you can visit `http://localhost:5000/account/claims` (or fetch it from the SPA with `credentials: 'include'`) to confirm the server sees the authentication cookie and the expected claims.

### Account Management Pages

- **Login** — `/account/login` - Sign in with email and password.
- **Register** — `/account/register` - Create a new account with email, password, first name, and last name.
- **Forgot Password** — `/account/forgot-password` - Request a password reset link (token printed to console in dev mode).
- **Reset Password** — `/account/reset-password?token={token}` - Set a new password with a valid reset token.
- **Logout** — `/account/logout` - Sign out and clear session.

### Example Usage

Quick examples (C# interactive or controller) — create a user with password and sign-in:

```csharp
// register
var user = Johodp.Domain.Users.Aggregates.User.Create("user@example.com", "First", "Last");
var result = await userManager.CreateAsync(user, "P@ssw0rd!");

// sign-in
var signIn = await signInManager.PasswordSignInAsync("user@example.com", "P@ssw0rd!", isPersistent: false, lockoutOnFailure: false);
if (signIn.Succeeded) { /* proceed */ }
else if (signIn.RequiresTwoFactor) { /* start 2FA flow */ }
```

Notes:
- Password hashing is provided by the `IPasswordHasher<TUser>` registered by Identity.
- The login UI is available at `/account/login` and posts to the local sign-in flow.
- Password reset tokens are generated and logged to console in development (ready for integration with email services in production).

### Récupérer un utilisateur

```bash
GET /api/users/{userId}
```

## Structure du projet

```
src/
├── Johodp.Domain/           # Logique métier
│   ├── Common/              # Classes de base DDD
│   ├── Users/               # Agrégat Users
│   │   ├── Aggregates/
│   │   ├── ValueObjects/
│   │   └── Events/
│   └── Clients/             # Agrégat Clients
│       ├── Aggregates/
│       ├── ValueObjects/
│       └── Events/
├── Johodp.Application/      # Cas d'utilisation
│   ├── Common/Interfaces/   # Interfaces de dépôt
│   ├── Users/
│   │   ├── Commands/
│   │   ├── Queries/
│   │   └── DTOs/
│   └── Clients/
│       ├── Commands/
│       ├── Queries/
│       └── DTOs/
├── Johodp.Infrastructure/   # Implémentation technique
│   ├── Persistence/
│   │   ├── DbContext/
│   │   ├── Repositories/
│   │   └── Configurations/
│   ├── IdentityServer/
│   └── Services/
└── Johodp.Api/             # API Web
    ├── Controllers/
    ├── Extensions/
    └── Program.cs
```

## Concepts clés

### Agrégats
- **User** : Représente un utilisateur du système
- **Client** : Représente une application cliente IdentityServer

### Value Objects
- **UserId** : Identifiant unique d'un utilisateur
- **Email** : Adresse e-mail avec validation
- **ClientId** : Identifiant unique d'un client
- **ClientSecret** : Secret partagé du client

### Events de domaine
- **UserRegisteredEvent** : Déclenché lors de l'enregistrement d'un utilisateur
- **UserEmailConfirmedEvent** : Déclenché lors de la confirmation d'e-mail
- **ClientCreatedEvent** : Déclenché lors de la création d'un client

## Prochaines étapes

- [ ] Implémenter IdentityServer4
- [ ] Ajouter l'authentification OAuth2/OIDC
- [ ] Ajouter les migrations Entity Framework
- [ ] Implémenter les tests unitaires
- [ ] Implémenter les tests d'intégration
- [ ] Configurer CI/CD

## Ressources

- [Domain-Driven Design](https://domainlanguage.com/ddd/)
- [IdentityServer4 Documentation](https://docs.identityserver.io/)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
