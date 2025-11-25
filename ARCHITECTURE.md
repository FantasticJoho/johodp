# 🏗️ Architecture et Flux de traitement

## Vue d'ensemble du flux utilisateur

```
┌─────────────────────────────────────────────────────────────────┐
│                      CLIENT (Browser/Mobile)                     │
└────────────────────────────┬────────────────────────────────────┘
                             │
                    POST /api/users/register
                             │
┌────────────────────────────▼────────────────────────────────────┐
│                      API LAYER (Johodp.Api)                      │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  UsersController.Register()                              │   │
│  │  - Reçoit le RegisterUserCommand                         │   │
│  │  - Envoie au MediatR Pipeline                            │   │
│  └──────────────────────────┬───────────────────────────────┘   │
└─────────────────────────────┼───────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────────┐
│              APPLICATION LAYER (Johodp.Application)              │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Mini-MediatR Pipeline (~50 lignes)                     │   │
│  │  1. RegisterUserCommandValidator (FluentValidation)     │   │
│  │  2. RegisterUserCommandHandler (IRequestHandler)        │   │
│  │     - Vérifier si email existe (Repository)             │   │
│  │     - Créer l'agrégat User (Status = PendingActivation) │   │
│  │     - Ajouter au repository                             │   │
│  │     - Sauvegarder (UnitOfWork)                          │   │
│  │     - Publier les domain events (Channel-based)        │   │
│  │     - Retourner la réponse                              │   │
│  └──────────────────────────┬───────────────────────────────┘   │
└─────────────────────────────┼───────────────────────────────────┘
                              │
┌─────────────────────────────▼───────────────────────────────────┐
│             DOMAIN LAYER (Johodp.Domain)                         │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  User Aggregate                                          │   │
│  │  - Email (Value Object avec validation)                 │   │
│  │  - UserId (Value Object typé)                           │   │
│  │  - FirstName, LastName                                  │   │
│  │  - Status (Enumeration class, non enum C#)             │   │
│  │  - IsActive (propriété calculée = Status == Active)     │   │
│  │  - Déclenche UserPendingActivationEvent                │   │
│  └──────────────────────────┬───────────────────────────────┘   │
└─────────────────────────────┼───────────────────────────────────┘
                              │
┌─────────────────────────────▼───────────────────────────────────┐
│          INFRASTRUCTURE LAYER (Johodp.Infrastructure)            │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  UnitOfWork.SaveChangesAsync()                           │   │
│  │  - Mapper l'agrégat à l'entité EF Core                   │   │
│  │  - Conversion Status: Enumeration → int (Value)         │   │
│  │  - Insérer dans JohodpDbContext                          │   │
│  │  - Sauvegarder les changements à la DB                  │   │
│  └──────────────────────────┬───────────────────────────────┘   │
│                             │                                    │
│  ┌──────────────────────────▼───────────────────────────────┐   │
│  │  DomainEventPublisher.PublishAsync()                     │   │
│  │  - Publier événements via Channel (BoundedChannel)      │   │
│  │  - DomainEventProcessor (BackgroundService)             │   │
│  │  - Déclencher les handlers d'événements                  │   │
│  └──────────────────────────┬───────────────────────────────┘   │
└─────────────────────────────┼───────────────────────────────────┘
                              │
┌─────────────────────────────▼───────────────────────────────────┐
│                    DATABASE (PostgreSQL)                         │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Table: users                                            │   │
│  │  - id (UUID)                                             │   │
│  │  - email (VARCHAR)                                       │   │
│  │  - first_name (VARCHAR)                                  │   │
│  │  - last_name (VARCHAR)                                   │   │
│  │  - email_confirmed (BOOLEAN)                             │   │
│  │  - status (INTEGER) - 0: PendingActivation, 1: Active   │   │
│  │  - password_hash (VARCHAR, nullable)                    │   │
│  │  - activated_at (TIMESTAMP, nullable)                   │   │
│  │  - created_at (TIMESTAMP)                                │   │
│  │  - updated_at (TIMESTAMP)                                │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

## Flux de récupération d'utilisateur

```
GET /api/users/{userId}
         │
         ▼
UsersController.GetUser(userId)
         │
         ▼
GetUserByIdQuery → GetUserByIdQueryHandler
         │
         ▼
IUnitOfWork.Users.GetByIdAsync(UserId)
         │
         ▼
UserRepository.GetByIdAsync(UserId)
         │
         ▼
DbContext.Users.FirstOrDefaultAsync()
         │
         ▼
SELECT * FROM users WHERE id = @id
         │
         ▼
Mapper entité EF Core → UserDto
         │
         ▼
Retourner UserDto au client
```

## Intégration IdentityServer

```
┌──────────────────────────────────────────────────────┐
│         OAuth2/OIDC Authorization Flow               │
└─────────────────────────┬──────────────────────────┬─┘
                          │                          │
            ┌─────────────▼──────────────┐         │
            │   Authorization Server    │         │
            │   (IdentityServer4)        │         │
            │  - Endpoint /authorize     │         │
            │  - Endpoint /token         │         │
            │  - Endpoint /userinfo      │         │
            └─────────────┬──────────────┘         │
                          │                         │
     ┌────────────────────▼──────────────┐        │
     │    Resource Owner (User)           │        │
     │  - Credentials validation          │        │
     │  - Consent screen                  │        │
     │  - Token generation                │        │
     └────────────────────┬──────────────┘        │
                          │                        │
     ┌────────────────────▼──────────────┐        │
     │      Client Application           │        │
     │  - Receives access_token          │        │
     │  - Receives id_token (OIDC)       │        │
     │  - Calls API with token           │        │
     └────────────────────┬──────────────┘        │
                          │                        │
            ┌─────────────▼──────────────┐        │
            │   Johodp API               │        │
            │  - Validates JWT Token     │        │
            │  - Autorize l'accès        │        │
            │  - Retourne les resources  │        │
            └────────────────────────────┘        │
                                                   │
     ┌──────────────────────────────────────────┘
     │
     └─► User connected and authenticated
```

## Pattern CQRS - Commandes vs Requêtes

### Commandes (Write Operations)
```
RegisterUserCommand
    ↓
RegisterUserCommandValidator (FluentValidation)
    ↓
RegisterUserCommandHandler
    ├─ Chercher l'utilisateur existant
    ├─ Créer l'agrégat User
    ├─ Ajouter au repository
    ├─ Sauvegarder via UnitOfWork
    ├─ Publier les domain events
    └─ Retourner RegisterUserResponse
```

### Requêtes (Read Operations)
```
GetUserByIdQuery
    ↓
GetUserByIdQueryHandler
    ├─ Récupérer l'utilisateur
    ├─ Mapper en UserDto
    └─ Retourner UserDto
```

## Transactions et Consistency

```
┌─────────────────────────────────────────────────┐
│  Opération utilisateur atomic                    │
├─────────────────────────────────────────────────┤
│  1. BEGIN TRANSACTION                            │
│  2. INSERT INTO users ...                        │
│  3. PUBLISH UserRegisteredEvent                  │
│  4. COMMIT                                       │
│  5. Si erreur → ROLLBACK                         │
└─────────────────────────────────────────────────┘
```

## Gestion CORS et Sécurité

### Architecture CORS

```
┌─────────────────────────────────────────────────────────┐
│                    Client Aggregate                      │
│  - ClientId, ClientName                                  │
│  - AllowedScopes                                        │
│  - RequireConsent                                       │
│  ❌ AllowedCorsOrigins (DÉPLACÉ vers Tenant)           │
└────────────────────────┬────────────────────────────────┘
                         │
                         │ 1:N (via associatedTenantIds)
                         │
┌────────────────────────▼────────────────────────────────┐
│                    Tenant Aggregate                      │
│  - TenantId, DisplayName                                │
│  - AllowedReturnUrls (Redirect URIs)                    │
│  ✅ AllowedCorsOrigins (Liste des origines CORS)       │
│  - Branding (colors, logo, CSS)                         │
│  - Localization (languages, timezone, currency)         │
└────────────────────────┬────────────────────────────────┘
                         │
                         │ Agrégation dynamique
                         │
┌────────────────────────▼────────────────────────────────┐
│              CustomClientStore (IdentityServer)         │
│  MapToIdentityServerClient():                           │
│  - AllowedRedirectUris = tenants.SelectMany(           │
│      t => t.AllowedReturnUrls).Distinct()              │
│  - AllowedCorsOrigins = tenants.SelectMany(            │
│      t => t.AllowedCorsOrigins).Distinct()             │
└─────────────────────────────────────────────────────────┘
```

### Pourquoi CORS est géré au niveau Tenant ?

✅ **Cohérence** - AllowedReturnUrls et AllowedCorsOrigins au même endroit
✅ **Multi-tenant** - Chaque tenant peut avoir ses propres origines CORS
✅ **Flexibilité** - Un client peut hériter des CORS de plusieurs tenants
✅ **Maintenabilité** - Configuration centralisée par tenant

### ⚠️ IMPORTANT: Limites de sécurité CORS

**CORS ne protège QUE les navigateurs web !**

```
┌──────────────────────────────────────────────────────┐
│         CORS = Protection NAVIGATEUR uniquement       │
├──────────────────────────────────────────────────────┤
│                                                       │
│  ✅ Protège:                                          │
│     - Requêtes depuis un navigateur web               │
│     - JavaScript (fetch, XMLHttpRequest, axios)       │
│     - Applications SPA (React, Angular, Vue)          │
│                                                       │
│  ❌ NE protège PAS:                                   │
│     - Requêtes curl / wget / Postman                  │
│     - Applications serveur (Node.js, C#, Python)      │
│     - Applications mobile natives (iOS, Android)      │
│     - Scripts backend / API-to-API calls              │
│                                                       │
└──────────────────────────────────────────────────────┘
```

### Exemple de contournement CORS

```bash
# ❌ BLOQUÉ dans un navigateur (Origin: http://evil.com)
fetch('https://api.johodp.com/connect/token', {
  method: 'POST',
  body: 'grant_type=client_credentials&client_id=xyz'
})
// ERROR: CORS policy: No 'Access-Control-Allow-Origin' header

# ✅ FONCTIONNE avec curl (pas de vérification CORS)
curl -X POST https://api.johodp.com/connect/token \
  -d "grant_type=client_credentials&client_id=xyz"
# SUCCESS: Retourne les tokens sans vérification d'origine
```

### Sécurité réelle : Authentication + Authorization

**CORS est une COMMODITÉ, pas une SÉCURITÉ**

```
┌─────────────────────────────────────────────────────────┐
│              Protection en couches (Defense in Depth)    │
├─────────────────────────────────────────────────────────┤
│ 1. CORS (navigateur)       → Commodité UX               │
│ 2. Authentication (OAuth2) → Qui êtes-vous ?            │
│ 3. Authorization (Claims)  → Que pouvez-vous faire ?    │
│ 4. Rate Limiting           → Limite abus                │
│ 5. API Keys / Client Auth  → Identification client      │
│ 6. IP Whitelist (optionnel)→ Restriction géographique  │
└─────────────────────────────────────────────────────────┘
```

### Configuration CORS dans Johodp

```csharp
// Infrastructure/IdentityServer/CustomClientStore.cs
public Duende.IdentityServer.Models.Client MapToIdentityServerClient(
    Client client, 
    IEnumerable<Tenant> tenants)
{
    // Agrégation dynamique des CORS origins depuis les tenants
    var corsOrigins = tenants
        .SelectMany(t => t.AllowedCorsOrigins)
        .Distinct()
        .ToList();

    return new Duende.IdentityServer.Models.Client
    {
        ClientId = client.ClientName.Value,
        AllowedCorsOrigins = corsOrigins,  // ⚠️ Protège UNIQUEMENT les navigateurs
        // ... autres propriétés
    };
}
```

### Validation des CORS Origins

```csharp
// Domain/Tenants/Aggregates/Tenant.cs
public void AddAllowedCorsOrigin(string origin)
{
    if (string.IsNullOrWhiteSpace(origin))
        throw new ArgumentException("CORS origin cannot be empty");

    // Validation: Autorité uniquement (pas de path)
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        throw new ArgumentException($"Invalid CORS origin format: {origin}");

    if (!string.IsNullOrEmpty(uri.PathAndQuery) && uri.PathAndQuery != "/")
        throw new ArgumentException(
            $"CORS origin must be authority only (no path): {origin}");

    // Format normalisé: https://example.com (pas de trailing slash)
    var normalizedOrigin = $"{uri.Scheme}://{uri.Authority}";
    
    if (!_allowedCorsOrigins.Contains(normalizedOrigin))
        _allowedCorsOrigins.Add(normalizedOrigin);
}
```

### Migration CORS (Client → Tenant)

**Base de données**
```sql
-- Migration: MoveCorsOriginsFromClientToTenant

-- Étape 1: Ajouter colonne nullable
ALTER TABLE tenants 
ADD COLUMN "AllowedCorsOrigins" jsonb NULL;

-- Étape 2: Définir valeur par défaut
UPDATE tenants 
SET "AllowedCorsOrigins" = '[]'::jsonb 
WHERE "AllowedCorsOrigins" IS NULL;

-- Étape 3: Rendre NOT NULL
ALTER TABLE tenants 
ALTER COLUMN "AllowedCorsOrigins" SET NOT NULL;

-- Étape 4: Supprimer ancienne colonne
ALTER TABLE clients 
DROP COLUMN IF EXISTS "AllowedCorsOrigins";
```

**Code impacté**
- ✅ `Domain/Clients/Aggregates/Client.cs` - AllowedCorsOrigins supprimé
- ✅ `Domain/Tenants/Aggregates/Tenant.cs` - AllowedCorsOrigins ajouté
- ✅ `Application/Clients/DTOs/*` - AllowedCorsOrigins supprimé
- ✅ `Application/Tenants/DTOs/*` - AllowedCorsOrigins ajouté
- ✅ `Application/Tenants/Commands/*` - Gestion AllowedCorsOrigins
- ✅ `Infrastructure/IdentityServer/CustomClientStore.cs` - Agrégation depuis tenants
- ✅ `Infrastructure/Persistence/Configurations/*` - Mapping jsonb

## Webhook de Vérification Utilisateur (Onboarding)

Le webhook d'onboarding permet la **validation métier externe** avant la création effective de l'utilisateur (UC-04 / US-4.2). Il complète le flux de formulaire côté Johodp en déclenchant un appel sortant vers l'application tierce.

### Rôle dans le flux
1. L'utilisateur soumet le formulaire `/account/onboarding` (Johodp).
2. Johodp crée une demande interne et envoie un `POST` vers `userVerificationEndpoint` configuré dans le tenant.
3. L'application tierce valide (contrats, email, contraintes métiers).
4. Si acceptée, elle appelle l'API Johodp `/api/users/register` avec un access token `johodp.admin` pour créer l'utilisateur en `PendingActivation`.
5. Johodp envoie l'email d'activation → flux d'activation standard.

### Sécurité
- Signature HMAC envoyée dans `X-Johodp-Signature` + horodatage `X-Johodp-Timestamp` (voir détails dans `API_ENDPOINTS.md`).
- Timeout de validation: 5 minutes (sinon message d'attente ou réessai manuel).
- Endpoint recommandé HTTPS obligatoire en production.
- Idempotence requise: plusieurs envois possibles en cas de retry réseau.

### Différences vs Appels API
- Direction: webhook = sortie Johodp → application tierce; API = entrée vers Johodp.
- Authentification: webhook par secret partagé (HMAC) vs API par OAuth2 (Bearer access token).
- Synchronicité: webhook déclenché par événement interne; appel API initié par le client.

### Extension / Évolutions futures
- File d'attente persistée pour retries (ex: table `webhook_outbox`).
- Traçabilité: corrélation par `requestId` dans logs.
- Signature enrichie (payload canonique + version de schéma).

### Références
- Spécification payload & headers: `API_ENDPOINTS.md` (section Webhook)
- User Stories: `USER_STORIES.md` (US-4.2 Onboarding, US-3.1 création via API, US-5.2 login tenant)
- Use Cases: UC-04 (Onboarding), UC-05 (Activation)

## Avantages de cette architecture

✅ **Séparation des préoccupations** - Chaque couche a une responsabilité unique
✅ **Testabilité** - Facile de tester chaque couche indépendamment
✅ **Maintenabilité** - Code organisé et facile à maintenir
✅ **Évolutivité** - Facile d'ajouter de nouvelles fonctionnalités
✅ **Domain-Driven** - La logique métier est au cœur de l'application
✅ **Event Sourcing ready** - Les domain events peuvent être persisted
✅ **CQRS friendly** - Séparation naturelle read/write
✅ **Multi-tenant CORS** - Gestion CORS flexible par tenant

## Pattern DDD Enumeration

### Pourquoi Enumeration class plutôt que enum C# ?

Les `enum` C# ont des limitations importantes en Domain-Driven Design :

❌ **Valeurs par défaut problématiques** - `default(UserStatus)` = 0, peut causer des bugs
❌ **Pas de comportement** - Impossible d'ajouter de la logique métier
❌ **Primitive obsession** - Les enums sont essentiellement des int
❌ **Pas extensible** - Impossible d'ajouter des méthodes ou propriétés

### Solution : Enumeration base class

Notre implémentation suit le pattern de **Jimmy Bogard** :

```csharp
// Domain/Common/Enumeration.cs
public abstract class Enumeration : IComparable
{
    public int Value { get; private set; }
    public string Name { get; private set; }

    protected Enumeration(int value, string name)
    {
        Value = value;
        Name = name;
    }

    public static IEnumerable<T> GetAll<T>() where T : Enumeration
        => typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                     .Select(f => f.GetValue(null))
                     .Cast<T>();

    public static T FromValue<T>(int value) where T : Enumeration
        => GetAll<T>().FirstOrDefault(e => e.Value == value) 
           ?? throw new InvalidOperationException($"'{value}' n'est pas valide pour {typeof(T)}");

    public static T FromName<T>(string name) where T : Enumeration
        => GetAll<T>().FirstOrDefault(e => e.Name == name) 
           ?? throw new InvalidOperationException($"'{name}' n'est pas valide pour {typeof(T)}");

    public static bool operator ==(Enumeration left, Enumeration right)
        => left?.Value == right?.Value;

    public static bool operator !=(Enumeration left, Enumeration right)
        => !(left == right);

    public override bool Equals(object obj)
        => obj is Enumeration other && Value.Equals(other.Value);

    public override int GetHashCode() => Value.GetHashCode();

    public int CompareTo(object obj)
        => obj is Enumeration other ? Value.CompareTo(other.Value) : 1;
}
```

### Exemple : UserStatus Enumeration

```csharp
// Domain/Users/Aggregates/User.cs
public class UserStatus : Enumeration
{
    // Instances statiques - type-safe, pas de valeur par défaut
    public static readonly UserStatus PendingActivation = new(0, nameof(PendingActivation));
    public static readonly UserStatus Active = new(1, nameof(Active));
    public static readonly UserStatus Suspended = new(2, nameof(Suspended));
    public static readonly UserStatus Deleted = new(3, nameof(Deleted));

    // Constructeur privé - seules les instances statiques peuvent exister
    private UserStatus(int value, string name) : base(value, name) { }

    // Méthodes comportementales - logique métier dans le domaine
    public bool CanActivate() => this == PendingActivation;
    public bool CanLogin() => this == Active;
    public bool CanSuspend() => this == Active;
    public bool IsDeleted() => this == Deleted;
}
```

### Utilisation dans l'agrégat User

```csharp
public class User : AggregateRoot<UserId>
{
    // Propriété avec valeur par défaut explicite
    public UserStatus Status { get; private set; } = UserStatus.PendingActivation;
    
    // Propriété calculée - pas de colonne en base
    public bool IsActive => Status == UserStatus.Active;

    public void Activate()
    {
        if (!Status.CanActivate())
            throw new InvalidOperationException("L'utilisateur ne peut pas être activé");
        
        Status = UserStatus.Active;
        ActivatedAt = DateTime.UtcNow;
        AddDomainEvent(new UserActivatedEvent(Id));
    }

    public void Suspend()
    {
        if (!Status.CanSuspend())
            throw new InvalidOperationException("L'utilisateur ne peut pas être suspendu");
        
        Status = UserStatus.Suspended;
        AddDomainEvent(new UserSuspendedEvent(Id));
    }
}
```

### Configuration EF Core

```csharp
// Infrastructure/Persistence/Configurations/UserConfiguration.cs
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Conversion Enumeration ↔ int pour la base de données
        builder.Property(x => x.Status)
               .HasConversion(
                   v => v.Value,  // Enumeration → int (sauvegarde)
                   v => UserStatus.FromValue<UserStatus>(v))  // int → Enumeration (lecture)
               .IsRequired();

        // Ignorer les propriétés calculées
        builder.Ignore(x => x.IsActive);
    }
}
```

### Avantages obtenus

✅ **Type-safe** - Impossible d'utiliser des valeurs invalides
✅ **Pas de valeur par défaut** - `Status = UserStatus.PendingActivation` est explicite
✅ **Comportement riche** - `Status.CanActivate()`, `Status.CanLogin()`
✅ **Logique métier dans le domaine** - Pas dans les controllers ou services
✅ **Extensibilité** - Facile d'ajouter nouvelles méthodes ou propriétés
✅ **Lisibilité** - `if (user.Status.CanLogin())` vs `if (user.Status == 1)`
✅ **Refactoring-friendly** - Changements de valeurs sans casser le code
✅ **Compatible EF Core** - Stockage en int, conversion transparente

### Comparaison enum vs Enumeration

| Caractéristique | enum C# | Enumeration class |
|----------------|---------|-------------------|
| Type-safe | ✅ | ✅ |
| Valeur par défaut | ❌ `default = 0` | ✅ Explicite |
| Comportement métier | ❌ Impossible | ✅ Méthodes |
| Extensibilité | ❌ Limité | ✅ Illimité |
| Lisibilité | ⚠️ Moyen | ✅ Excellent |
| Performance | ✅ Rapide | ⚠️ Légèrement plus lent |
| Storage DB | ✅ int | ✅ int (HasConversion) |

