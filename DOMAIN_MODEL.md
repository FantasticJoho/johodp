# Modèle de Domaine Johodp

## Vue d'ensemble des relations

```mermaid
classDiagram
    %% ============================================================================
    %% AGGREGATES ROOTS
    %% ============================================================================
    
    class Client {
        <<Aggregate Root>>
        +ClientId Id
        +string ClientName
        +string[] AllowedScopes
        +bool RequireClientSecret
        +bool RequireConsent
        +bool RequireMfa
        +bool IsActive
        +List~string~ AssociatedTenantIds
        +Create()
        +AssociateTenant()
        +DissociateTenant()
        +EnableMfa()
        +DisableMfa()
    }
    
    class Tenant {
        <<Aggregate Root>>
        +TenantId Id
        +string Name
        +string DisplayName
        +bool IsActive
        +string? ClientId
        +List~string~ Urls
        +List~string~ AllowedReturnUrls
        +List~string~ AllowedCorsOrigins
        +Branding branding
        +Localization localization
        +Create()
        +SetClient()
        +AddUrl()
        +AddAllowedReturnUrl()
        +UpdateBranding()
    }
    
    class User {
        <<Aggregate Root>>
        +UserId Id
        +Email Email
        +string FirstName
        +string LastName
        +UserStatus Status
        +string? PasswordHash
        +List~UserTenant~ UserTenants
        +bool MFAEnabled
        +Create()
        +AddTenant()
        +RemoveTenant()
        +UpdateTenantRoleAndScope()
        +Activate()
        +Suspend()
        +EnableMFA()
        +DisableMFA()
    }
    
    %% ============================================================================
    %% VALUE OBJECTS
    %% ============================================================================
    
    class ClientId {
        <<Value Object>>
        +Guid Value
        +Create()
        +From(Guid)
    }
    
    class TenantId {
        <<Value Object>>
        +Guid Value
        +Create()
        +From(Guid)
    }
    
    class UserId {
        <<Value Object>>
        +Guid Value
        +Create()
        +From(Guid)
    }
    
    class Email {
        <<Value Object>>
        +string Value
        +Create(string)
    }
    
    class UserTenant {
        <<Value Object>>
        +UserId UserId
        +TenantId TenantId
        +string Role
        +string Scope
        +DateTime CreatedAt
        +DateTime? UpdatedAt
        +Create()
        +Update()
    }
    
    class RoleId {
        <<Value Object>>
        +Guid Value
        +Create()
        +From(Guid)
    }
    
    class UserStatus {
        <<Enumeration>>
        PendingActivation
        Active
        Suspended
        Deleted
    }
    
    %% ============================================================================
    %% RELATIONS
    %% ============================================================================
    
    %% Client - Tenant (1-to-many)
    Client "1" --> "0..*" Tenant : associe
    Tenant --> Client : ClientId
    
    %% User - UserTenant - Tenant (many-to-many via Value Object)
    User "1" *-- "0..*" UserTenant : contient
    UserTenant --> TenantId : référence
    UserTenant --> UserId : référence
    Tenant "1" <.. "0..*" UserTenant : validé contre
    
    %% User - Value Objects
    User *-- UserId : a
    User *-- Email : a
    User *-- UserStatus : a
    
    %% Client - Value Objects
    Client *-- ClientId : a
    
    %% Tenant - Value Objects
    Tenant *-- TenantId : a
    
    %% Notes explicatives
    note for UserTenant "Value Object: identité = (UserId, TenantId)
    Role et Scope sont des STRINGS libres
    fournis par l'application tierce,
    PAS des références à des aggregates"
    
    note for Client "Un client devient visible pour
    IdentityServer uniquement s'il a
    au moins 1 tenant associé avec
    des redirect URIs"
```

## Diagramme de séquence : Création d'utilisateur multi-tenant

```mermaid
sequenceDiagram
    participant App as Application Tierce
    participant API as Johodp API
    participant Handler as RegisterUserHandler
    participant User as User Aggregate
    participant Repo as UserRepository
    participant Events as Event Bus
    
    App->>API: POST /api/users/register
    Note over App,API: { tenants: [{tenantId, role, scope}, ...] }
    
    API->>Handler: RegisterUserCommand
    Handler->>User: User.Create(email, firstName, lastName)
    Note over User: Status = PendingActivation
    User-->>Handler: user instance
    
    loop Pour chaque tenant
        Handler->>User: user.AddTenant(tenantId, role, scope)
        User->>User: Créer UserTenant Value Object
        User->>User: Ajouter à collection _userTenants
        Note over User: Validation: pas de doublon
    end
    
    Handler->>Repo: SaveAsync(user)
    Repo-->>Handler: Success
    
    Handler->>Events: PublishDomainEvents()
    Events->>Events: UserPendingActivationEvent
    Events-->>Handler: Published
    
    Handler-->>API: userId
    API-->>App: 201 Created
    
    Note over Events: Async: SendActivationEmailHandler
```

## Diagramme de séquence : Connexion avec tenant spécifique

```mermaid
sequenceDiagram
    participant SPA as Application SPA
    participant IS as IdentityServer
    participant Store as CustomClientStore
    participant Login as Login Controller
    participant User as User Aggregate
    participant Profile as ProfileService
    
    SPA->>IS: /connect/authorize?acr_values=tenant:acme-corp
    IS->>Store: FindClientByIdAsync(clientName)
    Store->>Store: Charger Client + Tenants associés
    Store->>Store: Agréger RedirectURIs et CORS
    Store-->>IS: Client avec URLs agrégées
    
    IS->>Login: Redirect to /api/auth/login
    Login->>User: Vérifier credentials
    Login->>User: user.BelongsToTenant(tenantId)?
    User->>User: Chercher dans _userTenants
    User-->>Login: true
    
    Login->>Login: SignInWithClaimsAsync()
    Note over Login: Ajoute claims: tenant_id, tenant_name
    Login-->>IS: Session créée
    
    IS->>Profile: GetProfileDataAsync()
    Profile->>Profile: Extraire tenant_id des claims
    Profile->>User: user.GetTenantContext(tenantId)
    User-->>Profile: UserTenant Value Object
    
    Profile->>Profile: Générer claims JWT
    Note over Profile: tenant_id, tenant_role, tenant_scope
    Profile-->>IS: Claims contextuels
    
    IS-->>SPA: Authorization code
    SPA->>IS: /connect/token (échange code)
    IS-->>SPA: JWT avec claims du tenant
```

## Relations clés

### 1. Client ↔ Tenant (1-to-many)

```mermaid
graph LR
    A[Client: my-app] -->|ClientId| B[Tenant: acme-corp]
    A -->|ClientId| C[Tenant: globex-inc]
    A -->|ClientId| D[Tenant: initech]
    
    B -->|AllowedReturnUrls| E[https://acme.com/callback]
    C -->|AllowedReturnUrls| F[https://globex.com/callback]
    D -->|AllowedReturnUrls| G[https://initech.com/callback]
    
    style A fill:#e1f5ff
    style B fill:#fff4e1
    style C fill:#fff4e1
    style D fill:#fff4e1
```

**Règles** :
- Un Client peut avoir plusieurs Tenants
- Un Tenant appartient à UN SEUL Client
- Les redirect URIs sont gérées au niveau Tenant
- IdentityServer agrège dynamiquement les URIs de tous les tenants

### 2. User ↔ Tenant (many-to-many via UserTenant)

```mermaid
graph TD
    U[User: john@example.com] -->|UserTenant| T1[Tenant: acme-corp]
    U -->|UserTenant| T2[Tenant: globex-inc]
    U -->|UserTenant| T3[Tenant: initech]
    
    UT1[UserTenant<br/>Role: admin<br/>Scope: full_access]
    UT2[UserTenant<br/>Role: developer<br/>Scope: project_alpha]
    UT3[UserTenant<br/>Role: viewer<br/>Scope: read_only]
    
    U -.-> UT1
    U -.-> UT2
    U -.-> UT3
    
    UT1 -.-> T1
    UT2 -.-> T2
    UT3 -.-> T3
    
    style U fill:#e1ffe1
    style T1 fill:#fff4e1
    style T2 fill:#fff4e1
    style T3 fill:#fff4e1
    style UT1 fill:#ffe1e1
    style UT2 fill:#ffe1e1
    style UT3 fill:#ffe1e1
```

**Règles** :
- Un User peut appartenir à plusieurs Tenants
- Chaque association a un **Role** et un **Scope** distincts
- UserTenant est un **Value Object** (identité = combinaison UserId + TenantId)
- Role et Scope sont des **strings libres** (pas de validation stricte)

### 3. User : Rôles tenant uniquement

```mermaid
graph TB
    subgraph "User Aggregate"
        U[User]
        U -->|Collection| UT[UserTenants<br/>Value Object]
    end
    
    subgraph "Rôles Tenant (Strings)"
        UT -->|String| TR1["admin"]
        UT -->|String| TR2["developer"]
        UT -->|String| TR3["viewer"]
        TR1 -.->|Défini par| EXT1[Application Tierce]
        TR2 -.->|Défini par| EXT1
        TR3 -.->|Défini par| EXT1
    end
    
    style UT fill:#ffe1e1
    style EXT1 fill:#fff4e1
```

**Note importante** :
- **Rôles tenant** (`UserTenant.Role`) : Strings libres pour l'application tierce
- Johodp n'a PAS de rôles système internes
- L'application tierce décide de tous les rôles et leur signification

## Architecture persistance

```mermaid
erDiagram
    CLIENTS ||--o{ TENANTS : "1-to-many"
    USERS ||--o{ USER_TENANTS : "1-to-many"
    TENANTS ||--o{ USER_TENANTS : "1-to-many (logique)"
    
    CLIENTS {
        uuid id PK
        string client_name UK
        jsonb allowed_scopes
        bool require_mfa
        bool is_active
        timestamp created_at
    }
    
    TENANTS {
        uuid id PK
        string name UK
        string display_name
        string client_id FK
        jsonb urls
        jsonb allowed_return_urls
        jsonb allowed_cors_origins
        jsonb branding
        string default_language
        string timezone
        string currency
        bool is_active
        timestamp created_at
    }
    
    USERS {
        uuid id PK
        string email UK
        string first_name
        string last_name
        string password_hash
        int status
        bool mfa_enabled
        timestamp created_at
        timestamp activated_at
    }
    
    USER_TENANTS {
        uuid user_id PK,FK
        uuid tenant_id PK,FK
        string role
        string scope
        timestamp created_at
        timestamp updated_at
    }
    
    ROLES {
        uuid id PK
        string name UK
        string description
        bool requires_mfa
        bool is_active
        timestamp created_at
    }
    
    USER_ROLES {
        uuid user_id PK,FK
        uuid role_id PK,FK
    }
    
    SCOPES {
        uuid id PK
        string name UK
        string code
        string description
        bool is_active
        timestamp created_at
    }
    
    PERMISSIONS {
        uuid id PK
        string name UK
        string description
    }
    
    ROLE_PERMISSIONS {
        uuid role_id PK,FK
        uuid permission_id PK,FK
    }
```

## Flux OAuth2 complet avec architecture

```mermaid
sequenceDiagram
    autonumber
    participant User as 👤 Utilisateur
    participant SPA as 🌐 SPA (React/Angular)
    participant IS as 🔐 IdentityServer
    participant CS as 💾 CustomClientStore
    participant DB as 🗄️ Database
    participant Profile as 👥 ProfileService
    participant API as 🚀 API Protected
    
    User->>SPA: Clic "Se connecter"
    SPA->>SPA: Générer PKCE (code_verifier)
    SPA->>IS: /connect/authorize?client_id=my-app&acr_values=tenant:acme-corp
    
    IS->>CS: FindClientByIdAsync("my-app")
    CS->>DB: SELECT Client WHERE ClientName = 'my-app'
    DB-->>CS: Client + AssociatedTenantIds
    CS->>DB: SELECT Tenants WHERE Id IN (...)
    DB-->>CS: Tenants avec AllowedReturnUrls
    CS->>CS: Agréger URLs (Union + Dedupe)
    CS-->>IS: Client configuré
    
    IS-->>SPA: Redirect /api/auth/login?returnUrl=...
    SPA->>User: Afficher formulaire
    User->>SPA: Entrer email/password
    SPA->>IS: POST /api/auth/login
    
    IS->>DB: Vérifier credentials + BelongsToTenant
    DB-->>IS: User valide
    IS->>IS: SignInWithClaimsAsync (tenant_id, tenant_name)
    IS-->>SPA: Redirect avec Authorization Code
    
    SPA->>IS: POST /connect/token (code + code_verifier)
    IS->>IS: Valider PKCE
    IS->>Profile: GetProfileDataAsync()
    Profile->>DB: user.GetTenantContext(tenant_id)
    DB-->>Profile: UserTenant (role, scope)
    Profile->>Profile: Générer claims JWT
    Profile-->>IS: Claims (tenant_id, tenant_role, tenant_scope)
    IS-->>SPA: Access Token + ID Token + Refresh Token
    
    SPA->>API: GET /api/data (Bearer token)
    API->>API: Valider JWT signature
    API-->>SPA: Protected data
```

## Règles métier clés

### ✅ Client

1. Un Client sans Tenant n'est **pas visible** pour IdentityServer
2. Les redirect URIs sont **agrégées** depuis tous les tenants associés
3. Un Client peut avoir `RequireMfa = true` pour forcer MFA sur tous ses utilisateurs

### ✅ Tenant

1. Un Tenant **doit** avoir au moins une `AllowedReturnUrl` pour être opérationnel
2. `Name` est normalisé en lowercase et doit respecter le format `[a-z0-9-]+`
3. Les CORS origins sont des URIs d'**autorité uniquement** (pas de path)

### ✅ User

1. Un User ne peut pas se connecter à un Tenant s'il n'a pas de `UserTenant` correspondant
2. Le JWT contient **uniquement** les claims du tenant de connexion (isolation)
3. MFA peut être activé par utilisateur via `MFAEnabled`

### ✅ UserTenant

1. **Value Object** : son identité est la combinaison `(UserId, TenantId)`
2. `Role` et `Scope` sont des **strings libres** fournis par l'application tierce
3. Pas de validation stricte : l'application tierce décide des valeurs

## Points d'attention DDD

| Concept | Type DDD | Justification |
|---------|----------|---------------|
| User | Aggregate Root | Frontière transactionnelle, a son propre cycle de vie |
| Tenant | Aggregate Root | Frontière transactionnelle, configuration indépendante |
| Client | Aggregate Root | Frontière transactionnelle, configuration OAuth2 |
| UserTenant | **Value Object** | Identité = (UserId, TenantId), pas d'ID propre |
| Email | Value Object | Immuable, égalité par valeur |
| ClientId, TenantId, UserId | Value Object | Strong typing, validation |
| UserStatus | Enumeration | Ensemble fini de valeurs avec comportement |

## Glossaire

- **Aggregate** : Cluster d'entités avec une racine et une frontière transactionnelle
- **Value Object** : Objet sans identité propre, défini par ses attributs
- **Entity** : Objet avec identité unique persistante
- **Domain Event** : Événement métier significatif (UserActivatedEvent, etc.)
- **Invariant** : Règle métier qui doit toujours être vraie
- **Ubiquitous Language** : Vocabulaire partagé entre dev et métier
