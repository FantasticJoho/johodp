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

```bash
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
