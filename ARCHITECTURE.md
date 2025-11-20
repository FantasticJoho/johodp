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

## Avantages de cette architecture

✅ **Séparation des préoccupations** - Chaque couche a une responsabilité unique
✅ **Testabilité** - Facile de tester chaque couche indépendamment
✅ **Maintenabilité** - Code organisé et facile à maintenir
✅ **Évolutivité** - Facile d'ajouter de nouvelles fonctionnalités
✅ **Domain-Driven** - La logique métier est au cœur de l'application
✅ **Event Sourcing ready** - Les domain events peuvent être persisted
✅ **CQRS friendly** - Séparation naturelle read/write

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

