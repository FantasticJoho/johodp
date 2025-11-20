# API Complète - Récapitulatif des Fonctionnalités

## ✅ Toutes les Fonctionnalités Implémentées

Ce document liste toutes les fonctionnalités de l'IDP Johodp conformément aux besoins exprimés.

## 1. ✅ Gestion des Clients OAuth2/OIDC

### Endpoints Disponibles

| Méthode | Route | Description | Auth |
|---------|-------|-------------|------|
| **POST** | `/api/clients` | Créer un nouveau client | Oui |
| **GET** | `/api/clients/{clientId}` | Récupérer un client par ID | Oui |
| **GET** | `/api/clients/by-name/{clientName}` | Récupérer un client par nom | Oui |
| **PUT** | `/api/clients/{clientId}` | Mettre à jour un client | Oui |
| **DELETE** | `/api/clients/{clientId}` | Supprimer un client | Oui |

### Exemple de Création

```json
POST /api/clients
{
  "clientName": "spa-app",
  "allowedScopes": ["openid", "profile", "email", "api"],
  "allowedRedirectUris": [
    "https://app.example.com/callback",
    "https://app.example.com/signin-oidc"
  ],
  "allowedCorsOrigins": [
    "https://app.example.com"
  ],
  "requireConsent": true
}
```

### Propriétés Client

- `ClientName` : Nom unique du client
- `AllowedScopes` : Scopes OAuth2/OIDC autorisés
- `AllowedRedirectUris` : URLs de redirection après authentification
- `AllowedCorsOrigins` : Origines CORS autorisées
- `RequireConsent` : Nécessite le consentement de l'utilisateur
- `RequireClientSecret` : Nécessite un secret (toujours true pour l'instant)
- `IsActive` : Client actif ou désactivé

### Commandes et Queries

- `CreateClientCommand` + `CreateClientCommandHandler`
- `UpdateClientCommand` + `UpdateClientCommandHandler`
- `GetClientByIdQuery` + Handler
- `GetClientByNameQuery` + Handler

---

## 2. ✅ CRUD Complet des Tenants

### Endpoints Disponibles

| Méthode | Route | Description | Auth |
|---------|-------|-------------|------|
| **POST** | `/api/tenant` | Créer un nouveau tenant | Oui |
| **GET** | `/api/tenant` | Lister tous les tenants | Oui |
| **GET** | `/api/tenant/{id}` | Récupérer un tenant par ID | Oui |
| **GET** | `/api/tenant/by-name/{name}` | Récupérer un tenant par nom | Oui |
| **PUT** | `/api/tenant/{id}` | Mettre à jour un tenant | Oui |
| **DELETE** | `/api/tenant/{id}` | Supprimer un tenant | Oui |
| **GET** | `/api/tenant/{tenantId}/branding.css` | CSS de branding | Non |
| **GET** | `/api/tenant/{tenantId}/language` | Paramètres de langue | Non |

### Exemple de Création

```json
POST /api/tenant
{
  "name": "acme",
  "displayName": "ACME Corporation",
  "defaultLanguage": "fr-FR",
  "supportedLanguages": ["fr-FR", "en-US"],
  "primaryColor": "#0078d4",
  "secondaryColor": "#106ebe",
  "logoUrl": "https://example.com/logo.png",
  "timezone": "Europe/Paris",
  "currency": "EUR",
  "allowedReturnUrls": [
    "https://acme.com/callback"
  ],
  "associatedClientIds": [
    "acme-spa"
  ]
}
```

### Synchronisation Automatique

**Lors de la création/mise à jour d'un tenant**, les clients associés sont automatiquement synchronisés :
- Les `AllowedReturnUrls` du tenant sont ajoutés aux `AllowedRedirectUris` des clients

Voir `TENANT_MANAGEMENT.md` pour les détails complets.

---

## 3. ✅ Authentification PKCE (Authorization Code Flow)

### Configuration IdentityServer

L'IDP est configuré avec Duende IdentityServer 7 qui supporte nativement PKCE.

### Endpoints IdentityServer Standard

| Endpoint | Description |
|----------|-------------|
| `/.well-known/openid-configuration` | Découverte OIDC |
| `/connect/authorize` | Autorisation avec PKCE |
| `/connect/token` | Échange de code contre token |
| `/connect/userinfo` | Informations utilisateur |
| `/connect/endsession` | Déconnexion |

### Flow PKCE Complet

Voir le diagramme Mermaid dans `README.md` pour le flow complet en 8 étapes.

### Exemple d'Utilisation

```http
# 1. Générer code_verifier et code_challenge
code_verifier = base64url(random(32))
code_challenge = base64url(sha256(code_verifier))

# 2. Demande d'autorisation
GET /connect/authorize?
  response_type=code&
  client_id=spa-app&
  redirect_uri=https://app.com/callback&
  scope=openid profile email&
  code_challenge=xyz123&
  code_challenge_method=S256&
  acr_values=tenant:acme

# 3. Échange du code
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code&
code=AUTH_CODE&
redirect_uri=https://app.com/callback&
client_id=spa-app&
code_verifier=ORIGINAL_VERIFIER
```

---

## 4. ✅ Authentification Cookie via API

### Endpoint

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "password123"
}

?acr_values=tenant:acme  (optionnel)
```

### Réponse

```json
{
  "message": "Login successful",
  "email": "user@example.com"
}
```

**Headers de Réponse :**
```
Set-Cookie: .AspNetCore.Identity.Application=...; Path=/; HttpOnly; SameSite=Lax
```

### Validation Multi-Tenant

L'endpoint vérifie automatiquement que l'utilisateur appartient au tenant demandé :
- Si `acr_values=tenant:acme`, l'utilisateur doit avoir "acme" dans ses tenants
- Si l'utilisateur a le tenant "*" (wildcard), il accède à tous les tenants
- Si aucun tenant n'est spécifié (`acr_values=tenant:*`), n'importe quel utilisateur peut se connecter

---

## 5. ✅ Création d'Utilisateurs Multi-Tenant

### Endpoints Disponibles

| Méthode | Route | Description | Auth |
|---------|-------|-------------|------|
| **POST** | `/api/users/register` | Créer un utilisateur | Non |
| **GET** | `/api/users/{userId}` | Récupérer un utilisateur | Oui |
| **POST** | `/api/users/{userId}/tenants/{tenantId}` | Ajouter utilisateur à un tenant | Oui |
| **DELETE** | `/api/users/{userId}/tenants/{tenantId}` | Retirer utilisateur d'un tenant | Oui |
| **GET** | `/api/users/{userId}/tenants` | Lister les tenants de l'utilisateur | Oui |

### Exemple de Création avec Tenant Initial

```json
POST /api/users/register
{
  "email": "john.doe@example.com",
  "password": "SecurePassword123!",
  "firstName": "John",
  "lastName": "Doe",
  "tenantId": "acme"
}
```

### Gestion Multi-Tenant

Un utilisateur peut appartenir à **plusieurs tenants** :

```http
# Ajouter l'utilisateur à un nouveau tenant
POST /api/users/123e4567-e89b-12d3-a456-426614174000/tenants/contoso

# Retirer l'utilisateur d'un tenant
DELETE /api/users/123e4567-e89b-12d3-a456-426614174000/tenants/acme

# Voir tous les tenants de l'utilisateur
GET /api/users/123e4567-e89b-12d3-a456-426614174000/tenants

Réponse:
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "tenants": ["acme", "contoso", "fabrikam"]
}
```

### Wildcard Access

Un utilisateur avec le tenant `"*"` a accès à **tous les tenants** :

```http
POST /api/users/123e4567-e89b-12d3-a456-426614174000/tenants/*
```

### Claims JWT

Les tokens JWT incluent **tous** les tenants de l'utilisateur :

```json
{
  "sub": "123e4567-e89b-12d3-a456-426614174000",
  "email": "john.doe@example.com",
  "given_name": "John",
  "family_name": "Doe",
  "tenant_id": ["acme", "contoso"],
  ...
}
```

---

## Architecture Technique

### Couches DDD

```
┌─────────────────────────────────────────┐
│           API Layer                     │
│  - Controllers (Clients, Tenants,       │
│    Users, Account)                      │
│  - Authentication Middleware            │
└─────────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────────┐
│       Application Layer                 │
│  - Commands & CommandHandlers           │
│  - Queries & QueryHandlers              │
│  - DTOs                                 │
└─────────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────────┐
│         Domain Layer                    │
│  - Aggregates (Client, Tenant, User)    │
│  - Value Objects                        │
│  - Domain Events                        │
└─────────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────────┐
│      Infrastructure Layer               │
│  - Repositories                         │
│  - EF Core Configurations               │
│  - IdentityServer Integration           │
└─────────────────────────────────────────┘
```

### Technologies

- **.NET 8.0** : Framework principal
- **ASP.NET Core MVC** : API et pages Razor
- **Duende IdentityServer 7** : OAuth2/OIDC avec PKCE
- **Entity Framework Core** : ORM
- **PostgreSQL** : Base de données
- **Serilog** : Logging structuré
- **MediatR** : Pattern CQRS

---

## Base de Données

### Migrations

Deux migrations ont été créées :

1. **AddTenantEntity** : Ajoute la table `tenants`
2. **UpdateUserMultiTenant** : Change `User.TenantId` (string) en `User.TenantIds` (JSONB array)

### Appliquer les Migrations

```bash
# Démarrer PostgreSQL
docker-compose up -d postgres

# Appliquer toutes les migrations
cd src/Johodp.Infrastructure
dotnet ef database update --startup-project ../Johodp.Api
```

### Schémas Principaux

**Table `clients` :**
- `Id` (UUID PK)
- `ClientName` (string, unique)
- `AllowedScopes` (string[])
- `AllowedRedirectUris` (string[])
- `AllowedCorsOrigins` (string[])
- `RequireClientSecret` (bool)
- `RequireConsent` (bool)
- `IsActive` (bool)

**Table `tenants` :**
- `Id` (UUID PK)
- `Name` (string, unique, index)
- `DisplayName` (string)
- `PrimaryColor`, `SecondaryColor`, `LogoUrl`, `BackgroundImageUrl`, `CustomCss`
- `DefaultLanguage`, `Timezone`, `Currency`
- `SupportedLanguages` (JSONB)
- `AllowedReturnUrls` (JSONB)
- `AssociatedClientIds` (JSONB)
- `IsActive` (bool)

**Table `users` :**
- `Id` (UUID PK)
- `Email` (string, unique)
- `FirstName`, `LastName`
- `PasswordHash`
- `TenantIds` (JSONB array) ← **Multi-tenant support**
- `EmailConfirmed`, `IsActive`, `MFAEnabled` (bool)
- Relations: `Roles`, `Permissions`, `Scope`

---

## Tests Recommandés

### 1. Test Client CRUD

```bash
# Créer un client
POST /api/clients
Authorization: Bearer YOUR_TOKEN
{
  "clientName": "test-spa",
  "allowedScopes": ["openid", "profile"],
  "allowedRedirectUris": ["https://localhost:4200/callback"],
  "allowedCorsOrigins": ["https://localhost:4200"]
}

# Récupérer le client
GET /api/clients/{id}

# Mettre à jour
PUT /api/clients/{id}
{
  "allowedScopes": ["openid", "profile", "email"]
}

# Supprimer
DELETE /api/clients/{id}
```

### 2. Test Tenant CRUD

```bash
# Créer un tenant
POST /api/tenant
{
  "name": "test-tenant",
  "displayName": "Test Tenant",
  "primaryColor": "#ff0000"
}

# Vérifier la synchronisation du client
GET /api/clients/by-name/test-spa
# Les returnUrls du tenant doivent apparaître dans allowedRedirectUris
```

### 3. Test Multi-Tenant User

```bash
# Créer un utilisateur
POST /api/users/register
{
  "email": "multi@test.com",
  "password": "Test123!",
  "firstName": "Multi",
  "lastName": "Tenant",
  "tenantId": "acme"
}

# Ajouter à un autre tenant
POST /api/users/{userId}/tenants/contoso

# Vérifier les tenants
GET /api/users/{userId}/tenants
# Devrait retourner ["acme", "contoso"]

# Tester l'authentification avec tenant
POST /api/auth/login?acr_values=tenant:contoso
{
  "email": "multi@test.com",
  "password": "Test123!"
}
# Devrait réussir

POST /api/auth/login?acr_values=tenant:fabrikam
# Devrait échouer (401 Unauthorized)
```

### 4. Test PKCE Flow

Voir `README.md` et `httpTest/pkceconnection.http` pour les exemples complets.

---

## Documentation Complète

| Document | Description |
|----------|-------------|
| `README.md` | Guide de démarrage rapide + diagramme PKCE |
| `ARCHITECTURE.md` | Architecture DDD et structure du projet |
| `TENANT_MANAGEMENT.md` | Guide complet de gestion des tenants |
| `JOURNALISATION.md` | Bonnes pratiques de logging |
| `CACHE.md` | Stratégies de cache pour DDD |
| `API_ENDPOINTS.md` | Documentation API (si existe) |

---

## Checklist de Validation

### ✅ Clients
- [x] Créer un client
- [x] Récupérer un client (par ID et par nom)
- [x] Mettre à jour un client
- [x] Supprimer un client
- [x] Logging sur toutes les opérations

### ✅ Tenants  
- [x] Créer un tenant
- [x] Lister tous les tenants
- [x] Récupérer un tenant (par ID et par nom)
- [x] Mettre à jour un tenant
- [x] Supprimer un tenant
- [x] Synchronisation automatique avec clients
- [x] Branding et langue publics

### ✅ PKCE Authentication
- [x] Authorization Code Flow avec PKCE
- [x] Support des tenants via `acr_values`
- [x] Validation multi-tenant
- [x] Claims JWT avec tenant_id

### ✅ Cookie Authentication
- [x] Endpoint `/api/auth/login`
- [x] Cookie ASP.NET Identity
- [x] Validation multi-tenant
- [x] Logging des tentatives

### ✅ Users Multi-Tenant
- [x] Créer utilisateur avec tenant initial
- [x] Ajouter utilisateur à un tenant
- [x] Retirer utilisateur d'un tenant
- [x] Lister les tenants d'un utilisateur
- [x] Support wildcard (`*`)
- [x] Claims JWT multi-tenant

---

## Prochaines Étapes (Optionnel)

### Améliorations Possibles

1. **Sécurité**
   - [ ] Implémenter les rôles et permissions
   - [ ] MFA (Multi-Factor Authentication)
   - [ ] Rate limiting sur les endpoints sensibles

2. **Performance**
   - [ ] Cache distribué (Redis) pour clients et tenants
   - [ ] Pagination sur les listes
   - [ ] Batch operations pour clients

3. **Monitoring**
   - [ ] Dashboard Grafana + Prometheus
   - [ ] Alertes sur erreurs critiques
   - [ ] Métriques métier (nb connexions, tenants actifs)

4. **Tests**
   - [ ] Tests unitaires pour tous les handlers
   - [ ] Tests d'intégration des endpoints
   - [ ] Tests E2E du flow PKCE

5. **Documentation**
   - [ ] OpenAPI/Swagger pour tous les endpoints
   - [ ] Postman collection complète
   - [ ] Guide de déploiement en production

---

## Résumé

✅ **100% des fonctionnalités demandées sont implémentées :**

1. ✅ **Ajouter des clients** : POST /api/clients avec CRUD complet
2. ✅ **CRUD Tenants** : Create, Read, Update, Delete avec synchronisation clients
3. ✅ **Connexion PKCE** : Authorization Code Flow avec IdentityServer
4. ✅ **Authentification cookie** : POST /api/auth/login avec Set-Cookie
5. ✅ **Utilisateurs multi-tenant** : Un utilisateur peut appartenir à plusieurs tenants

Le projet compile sans erreur et les migrations sont prêtes à être appliquées ! 🚀
