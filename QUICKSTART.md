# Guide de démarrage rapide - Johodp IDP

## 🚀 Démarrage en 5 minutes

### 1. Démarrer PostgreSQL avec Docker

```powershell
docker-compose up -d
```

### 2. Restaurer les packages NuGet

```powershell
dotnet restore
```

### 3. Appliquer les migrations (Windows)

```powershell
.\init-db.ps1
```

Ou sur Linux/Mac :
```bash
./init-db.sh
```

### 4. Lancer l'API

```powershell
dotnet run --project src/Johodp.Api
```

L'application démarrera sur `https://localhost:5001`

## 📋 Architecture DDD

```
┌─────────────────────────────────────────────────────────┐
│                    Johodp.Api (Présentation)            │
│  • Controllers (REST API)                               │
│  • Program.cs (Configuration)                           │
│  • Extensions (Injection de dépendances)                │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│              Johodp.Application (Cas d'usage)           │
│  • Commands & CommandHandlers (CQRS)                    │
│  • Queries & QueryHandlers                              │
│  • DTOs                                                 │
│  • Interfaces de dépôt                                  │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│           Johodp.Infrastructure (Implémentation)        │
│  • Entity Framework Core                                │
│  • Repositories                                         │
│  • Unit of Work                                         │
│  • IdentityServer Configuration                         │
│  • Domain Event Publisher                               │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│              Johodp.Domain (Logique métier)             │
│  • Agrégats (User, Client)                              │
│  • Value Objects (Email, UserId, etc.)                  │
│  • Domain Events                                        │
│  • Classes de base (AggregateRoot, ValueObject)         │
└─────────────────────────────────────────────────────────┘
```

## 🔑 Concepts clés DDD implémentés

### Agrégats
- **User** : Gère l'enregistrement, la confirmation d'email, la désactivation
- **Client** : Gère les applications OAuth2/OIDC

### Value Objects
- **Email** : Validation d'email intégrée
- **UserId** : Identifiant utilisateur typé
- **ClientId** : Identifiant client typé
- **ClientSecret** : Secret client typé

### Domain Events
- **UserRegisteredEvent** : Publié lors de la création d'un utilisateur
- **UserEmailConfirmedEvent** : Publié lors de la confirmation d'email
- **ClientCreatedEvent** : Publié lors de la création d'un client

### Patterns CQRS
- Commands pour les opérations d'écriture (RegisterUserCommand)
- Queries pour les lectures (GetUserByIdQuery)
- Handlers séparant la logique

## 📚 Points d'entrée API

### Utilisateurs

**Enregistrer un utilisateur**
```bash
POST /api/users/register
{
  "email": "user@example.com",
  "firstName": "Jean",
  "lastName": "Dupont"
}
```

**Récupérer un utilisateur**
```bash
GET /api/users/{userId}
```

## 🧪 Tests

Les tests sont organisés avec xUnit :

```powershell
dotnet test tests/Johodp.Tests/
```

## 🔒 Sécurité & Prochaines étapes

- [ ] Intégrer IdentityServer4
- [ ] Implémenter OAuth2/OIDC
- [ ] Ajouter l'authentification JWT
- [ ] Configurer les policies d'autorisation
- [ ] Ajouter les migrations EF Core
- [ ] Implémenter plus de domain events
- [ ] Ajouter des tests d'intégration

## 📁 Structure détaillée

```
src/
├── Johodp.Domain/
│   ├── Common/
│   │   ├── AggregateRoot.cs
│   │   ├── DomainEvent.cs
│   │   └── ValueObject.cs
│   ├── Users/
│   │   ├── Aggregates/User.cs
│   │   ├── ValueObjects/Email.cs, UserId.cs
│   │   └── Events/UserRegisteredEvent.cs, UserEmailConfirmedEvent.cs
│   └── Clients/
│       ├── Aggregates/Client.cs
│       ├── ValueObjects/ClientId.cs, ClientSecret.cs
│       └── Events/ClientCreatedEvent.cs
│
├── Johodp.Application/
│   ├── Common/Interfaces/
│   │   ├── IUserRepository.cs
│   │   ├── IClientRepository.cs
│   │   ├── IUnitOfWork.cs
│   │   └── IDomainEventPublisher.cs
│   ├── Users/
│   │   ├── Commands/RegisterUserCommand.cs, RegisterUserCommandHandler.cs
│   │   ├── Queries/GetUserByIdQuery.cs, GetUserByIdQueryHandler.cs
│   │   └── DTOs/UserDto.cs
│   └── Clients/
│
├── Johodp.Infrastructure/
│   ├── Persistence/
│   │   ├── DbContext/JohodpDbContext.cs
│   │   ├── Repositories/UserRepository.cs, ClientRepository.cs
│   │   ├── Configurations/UserConfiguration.cs, ClientConfiguration.cs
│   │   └── UnitOfWork.cs
│   ├── IdentityServer/
│   ├── Services/DomainEventPublisher.cs
│   └── Migrations/
│
├── Johodp.Api/
│   ├── Controllers/UsersController.cs
│   ├── Extensions/ServiceCollectionExtensions.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
│
└── tests/
    └── Johodp.Tests/
        └── UserAggregateTests.cs
```

## 💡 Ressources

- [Domain-Driven Design Eric Evans](https://www.domainlanguage.com/ddd/)
- [Microsoft - CQRS Pattern](https://docs.microsoft.com/en-us/azure/architecture/patterns/cqrs)
- [IdentityServer4 Docs](https://docs.identityserver.io/)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
