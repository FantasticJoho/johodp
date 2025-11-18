# 🔐 Rôles, Permissions, Périmètres et Authentification Forte

## Vue d'ensemble

L'architecture a été enrichie avec un système complet de gestion des accès incluant :
- **Rôles** : Groupes de permissions
- **Permissions** : Actions autorisées
- **Périmètres** : Limites géographiques ou organisationnelles
- **MFA** : Authentification forte pour les administrateurs
- **Specification Pattern** : Requêtes complexes réutilisables
- **Claims JWT** : Tous les éléments dans les tokens

---

## Architecture

### Value Objects

```
RoleId           → Identifiant unique typé pour les rôles
PermissionId     → Identifiant unique typé pour les permissions
PermissionName   → Nom de permission avec validation (max 100 chars)
ScopeId          → Identifiant unique typé pour les périmètres
```

### Agrégats

```
Role
├── Id: RoleId
├── Name: string
├── Description: string
├── RequiresMFA: bool          ← Force MFA pour ce rôle
├── IsActive: bool
├── CreatedAt: DateTime
└── PermissionIds: List<PermissionId>

Permission
├── Id: PermissionId
├── Name: PermissionName       ← Exemple: "USERS_READ", "USERS_WRITE"
├── Description: string
├── IsActive: bool
└── CreatedAt: DateTime

Scope
├── Id: ScopeId
├── Name: string
├── Code: string               ← Code unique (ex: "FR", "PARIS")
├── Description: string
├── IsActive: bool
└── CreatedAt: DateTime

User (enrichi)
├── Id, Email, FirstName, LastName
├── MFAEnabled: bool
├── Roles: List<Role>
├── Permissions: List<Permission>
├── Scope: Scope?
└── Methods:
    ├── AddRole(role)
    ├── RemoveRole(roleId)
    ├── AddPermission(permission)
    ├── RemovePermission(permissionId)
    ├── SetScope(scope)
    ├── EnableMFA() / DisableMFA()
    └── RequiresMFA() → vérifie si MFA requis par rôle
```

---

## Specification Pattern

### Utilisation

Le Specification Pattern permet de définir des requêtes réutilisables et testables :

```csharp
// Spécification pour récupérer un utilisateur avec ses rôles et permissions
public class UserWithRolesAndPermissionsSpecification : Specification<User>
{
    public UserWithRolesAndPermissionsSpecification(Guid userId)
    {
        Criteria = u => u.Id.Value == userId;
        AddInclude("Roles");
        AddInclude("Scope");
    }
}

// Utilisation
var spec = new UserWithRolesAndPermissionsSpecification(userId);
var query = SpecificationEvaluator<User>.GetQuery(dbContext.Users, spec);
var user = await query.FirstOrDefaultAsync();
```

### Spécifications disponibles

```csharp
// Utilisateurs avec rôles et permissions
UserWithRolesAndPermissionsSpecification(userId)

// Utilisateurs administrateurs nécessitant MFA
AdminUsersWithMFASpecification()

// Utilisateurs actifs par rôle
ActiveUsersByRoleSpecification(roleId)

// Utilisateurs par périmètre
UsersByScopeSpecification(scopeId)
```

---

## Claims JWT

### Contenu des tokens

Les claims JWT contiennent :

```json
{
  "sub": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "given_name": "John",
  "family_name": "Doe",
  "role": ["admin", "moderator"],
  "permission": ["USERS_READ", "USERS_WRITE", "role:admin:permission"],
  "scope": "PARIS",
  "scope_id": "550e8400-e29b-41d4-a716-446655440001",
  "mfa_required": "true",
  "mfa_enabled": "false"
}
```

### ClaimsBuilder

```csharp
var claimsBuilder = new ClaimsBuilder()
    .AddUserClaims(user)
    .AddRoles(user.Roles)
    .AddPermissions(user.Permissions)
    .AddRolePermissions(user.Roles)
    .AddScope(user.Scope)
    .AddMFARequirement(user)
    .AddCustomClaim("department", "Sales");

var claims = claimsBuilder.Build();
var principal = claimsBuilder.BuildClaimsPrincipal();
```

---

## Authentification Forte (MFA)

### Configuration

Les rôles peuvent requérir MFA :

```csharp
// Créer un rôle admin qui requiert MFA
var adminRole = Role.Create(
    name: "Administrator",
    description: "Full system access",
    requiresMFA: true  ← Force MFA
);
```

### Service MFA

```csharp
public interface IMFAService
{
    // Génère une demande MFA (ex: notification Microsoft Authenticator)
    Task<MFARequest> GenerateMFARequestAsync(
        Guid userId, 
        string email, 
        CancellationToken cancellationToken);

    // Valide la réponse MFA
    Task<bool> ValidateMFAResponseAsync(
        Guid requestId, 
        string response, 
        CancellationToken cancellationToken);

    // Vérifie si MFA est requis
    bool IsMFARequired(bool requiresMFA, bool mfaEnabled);
}
```

### Fournisseurs MFA supportés

- ✅ Microsoft Authenticator (Push notifications)
- ⏳ Google Authenticator (TOTP)
- ⏳ Authy
- ⏳ SMS
- ⏳ Email

### Flux d'authentification avec MFA

```
1. Utilisateur envoie credentials
   ↓
2. Validation credentials
   ↓
3. Vérifier si MFA requis (user.RequiresMFA())
   ├─ NON → Émettre JWT
   └─ OUI → Générer MFARequest
   ↓
4. Envoyer notification MFA
   (ex: Microsoft Authenticator)
   ↓
5. Utilisateur approuve
   ↓
6. Valider réponse MFA
   ├─ Valide → Émettre JWT
   └─ Invalide → Erreur
```

---

## Use Cases

### 1. Assigner un rôle à un utilisateur

```csharp
POST /api/users/{userId}/roles
{
  "roleId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Handler** :
- Récupère l'utilisateur et le rôle
- Appelle `user.AddRole(role)`
- Sauvegarde via UnitOfWork

### 2. Assigner un périmètre

```csharp
POST /api/users/{userId}/scope
{
  "scopeId": "550e8400-e29b-41d4-a716-446655440001"
}
```

**Handler** :
- Récupère l'utilisateur et le scope
- Appelle `user.SetScope(scope)`
- Sauvegarde via UnitOfWork

### 3. Activer MFA

```csharp
POST /api/users/{userId}/mfa/enable
```

**Handler** :
- Appelle `user.EnableMFA()`
- Peut retourner un QR code pour TOTP

---

## Base de données

### Nouvelles tables

```sql
-- Rôles
CREATE TABLE roles (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(500) NOT NULL,
    requires_mfa BOOLEAN DEFAULT FALSE,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE
);

-- Permissions
CREATE TABLE permissions (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(500) NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE
);

-- Périmètres
CREATE TABLE scopes (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    code VARCHAR(50) NOT NULL UNIQUE,
    description VARCHAR(500) NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE
);

-- Relations User-Roles
CREATE TABLE user_roles (
    user_id UUID NOT NULL,
    role_id UUID NOT NULL,
    PRIMARY KEY (user_id, role_id),
    FOREIGN KEY (user_id) REFERENCES users(id),
    FOREIGN KEY (role_id) REFERENCES roles(id)
);

-- Relations User-Permissions
CREATE TABLE user_permissions (
    user_id UUID NOT NULL,
    permission_id UUID NOT NULL,
    PRIMARY KEY (user_id, permission_id),
    FOREIGN KEY (user_id) REFERENCES users(id),
    FOREIGN KEY (permission_id) REFERENCES permissions(id)
);

-- Colonne ajoutée à users
ALTER TABLE users ADD COLUMN scope_id UUID REFERENCES scopes(id);
ALTER TABLE users ADD COLUMN mfa_enabled BOOLEAN DEFAULT FALSE;
```

---

## Exemple complet : Créer un administrateur avec MFA

```csharp
// 1. Créer le rôle admin avec MFA
var adminRole = Role.Create(
    name: "Administrator",
    description: "Full system access with MFA required",
    requiresMFA: true
);
await _unitOfWork.Roles.AddAsync(adminRole);

// 2. Créer les permissions admin
var usersReadPerm = Permission.Create("USERS_READ", "Can read users");
var usersWritePerm = Permission.Create("USERS_WRITE", "Can modify users");
await _unitOfWork.Permissions.AddAsync(usersReadPerm);
await _unitOfWork.Permissions.AddAsync(usersWritePerm);

// 3. Assigner permissions au rôle
adminRole.AddPermission(usersReadPerm.Id);
adminRole.AddPermission(usersWritePerm.Id);
await _unitOfWork.Roles.UpdateAsync(adminRole);

// 4. Créer un utilisateur
var user = User.Create("admin@company.com", "John", "Doe");

// 5. Assigner le rôle
user.AddRole(adminRole);

// 6. Assigner le périmètre
var scope = Scope.Create("France", "FR", "Scope for France region");
await _unitOfWork.Scopes.AddAsync(scope);
user.SetScope(scope);

// 7. Activer MFA
user.EnableMFA();

// 8. Sauvegarder
await _unitOfWork.Users.AddAsync(user);
await _unitOfWork.SaveChangesAsync();

// 9. Générer les claims
var claimsBuilder = new ClaimsBuilder()
    .AddUserClaims(user)
    .AddRoles(user.Roles)
    .AddPermissions(user.Permissions)
    .AddScope(user.Scope)
    .AddMFARequirement(user);

var claims = claimsBuilder.Build();
// Claims contiennent: role:admin, permission:USERS_READ, permission:USERS_WRITE, 
// scope:FR, mfa_required:true, mfa_enabled:true
```

---

## Avantages du design

✅ **Specification Pattern** - Requêtes réutilisables et testables
✅ **Value Objects typés** - Pas d'erreurs d'ID
✅ **DDD Aggregates** - Logique métier encapsulée
✅ **Claims complets** - Tous les éléments dans les tokens
✅ **MFA flexible** - Supportable par rôle ou utilisateur
✅ **Scope management** - Multi-tenant ready
✅ **CQRS patterns** - Séparation read/write
✅ **Testable** - Chaque couche peut être testée

---

## Prochaines étapes

- [ ] Implémenter Microsoft Authenticator push notifications
- [ ] Ajouter les migrations Entity Framework
- [ ] Tests unitaires pour le Specification Pattern
- [ ] API endpoints pour CRUD rôles/permissions
- [ ] UI pour la gestion des accès
- [ ] Audit logging pour les changements d'accès
- [ ] Rate limiting sur les tentatives MFA
