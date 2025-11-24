# 🌐 CORS Configuration et Sécurité

## ⚠️ IMPORTANT: Limites de sécurité CORS

### CORS protège UNIQUEMENT les navigateurs web !

```
┌────────────────────────────────────────────────────────┐
│         CORS = Protection NAVIGATEUR uniquement        │
├────────────────────────────────────────────────────────┤
│                                                        │
│  ✅ CORS protège:                                      │
│     - Navigateurs (Chrome, Firefox, Safari, Edge)     │
│     - JavaScript (fetch, axios, XMLHttpRequest)       │
│     - Applications SPA (React, Angular, Vue)          │
│                                                        │
│  ❌ CORS NE protège PAS:                               │
│     - curl / wget / Postman / Insomnia                │
│     - Applications serveur (Node.js, Python, C#)      │
│     - Applications mobile natives (iOS, Android)      │
│     - Scripts backend / API-to-API calls              │
│                                                        │
└────────────────────────────────────────────────────────┘
```

## 📐 Architecture CORS dans Johodp

### Migration: Client → Tenant (Nov 2025)

**Avant:**
```
Client
  ├── ClientId
  ├── AllowedScopes
  └── ❌ AllowedCorsOrigins (ancien emplacement)
```

**Après:**
```
Client
  ├── ClientId
  ├── AllowedScopes
  └── AssociatedTenantIds (1:N)
        │
        └─→ Tenant
              ├── TenantId
              ├── AllowedReturnUrls (redirect URIs)
              └── ✅ AllowedCorsOrigins (nouvel emplacement)
```

### Pourquoi ce changement ?

✅ **Cohérence** - AllowedReturnUrls et AllowedCorsOrigins au même endroit
✅ **Multi-tenant** - Chaque tenant a ses propres origines CORS
✅ **Flexibilité** - Un client hérite des CORS de tous ses tenants
✅ **Maintenabilité** - Configuration centralisée par tenant

### Agrégation dynamique

```csharp
// Infrastructure/IdentityServer/CustomClientStore.cs
public Duende.IdentityServer.Models.Client MapToIdentityServerClient(
    Client client, 
    IEnumerable<Tenant> tenants)
{
    // Agrégation des CORS origins depuis TOUS les tenants associés
    var corsOrigins = tenants
        .SelectMany(t => t.AllowedCorsOrigins)
        .Distinct()
        .ToList();

    return new Duende.IdentityServer.Models.Client
    {
        ClientId = client.ClientName.Value,
        AllowedCorsOrigins = corsOrigins,
        // ...
    };
}
```

**Exemple:**
```
Client "my-spa-app" associé à:
  - Tenant "acme" → ["http://localhost:4200", "https://app.acme.com"]
  - Tenant "beta" → ["http://localhost:3000", "https://beta.acme.com"]

Résultat agrégé:
  AllowedCorsOrigins = [
    "http://localhost:4200",
    "https://app.acme.com",
    "http://localhost:3000",
    "https://beta.acme.com"
  ]
```

## 🔒 Vraie Sécurité vs CORS

### CORS est une COMMODITÉ, pas une SÉCURITÉ

```
┌─────────────────────────────────────────────────────────┐
│          Protection en couches (Defense in Depth)       │
├─────────────────────────────────────────────────────────┤
│ 1. CORS (navigateur)        → Commodité UX             │
│ 2. Authentication (OAuth2)  → Qui êtes-vous ?          │
│ 3. Authorization (Claims)   → Que pouvez-vous faire ?  │
│ 4. Rate Limiting            → Limite abus              │
│ 5. API Keys / Client Secret → Identification client    │
│ 6. IP Whitelist (optionnel) → Restriction géographique │
└─────────────────────────────────────────────────────────┘
```

### Exemple de contournement CORS

#### Scénario 1: Navigateur (CORS activé)

```javascript
// Frontend: http://evil.com essaie d'appeler l'API
fetch('https://api.johodp.com/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    email: 'victim@example.com',
    password: 'stolen-password'
  })
})
// ❌ ERROR: CORS policy: No 'Access-Control-Allow-Origin' header
// Requête bloquée par le navigateur
```

#### Scénario 2: curl (CORS ignoré)

```bash
# Attaquant utilise curl (hors navigateur)
curl -X POST https://api.johodp.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"victim@example.com","password":"stolen-password"}'

# ✅ SUCCESS: Retourne userId et cookie de session
# CORS n'a AUCUN effet car ce n'est pas un navigateur !
```

#### Scénario 3: Application serveur (CORS ignoré)

```csharp
// Application C# serveur (pas de CORS)
var client = new HttpClient();
var request = new {
    email = "victim@example.com",
    password = "stolen-password"
};

var response = await client.PostAsJsonAsync(
    "https://api.johodp.com/api/auth/login", 
    request);

// ✅ SUCCESS: Reçoit la réponse sans vérification CORS
```

### Solution: Defense in Depth

```
┌────────────────────────────────────────────────────────┐
│ Attaque → Défenses en couches → Protection            │
├────────────────────────────────────────────────────────┤
│                                                        │
│ curl malveillant                                      │
│   ├─ Bypass CORS ✓ (pas un navigateur)                │
│   ├─ Rate Limiting → Bloqué après 10 tentatives/min   │
│   ├─ Authentication → Needs valid password             │
│   └─ Authorization → Needs valid token                 │
│                                                        │
│ Bot serveur                                           │
│   ├─ Bypass CORS ✓                                     │
│   ├─ Client Secret → Required for token endpoint      │
│   ├─ API Key → Required for registration endpoint     │
│   └─ IP Whitelist → Only approved IPs                 │
│                                                        │
│ Navigateur compromis                                  │
│   ├─ CORS OK ✓ (origine autorisée)                    │
│   ├─ Authentication → Stolen credentials detected     │
│   ├─ MFA → Second factor required                     │
│   └─ Anomaly Detection → Unusual location/device      │
│                                                        │
└────────────────────────────────────────────────────────┘
```

## 🛠️ Configuration CORS

### Créer un Tenant avec CORS

```http
POST /api/tenant
Content-Type: application/json

{
  "name": "acme",
  "displayName": "ACME Corporation",
  "allowedReturnUrls": [
    "http://localhost:4200/callback",
    "https://app.acme.com/callback"
  ],
  "allowedCorsOrigins": [
    "http://localhost:4200",
    "https://app.acme.com"
  ],
  "clientId": "my-spa-app"
}
```

### Validation des CORS Origins

```csharp
// Domain/Tenants/Aggregates/Tenant.cs
public void AddAllowedCorsOrigin(string origin)
{
    // 1. Vérifier non vide
    if (string.IsNullOrWhiteSpace(origin))
        throw new ArgumentException("CORS origin cannot be empty");

    // 2. Valider format URI
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        throw new ArgumentException($"Invalid CORS origin format: {origin}");

    // 3. Interdire les paths (autorité uniquement)
    if (!string.IsNullOrEmpty(uri.PathAndQuery) && uri.PathAndQuery != "/")
        throw new ArgumentException(
            $"CORS origin must be authority only (no path): {origin}");

    // 4. Normaliser: https://example.com (pas de trailing slash)
    var normalizedOrigin = $"{uri.Scheme}://{uri.Authority}";
    
    // 5. Ajouter si pas déjà présent
    if (!_allowedCorsOrigins.Contains(normalizedOrigin))
        _allowedCorsOrigins.Add(normalizedOrigin);
}
```

**Exemples:**
```csharp
// ✅ Valides
tenant.AddAllowedCorsOrigin("http://localhost:4200");
tenant.AddAllowedCorsOrigin("https://app.acme.com");
tenant.AddAllowedCorsOrigin("https://api.acme.com:8443");

// ❌ Invalides
tenant.AddAllowedCorsOrigin("http://localhost:4200/callback");  // Path interdit
tenant.AddAllowedCorsOrigin("not-a-url");                       // Format invalide
tenant.AddAllowedCorsOrigin("");                                // Vide
```

### Mettre à jour les CORS

```http
PUT /api/tenant/{tenantId}
Content-Type: application/json

{
  "displayName": "ACME Corporation",
  "allowedReturnUrls": [
    "http://localhost:4200/callback",
    "https://app.acme.com/callback",
    "https://mobile.acme.com/callback"
  ],
  "allowedCorsOrigins": [
    "http://localhost:4200",
    "https://app.acme.com",
    "https://mobile.acme.com"
  ],
  "isActive": true
}
```

**⚠️ Note:** UpdateTenantCommand REMPLACE toutes les CORS origins (pas de merge)

## 🗄️ Migration Base de Données

### Script de migration (20251124115839_MoveCorsOriginsFromClientToTenant)

```sql
-- Étape 1: Ajouter colonne nullable
ALTER TABLE tenants 
ADD COLUMN "AllowedCorsOrigins" jsonb NULL;

-- Étape 2: Définir valeur par défaut pour lignes existantes
UPDATE tenants 
SET "AllowedCorsOrigins" = '[]'::jsonb 
WHERE "AllowedCorsOrigins" IS NULL;

-- Étape 3: Rendre NOT NULL
ALTER TABLE tenants 
ALTER COLUMN "AllowedCorsOrigins" SET NOT NULL;

-- Étape 4: Supprimer ancienne colonne (si elle existe)
ALTER TABLE clients 
DROP COLUMN IF EXISTS "AllowedCorsOrigins";
```

### Appliquer la migration

```powershell
# Windows
cd src/Johodp.Infrastructure
dotnet ef database update --startup-project ../Johodp.Api/Johodp.Api.csproj --context JohodpDbContext

# Linux/Mac
cd src/Johodp.Infrastructure
dotnet ef database update --startup-project ../Johodp.Api/Johodp.Api.csproj --context JohodpDbContext
```

## 📊 Impact du changement

### Fichiers modifiés

**Domain Layer:**
- ✅ `Domain/Clients/Aggregates/Client.cs` - AllowedCorsOrigins supprimé
- ✅ `Domain/Tenants/Aggregates/Tenant.cs` - AllowedCorsOrigins ajouté avec validation

**Application Layer:**
- ✅ `Application/Clients/DTOs/ClientDto.cs` - AllowedCorsOrigins supprimé
- ✅ `Application/Clients/DTOs/CreateClientDto.cs` - AllowedCorsOrigins supprimé
- ✅ `Application/Clients/DTOs/UpdateClientDto.cs` - AllowedCorsOrigins supprimé
- ✅ `Application/Clients/Commands/CreateClientCommand.cs` - Pas de gestion CORS
- ✅ `Application/Clients/Commands/UpdateClientCommand.cs` - Pas de gestion CORS
- ✅ `Application/Clients/Queries/ClientQueries.cs` - MapToDto sans CORS
- ✅ `Application/Tenants/DTOs/TenantDto.cs` - AllowedCorsOrigins ajouté
- ✅ `Application/Tenants/DTOs/CreateTenantDto.cs` - AllowedCorsOrigins ajouté
- ✅ `Application/Tenants/DTOs/UpdateTenantDto.cs` - AllowedCorsOrigins ajouté
- ✅ `Application/Tenants/Commands/CreateTenantCommand.cs` - Gestion CORS avec foreach
- ✅ `Application/Tenants/Commands/UpdateTenantCommand.cs` - Remplacement total CORS
- ✅ `Application/Tenants/Queries/TenantQueries.cs` - MapToDto avec CORS

**Infrastructure Layer:**
- ✅ `Infrastructure/IdentityServer/CustomClientStore.cs` - Agrégation CORS depuis tenants
- ✅ `Infrastructure/Persistence/Configurations/ClientConfiguration.cs` - Mapping CORS supprimé
- ✅ `Infrastructure/Persistence/Configurations/TenantConfiguration.cs` - Mapping CORS ajouté (jsonb)

**Migrations:**
- ✅ `Infrastructure/Migrations/20251124115839_MoveCorsOriginsFromClientToTenant.cs`

**Documentation:**
- ✅ `httpTest/admin-operations.http` - Exemples mis à jour
- ✅ `httpTest/pkceconnection.http` - Commentaires mis à jour
- ✅ `httpTest/api-auth-endpoints.http` - Nouveau fichier créé
- ✅ `ARCHITECTURE.md` - Section CORS ajoutée
- ✅ `API_ENDPOINTS.md` - Section CORS ajoutée
- ✅ `QUICKSTART.md` - Configuration CORS ajoutée
- ✅ `USE_CASES.md` - Avertissements sécurité ajoutés
- ✅ `USER_STORIES.md` - Notes sécurité ajoutées
- ✅ `CORS_SECURITY.md` - Ce fichier (documentation complète)

## 🧪 Tests

### Test CORS dans navigateur

```javascript
// Page web sur http://localhost:4200
fetch('https://api.johodp.com/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  credentials: 'include',
  body: JSON.stringify({
    email: 'test@example.com',
    password: 'P@ssw0rd123!'
  })
})
.then(response => response.json())
.then(data => console.log('✅ Login OK:', data))
.catch(error => console.error('❌ CORS Error:', error));
```

### Test avec curl (contournement CORS)

```bash
# CORS n'a AUCUN effet sur curl
curl -X POST https://api.johodp.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "P@ssw0rd123!"
  }' \
  -v

# Succès même si origine non autorisée
# → Démontre que CORS ≠ sécurité
```

### Test avec Postman (contournement CORS)

```
POST https://api.johodp.com/api/auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "P@ssw0rd123!"
}

// ✅ Succès - Postman ignore CORS
```

## 📚 Ressources

### Standards Web
- [MDN: CORS](https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS)
- [W3C: Cross-Origin Resource Sharing](https://www.w3.org/TR/cors/)

### ASP.NET Core
- [Microsoft: Enable CORS in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/security/cors)
- [Microsoft: CORS Middleware](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware/)

### Sécurité
- [OWASP: CSRF Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html)
- [OWASP: API Security Top 10](https://owasp.org/www-project-api-security/)

### IdentityServer
- [Duende IdentityServer: CORS](https://docs.duendesoftware.com/identityserver/v6/fundamentals/cors/)

## ❓ FAQ

### Q: Pourquoi CORS n'empêche pas les requêtes curl ?
**R:** CORS est une fonctionnalité de SÉCURITÉ DU NAVIGATEUR. curl, Postman, applications serveur ne sont pas des navigateurs et ignorent totalement CORS.

### Q: Comment bloquer vraiment les requêtes non autorisées ?
**R:** Utilisez Authentication (OAuth2/JWT), Authorization (Claims), Rate Limiting, API Keys, et IP Whitelist. CORS ne suffit JAMAIS.

### Q: Pourquoi déplacer CORS de Client vers Tenant ?
**R:** Cohérence architecturale. Les redirect URIs et CORS origins sont tous deux des configurations frontend et doivent être au même endroit (Tenant).

### Q: Un client peut-il avoir plusieurs tenants avec des CORS différents ?
**R:** Oui ! Les CORS origins sont agrégées dynamiquement depuis TOUS les tenants associés au client.

### Q: CORS protège-t-il contre les attaques CSRF ?
**R:** Non. CORS empêche la LECTURE des réponses cross-origin, mais pas l'ENVOI de requêtes. Utilisez des tokens anti-CSRF.

### Q: Dois-je configurer CORS pour mon application mobile ?
**R:** Non. Les applications mobile natives (iOS/Android) ignorent CORS. Utilisez OAuth2 + PKCE pour la sécurité.

### Q: Que se passe-t-il si j'oublie de configurer AllowedCorsOrigins ?
**R:** Les requêtes depuis navigateurs seront bloquées, mais curl/Postman/apps serveur fonctionneront normalement. Ce n'est qu'un problème d'UX.

---

**Dernière mise à jour:** 24 novembre 2025  
**Version:** 3.0 (Migration CORS Client → Tenant)
