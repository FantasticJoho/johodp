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

### Endpoints Web (HTML)
- GET/POST `/Account/Login` - Formulaire de connexion
- GET/POST `/Account/Register` - Formulaire d'inscription
- POST `/Account/Logout` - Déconnexion
- GET/POST `/Account/ForgotPassword` - Réinitialisation mot de passe
- GET/POST `/Account/ResetPassword` - Nouveau mot de passe
- GET/POST `/Account/Onboarding` - Demande d'onboarding
- GET/POST `/Account/Activate` - Activation compte

### Endpoints API (JSON)

**Authentication**
```bash
# Enregistrement
POST /api/auth/register
{
  "email": "user@example.com",
  "firstName": "Jean",
  "lastName": "Dupont",
  "password": "SecureP@ssw0rd123!",
  "confirmPassword": "SecureP@ssw0rd123!"
}

# Login
POST /api/auth/login
{
  "email": "user@example.com",
  "password": "SecureP@ssw0rd123!"
}

# Logout
POST /api/auth/logout

# Mot de passe oublié
POST /api/auth/forgot-password
{
  "email": "user@example.com"
}

# Réinitialiser mot de passe
POST /api/auth/reset-password
{
  "email": "user@example.com",
  "token": "CfDJ8N...",
  "password": "NewP@ssw0rd123!",
  "confirmPassword": "NewP@ssw0rd123!"
}
```

**Account Management**
```bash
# Activation compte
POST /api/account/activate
{
  "token": "CfDJ8N...",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "tenantId": "acme",
  "newPassword": "SecureP@ssw0rd123!",
  "confirmPassword": "SecureP@ssw0rd123!"
}

# Onboarding
POST /api/account/onboarding
{
  "tenantId": "acme",
  "email": "user@example.com",
  "firstName": "Jean",
  "lastName": "Dupont"
}
```

**Users Management**
```bash
# Créer utilisateur (appelé par app tierce après approbation)
POST /api/users/register
{
  "email": "user@example.com",
  "firstName": "Jean",
  "lastName": "Dupont",
  "password": "TempP@ssw0rd123!",
  "tenantId": "acme"
}

# Récupérer un utilisateur
GET /api/users/{userId}

# Récupérer les tenants d'un utilisateur
GET /api/users/{userId}/tenants
```

## 🌐 Configuration CORS

### ⚠️ IMPORTANT: Limites de CORS

**CORS protège UNIQUEMENT les navigateurs web !**

```
✅ CORS protège:
   - Navigateurs (Chrome, Firefox, Safari, Edge)
   - JavaScript (fetch, axios, XMLHttpRequest)
   - Applications SPA (React, Angular, Vue)

❌ CORS NE protège PAS:
   - curl / wget / Postman / Insomnia
   - Applications serveur (Node.js, Python, C#)
   - Applications mobile natives (iOS, Android)
   - Scripts backend / API-to-API calls
```

### Architecture CORS

- **AllowedCorsOrigins** géré au niveau **Tenant** (pas Client)
- Un Client hérite des CORS de tous ses tenants associés
- IdentityServer agrège dynamiquement les origines autorisées

**Exemple:**
```json
// Tenant "acme"
{
  "allowedCorsOrigins": [
    "http://localhost:4200",
    "https://app.acme.com"
  ]
}

// Client "acme-spa" (associé au tenant "acme")
// Hérite automatiquement: ["http://localhost:4200", "https://app.acme.com"]
```

### Contournement CORS

```bash
# ❌ Bloqué dans un navigateur
fetch('https://api.johodp.com/api/auth/login', { method: 'POST' })
// ERROR: CORS policy blocked

# ✅ Fonctionne avec curl (pas de CORS)
curl -X POST https://api.johodp.com/api/auth/login
# SUCCESS: Retourne la réponse
```

### Vraie Sécurité

**CORS = Commodité UX, PAS Sécurité !**

Protection réelle:
1. **Authentication** - OAuth2/OIDC tokens requis
2. **Authorization** - Claims-based policies
3. **Rate Limiting** - Limite tentatives abusives
4. **API Keys** - Identification client (optionnel)
5. **IP Whitelist** - Restriction géographique (optionnel)

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
