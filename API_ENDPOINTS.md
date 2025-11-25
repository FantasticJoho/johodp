# 📡 API Endpoints Reference

## Base URL
```
https://localhost:5001/api
```

## Users Endpoints

### 1. Register a New User
**POST** `/api/users/register`

Crée un nouvel utilisateur en statut **PendingActivation** (appelé par l'application tierce).

**Authentification :** `[AllowAnonymous]` (TODO: Sécuriser avec API Key)

#### Request Body
```json
{
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "tenantId": "acme",
  "createAsPending": true
}
```

#### Request Parameters
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| email | string | ✅ | Adresse e-mail unique |
| firstName | string | ✅ | Prénom (max 50 caractères) |
| lastName | string | ✅ | Nom (max 50 caractères) |
| tenantId | string | ✅ | Identifiant du tenant |
| createAsPending | boolean | Auto | Forcé à `true` par le controller |

#### Response 201 Created
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "john.doe@example.com",
  "status": "PendingActivation",
  "message": "User created successfully. Activation email will be sent."
}
```

#### Response 409 Conflict
```json
{
  "message": "User with email john.doe@example.com already exists"
}
```

#### Response 400 Bad Request
```json
{
  "errors": [
    "Email is required",
    "Email format is invalid",
    "First name is required",
    "Last name is required"
  ]
}
```

#### Example cURL
```bash
curl -X POST https://localhost:5001/api/users/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe"
  }'
```

#### Example PowerShell
```powershell
$body = @{
  email = "john.doe@example.com"
  firstName = "John"
  lastName = "Doe"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:5001/api/users/register" `
  -Method Post `
  -Body $body `
  -ContentType "application/json"
```

#### Example C# (HttpClient)
```csharp
var client = new HttpClient();
var request = new {
  email = "john.doe@example.com",
  firstName = "John",
  lastName = "Doe"
};

var json = JsonConvert.SerializeObject(request);
var content = new StringContent(json, Encoding.UTF8, "application/json");

var response = await client.PostAsync(
  "https://localhost:5001/api/users/register", 
  content);
```

---

### 2. Get User by ID
**GET** `/users/{userId}`

Récupère les détails d'un utilisateur par son ID.

#### URL Parameters
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| userId | GUID | ✅ | Identifiant unique de l'utilisateur |

#### Response 200 OK
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "emailConfirmed": false,
  "isActive": true,
  "createdAt": "2025-11-17T14:30:00Z"
}
```

#### Response 404 Not Found
```json
{
  "message": "User with ID 550e8400-e29b-41d4-a716-446655440000 not found"
}
```

#### Response 400 Bad Request
```json
{
  "message": "Invalid user ID format"
}
```

#### Example cURL
```bash
curl -X GET https://localhost:5001/api/users/550e8400-e29b-41d4-a716-446655440000 \
  -H "Accept: application/json"
```

#### Example PowerShell
```powershell
Invoke-RestMethod -Uri "https://localhost:5001/api/users/550e8400-e29b-41d4-a716-446655440000" `
  -Method Get `
  -ContentType "application/json"
```

#### Example C# (HttpClient)
```csharp
var client = new HttpClient();
var userId = "550e8400-e29b-41d4-a716-446655440000";

var response = await client.GetAsync(
  $"https://localhost:5001/api/users/{userId}");

if (response.IsSuccessStatusCode)
{
  var json = await response.Content.ReadAsStringAsync();
  var user = JsonConvert.DeserializeObject<UserDto>(json);
}
```

---

## Account Management Endpoints (NEW)

### 3. Onboarding - Display Form
**GET** `/account/onboarding`

Affiche le formulaire d'onboarding avec le branding du tenant.

#### Query Parameters
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| acr_values | string | ✅ | Format `tenant:{tenantId}` |
| return_url | string | ❌ | URL de retour après activation |

#### Example
```
GET /account/onboarding?acr_values=tenant:acme&return_url=https://app.acme.com/dashboard
```

---

### 4. Onboarding - Submit Request
**POST** `/account/onboarding`

Traite la demande d'onboarding et notifie l'application tierce (fire-and-forget).

#### Request Body (Form)
```json
{
  "tenantId": "acme",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "returnUrl": "https://app.acme.com/dashboard"
}
```

---

### 5. Activate - Display Form
**GET** `/account/activate`

Affiche le formulaire d'activation avec validation du token.

#### Query Parameters
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| token | string | ✅ | Token d'activation Identity |
| userId | string | ✅ | GUID de l'utilisateur |
| tenant | string | ✅ | Identifiant du tenant |

---

### 6. Activate - Set Password
**POST** `/account/activate`

Active le compte en définissant le mot de passe.

#### Request Body (Form)
```json
{
  "token": "CfDJ8N...",
  "userId": "b8c4d9e5-f6a7-8901-b2c3-d4e5f6g7h8i9",
  "tenantId": "acme",
  "newPassword": "SecureP@ssw0rd!",
  "confirmPassword": "SecureP@ssw0rd!",
  "returnUrl": "https://app.acme.com/dashboard"
}
```

---

## Response Status Codes

| Code | Meaning | Description |
|------|---------|-------------|
| 200 | OK | Requête réussie |
| 201 | Created | Ressource créée avec succès |
| 400 | Bad Request | Requête invalide (validation échouée) |
| 404 | Not Found | Ressource non trouvée |
| 409 | Conflict | Conflit (ex: email déjà existant) |
| 500 | Internal Server Error | Erreur serveur |

---

## Data Models

### UserDto
```json
{
  "id": "GUID",
  "email": "string",
  "firstName": "string",
  "lastName": "string",
  "emailConfirmed": "boolean",
  "isActive": "boolean",
  "createdAt": "ISO 8601 DateTime"
}
```

### RegisterUserResponse
```json
{
  "userId": "GUID",
  "email": "string"
}
```

---

## Validation Rules

### Email
- ✅ Requis
- ✅ Format valide (contient @)
- ✅ Unique en base de données
- ✅ Converti en minuscules

### First Name
- ✅ Requis
- ✅ Maximum 50 caractères

### Last Name
- ✅ Requis
- ✅ Maximum 50 caractères

---

## Authentication & Authorization

Actuellement, l'API n'a pas d'authentification.

**À faire** : Implémenter la sécurité JWT/OAuth2 via IdentityServer4

```csharp
// Exemple futur avec authentification
Authorization: Bearer {token}
```

---

## Pagination

Non implémentée actuellement.

---

## Rate Limiting

Non implémentée actuellement.

---

## Authentication API Endpoints (NEW)

### 7. Register User via API
**POST** `/api/auth/register`

Enregistre un nouvel utilisateur via JSON (pour mobile/SPA).

#### Request Body
```json
{
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "password": "SecureP@ssw0rd123!",
  "confirmPassword": "SecureP@ssw0rd123!"
}
```

#### Response 201 Created
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "message": "User registered successfully. Please check your email for activation."
}
```

---

### 8. Login via API
**POST** `/api/auth/login`

Authentifie un utilisateur et crée une session.

#### Request Body
```json
{
  "email": "user@example.com",
  "password": "SecureP@ssw0rd123!"
}
```

#### Response 201 Created
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "message": "Login successful"
}
```

---

### 9. Logout via API
**POST** `/api/auth/logout`

Déconnecte l'utilisateur et invalide la session.

#### Response 200 OK
```json
{
  "message": "Logout successful"
}
```

---

### 10. Forgot Password
**POST** `/api/auth/forgot-password`

Demande un token de réinitialisation de mot de passe.

#### Request Body
```json
{
  "email": "user@example.com"
}
```

#### Response 200 OK (DEV mode)
```json
{
  "message": "Password reset email sent",
  "token": "CfDJ8N...",
  "resetUrl": "https://localhost:5001/account/reset-password?token=..."
}
```

⚠️ **PROD mode**: Token NOT returned (sent via email only)

---

### 11. Reset Password
**POST** `/api/auth/reset-password`

Réinitialise le mot de passe avec le token.

#### Request Body
```json
{
  "email": "user@example.com",
  "token": "CfDJ8N...",
  "password": "NewP@ssw0rd123!",
  "confirmPassword": "NewP@ssw0rd123!"
}
```

#### Response 200 OK
```json
{
  "message": "Password reset successful"
}
```

---

### 12. Onboarding via API
**POST** `/api/account/onboarding`

Soumet une demande d'onboarding (notification envoyée à l'app tierce).

#### Request Body
```json
{
  "tenantId": "acme",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe"
}
```

#### Response 202 Accepted
```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "pending",
  "message": "Onboarding request submitted. You will receive an email if approved."
}
```

---

## CORS Configuration

### ⚠️ IMPORTANT: CORS Security Limitations

**CORS protège UNIQUEMENT les navigateurs web !**

```
┌────────────────────────────────────────────────┐
│  ✅ CORS protège:                              │
│     - Navigateurs web (Chrome, Firefox, etc.)  │
│     - JavaScript (fetch, axios, XMLHttpRequest)│
│     - Applications SPA (React, Angular, Vue)   │
│                                                │
│  ❌ CORS NE protège PAS:                       │
│     - curl / wget / Postman / Insomnia         │
│     - Applications serveur (Node.js, Python)   │
│     - Applications mobile natives              │
│     - Scripts backend / API-to-API calls       │
└────────────────────────────────────────────────┘
```

### Architecture CORS

- **AllowedCorsOrigins** est géré au niveau **Tenant** (pas Client)
- Chaque tenant définit ses origines CORS autorisées
- IdentityServer agrège dynamiquement les CORS de tous les tenants associés

#### Exemple Configuration Tenant
```json
{
  "name": "acme",
  "allowedReturnUrls": [
    "http://localhost:4200/callback",
    "https://app.acme.com/callback"
  ],
  "allowedCorsOrigins": [
    "http://localhost:4200",
    "https://app.acme.com"
  ]
}
```

### Contournement CORS (exemple)

```bash
# ❌ Bloqué dans un navigateur (origine non autorisée)
fetch('https://api.johodp.com/api/auth/login', {
  method: 'POST',
  body: JSON.stringify({ email: 'test@example.com', password: 'pass' })
})
// ERROR: CORS policy: No 'Access-Control-Allow-Origin' header

# ✅ Fonctionne avec curl (pas de vérification CORS)
curl -X POST https://api.johodp.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"pass"}'
# SUCCESS: Retourne le userId sans vérification CORS
```

### Vraie Sécurité

**CORS est une commodité UX, PAS une sécurité !**

Protection réelle:
1. **Authentication** - OAuth2/OIDC tokens
2. **Authorization** - Claims & Policies
3. **Rate Limiting** - Limite les abus
4. **API Keys** - Identification client (optionnel)
5. **IP Whitelist** - Restriction géographique (optionnel)

---

## Changelog des Endpoints

### v3.0 (2025-11-24) - API Authentication Endpoints + CORS Migration
- ✅ POST /api/auth/register - Enregistrement via JSON API
- ✅ POST /api/auth/login - Login via JSON API
- ✅ POST /api/auth/logout - Logout via JSON API
- ✅ POST /api/auth/forgot-password - Demande reset password via API
- ✅ POST /api/auth/reset-password - Reset password via API
- ✅ POST /api/account/onboarding - Onboarding via JSON API
- ✅ Migration CORS - AllowedCorsOrigins déplacé de Client vers Tenant
- ✅ CustomClientStore - Agrégation CORS depuis tenants
- ✅ Documentation - Avertissements sécurité CORS

### v2.0 (2025-11-20) - Onboarding Flow
- ✅ GET /account/onboarding - Formulaire d'inscription avec branding tenant
- ✅ POST /account/onboarding - Traitement demande + notification app tierce
- ✅ GET /account/activate - Formulaire activation avec token
- ✅ POST /account/activate - Définir mot de passe et activer compte
- ✅ POST /api/users/register - Modifié pour créer en PendingActivation ([AllowAnonymous])
- ✅ Migration database - Ajout Status, ActivatedAt, NotificationUrl, ApiKey

### v1.0 (2025-11-17) - Initial Release
- ✅ POST /api/users/register - Enregistrer un utilisateur
- ✅ GET /api/users/{userId} - Récupérer un utilisateur
- ⏳ GET /api/users - Lister tous les utilisateurs (à faire)
- ⏳ PUT /api/users/{userId} - Mettre à jour un utilisateur (à faire)
- ⏳ DELETE /api/users/{userId} - Supprimer un utilisateur (à faire)

---

## Swagger/OpenAPI

La documentation interactive est disponible sur:
```
https://localhost:5001/swagger
```

---

# 🧩 Clients Endpoints

### Create Client
**POST** `/api/clients`

Crée un client OAuth2 (aucun tenant associé initialement). (US-1.1 / UC-01)

**Auth:** `Bearer` (scope `johodp.admin` via client credentials UC-00)

**Request Body**
```json
{
  "clientName": "my-spa-app",
  "allowedScopes": ["openid", "profile", "email", "api"],
  "requireConsent": true
}
```

### Get Client by Id
**GET** `/api/clients/{clientId}` (US-1.2)

### Get Client by Name
**GET** `/api/clients/by-name/{clientName}` (US-1.3)

### Update Client
**PUT** `/api/clients/{clientId}` (US-1.4)

### Delete Client
**DELETE** `/api/clients/{clientId}` (US-1.5)

---

# 🏢 Tenants Endpoints

### Create Tenant
**POST** `/api/tenant` (US-2.1 / UC-02)

Inclut `userVerificationEndpoint` (HTTPS en production) pour webhook d'onboarding.

**Request Body (exemple)**
```json
{
  "name": "acme-corp",
  "displayName": "ACME Corporation",
  "clientId": "my-spa-app",
  "allowedReturnUrls": ["http://localhost:4200/callback"],
  "allowedCorsOrigins": ["http://localhost:4200"],
  "userVerificationEndpoint": "https://api.acme.com/webhooks/johodp/verify-user",
  "branding": {"primaryColor": "#007bff"},
  "localization": {"defaultLanguage": "fr-FR", "timezone": "Europe/Paris", "currency": "EUR"}
}
```

### List Tenants
**GET** `/api/tenant` (US-2.2)

### Get Tenant by Id
**GET** `/api/tenant/{id}` (US-2.3)

### Get Tenant by Name
**GET** `/api/tenant/by-name/{name}` (US-2.4)

### Update Tenant
**PUT** `/api/tenant/{id}` (US-2.5)

### Delete Tenant
**DELETE** `/api/tenant/{id}` (US-2.6)

### Tenant Branding CSS
**GET** `/api/tenant/{tenantId}/branding.css` (US-2.7 / UC-10)

### Tenant Localization
**GET** `/api/tenant/{tenantId}/language` (US-2.8 / UC-11)

---

# 👥 User Multi-Tenant Access

### Add User to Tenant
**POST** `/api/users/{userId}/tenants/{tenantId}` (US-3.3 / UC-09)

### Remove User From Tenant
**DELETE** `/api/users/{userId}/tenants/{tenantId}` (US-3.4 / UC-09)

### List User Tenants
**GET** `/api/users/{userId}/tenants` (US-3.5)

### Current User Profile
**GET** `/api/users/me` (Protected – requires valid access token; US-6.7 / UC-07)

---

# 🔐 OAuth2 / OIDC Endpoints

Base IdentityServer endpoints (hors préfixe `/api`).

### Authorization Request
**GET** `/connect/authorize` (UC-06)

Exemple:
```
/connect/authorize?client_id=my-spa-app&response_type=code&scope=openid profile email&redirect_uri=http://localhost:4200/callback&code_challenge=<challenge>&code_challenge_method=S256&acr_values=tenant:acme-corp
```

### Token Exchange
**POST** `/connect/token`

Grant Types supportés:
- `authorization_code` (PKCE obligatoire)
- `refresh_token` (UC-08 / US-6.6)
- `client_credentials` (UC-00 / US-7.1)

#### Authorization Code Flow Body
```json
{
  "grant_type": "authorization_code",
  "code": "abc123",
  "redirect_uri": "http://localhost:4200/callback",
  "client_id": "my-spa-app",
  "code_verifier": "<original_verifier>"
}
```

#### Refresh Token Flow Body
```json
{
  "grant_type": "refresh_token",
  "refresh_token": "rft123",
  "client_id": "my-spa-app"
}
```

#### Client Credentials Flow Body
```json
{
  "grant_type": "client_credentials",
  "client_id": "third-party-app",
  "client_secret": "secret-value",
  "scope": "johodp.admin"
}
```

### Token Response (exemple)
```json
{
  "access_token": "eyJ...",
  "id_token": "eyJ...",
  "refresh_token": "def456",
  "expires_in": 3600,
  "token_type": "Bearer"
}
```

---

# 📡 Webhook de Vérification Utilisateur (Onboarding)

Utilisé pendant UC-04 et US-4.2 pour validation métier avant création de l'utilisateur.

### Envoi par Johodp
`POST {userVerificationEndpoint}`

Headers:
- `Content-Type: application/json`
- `X-Johodp-Signature: <HMAC-SHA256>`
- `X-Johodp-Timestamp: <UTC ISO8601>`

Payload:
```json
{
  "requestId": "uuid",
  "tenantId": "acme-corp",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "timestamp": "2025-11-25T10:30:00Z"
}
```

La signature est calculée: `HMAC_SHA256(base64Secret, requestId + tenantId + email + timestamp)` (format canonique configurable).

### Réponse Attendue
- `200 OK` → Validation acceptée (application tierce créera l'utilisateur via `/api/users/register`)
- `4xx/5xx` → Considéré refusé / erreur; l'utilisateur reste en attente.

### Sécurité & Règles
- Timeout de validation: 5 minutes (RG-ONBOARD-03)
- Rejouer la notification en cas d'erreur réseau (stratégie de retry recommandée)
- Secret HMAC stocké chiffré côté tenant
- Journaliser les échecs de signature

### Bonnes Pratiques
1. Vérifier l'horodatage (anti-replay: tolérance ±5 min)
2. Normaliser email (lowercase) avant calcul signature
3. Implémenter circuit breaker si trop d'échecs

---

# 🔄 Refresh Token & Rotation

Caractéristiques (UC-08 / US-6.6):
- Durée de vie: 15 jours sliding window
- Usage unique: ancien refresh_token révoqué lors du renouvellement
- Rotation obligatoire: chaque réponse renvoie un nouveau token
- Révocation manuelle possible (future endpoint d'administration)

Erreurs possibles:
- `invalid_grant` (token expiré/révoqué)
- `unauthorized_client` (client sans droit refresh)

---

# 🛡️ Protected API Example

**GET** `/api/users/me` (US-6.7) nécessite header:
```
Authorization: Bearer <access_token>
```
Validations: signature, exp, iss, aud, scopes.

Réponse:
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "john.doe@example.com",
  "tenants": ["acme-corp"],
  "status": "Active"
}
```

---

## 🔄 Dynamic Client Aggregation

Lors de `/connect/authorize`, IdentityServer appelle `CustomClientStore.FindClientByIdAsync` (US-6.1 / UC-03) pour:
- Agréger `RedirectUris` depuis tous les tenants
- Agréger `AllowedCorsOrigins` depuis tous les tenants
- Dédupliquer entrées
- Retourner `null` si aucune redirect URI ou aucun tenant

---

## Changelog des Endpoints

### v4.0 (2025-11-25) - Multi-Tenant & OAuth Flows Complets
- ✅ Ajout endpoints Clients (CRUD)
- ✅ Ajout endpoints Tenants (CRUD + branding + localisation)
- ✅ Endpoints multi-tenant utilisateur (add/remove/list)
- ✅ Documentation `/connect/authorize` & `/connect/token` (authorization_code, refresh_token, client_credentials)
- ✅ Refresh token rotation & sécurité
- ✅ Webhook d'onboarding + signature HMAC
- ✅ Endpoint protégé `/api/users/me`
- ✅ Agrégation dynamique client documentée

### v3.0 (2025-11-24) - API Authentication Endpoints + CORS Migration

---

## Exemples de flux complet

### Scénario 1: Création et récupération d'un utilisateur

```bash
# 1. Enregistrer un nouvel utilisateur
curl -X POST https://localhost:5001/api/users/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "alice@example.com",
    "firstName": "Alice",
    "lastName": "Smith"
  }'

# Response:
# {
#   "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
#   "email": "alice@example.com"
# }

# 2. Récupérer l'utilisateur créé
curl -X GET https://localhost:5001/api/users/a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  -H "Accept: application/json"

# Response:
# {
#   "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
#   "email": "alice@example.com",
#   "firstName": "Alice",
#   "lastName": "Smith",
#   "emailConfirmed": false,
#   "isActive": true,
#   "createdAt": "2025-11-17T15:45:30Z"
# }
```
