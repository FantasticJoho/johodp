# Guide d'installation - Johodp Identity Provider

Guide d'installation complet pour déployer et configurer Johodp IDP avec toutes ses fonctionnalités : OAuth2/OIDC, multi-tenancy, MFA natif, logging enrichi et intégration IdP externe.

## 📋 Table des matières

- [Prérequis](#prérequis)
- [Installation PostgreSQL](#installation-postgresql)
- [Configuration de la base de données](#configuration-de-la-base-de-données)
- [Installation de l'application](#installation-de-lapplication)
- [Configuration IdentityServer](#configuration-identityserver)
- [Configuration MFA natif](#configuration-mfa-natif)
- [Configuration des enrichers Serilog](#configuration-des-enrichers-serilog)
- [Configuration OAuth2 Client Credentials](#configuration-oauth2-client-credentials)
- [Configuration IdP externe](#configuration-idp-externe)
- [Variables d'environnement](#variables-denvironnement)
- [Vérification de l'installation](#vérification-de-linstallation)
- [Troubleshooting](#troubleshooting)

## 🔧 Prérequis

### Logiciels requis

| Composant  | Version            | Description                  |
| ---------- | ------------------ | ---------------------------- |
| .NET SDK   | 8.0+               | Framework pour l'application |
| PostgreSQL | 12+                | Base de données principale   |
| Docker     | 20.10+ (optionnel) | Pour PostgreSQL conteneurisé |
| Git        | 2.x                | Contrôle de version          |

### Compétences recommandées

- Connaissance de base de .NET/C#
- Compréhension d'OAuth2/OIDC
- Familiarité avec PostgreSQL et Entity Framework
- Notions de sécurité (JWT, PKCE, TOTP)

### Ports réseau requis

| Port | Service    | Protocole |
| ---- | ---------- | --------- |
| 5000 | API HTTP   | HTTP      |
| 5001 | API HTTPS  | HTTPS     |
| 5432 | PostgreSQL | TCP       |

## 📦 Installation PostgreSQL

### Option 1 : Docker (recommandé pour développement)

```bash
# Démarrer PostgreSQL avec Docker Compose (configuration incluse)
docker-compose up -d

# Vérifier que PostgreSQL est en cours d'exécution
docker ps | grep postgres
```

Le fichier `docker-compose.yml` configure automatiquement :
- Base de données : `johodp`
- Utilisateur : `postgres`
- Mot de passe : `password`
- Port : `5432`

### Option 2 : Installation native

#### Windows

```powershell
# Télécharger depuis https://www.postgresql.org/download/windows/
# Ou utiliser Chocolatey
choco install postgresql

# Créer la base de données
psql -U postgres
CREATE DATABASE johodp;
\q
```

#### Linux (Ubuntu/Debian)

```bash
# Installer PostgreSQL
sudo apt update
sudo apt install postgresql postgresql-contrib

# Créer la base de données
sudo -u postgres psql
CREATE DATABASE johodp;
\q
```

#### macOS

```bash
# Avec Homebrew
brew install postgresql@15

# Démarrer le service
brew services start postgresql@15

# Créer la base de données
psql postgres
CREATE DATABASE johodp;
\q
```

## 🗄️ Configuration de la base de données

### 1. Configurer la chaîne de connexion

Éditer `src/Johodp.Api/appsettings.json` :

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=johodp;Username=postgres;Password=password"
  }
}
```

**Production** : Utiliser `appsettings.Production.json` avec des variables d'environnement :

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD};SSL Mode=Require"
  }
}
```

### 2. Restaurer les packages NuGet

```bash
cd c:\Users\jonat\repo\johodp
dotnet restore
```

### 3. Appliquer les migrations

#### PowerShell (Windows)

```powershell
# Utiliser le script fourni
.\init-db.ps1

# Ou manuellement
dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api
```

#### Bash/Shell (Linux/macOS)

```bash
# Utiliser le script fourni
./init-db.sh

# Ou manuellement
dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api
```

### 4. Vérifier la structure de la base

```sql
-- Se connecter à PostgreSQL
psql -U postgres -d johodp

-- Lister les tables créées
\dt

-- Tables attendues :
-- - AspNetUsers (utilisateurs ASP.NET Identity)
-- - AspNetRoles (rôles)
-- - AspNetUserRoles (association utilisateurs-rôles)
-- - Clients (clients OAuth2/OIDC)
-- - ApiScopes (scopes API)
-- - IdentityResources (scopes identité)
-- - PersistedGrants (tokens, authorization codes, refresh tokens)
-- - Keys (clés de signature IdentityServer)
-- - DeviceCodes (flow Device Authorization)

-- Vérifier la configuration IdentityServer
SELECT * FROM "Clients" WHERE "ClientId" = 'johodp-spa';
```

## 🚀 Installation de l'application

### 1. Cloner le dépôt (si ce n'est pas déjà fait)

```bash
git clone https://github.com/votre-org/johodp.git
cd johodp
```

### 2. Build de l'application

```bash
# Build en mode Release
dotnet build -c Release

# Ou utiliser la tâche VS Code
# Ctrl+Shift+B → "dotnet: build"
```

### 3. Exécuter l'application

#### Mode développement

```bash
# Lancer avec rechargement automatique (watch)
dotnet watch run --project src/Johodp.Api

# Ou utiliser la tâche VS Code
# Ctrl+Shift+P → "Tasks: Run Task" → "dotnet: watch"
```

#### Mode production

```bash
# Build et exécution
dotnet run --project src/Johodp.Api -c Release

# Ou avec profil de lancement spécifique
dotnet run --project src/Johodp.Api --launch-profile https
```

L'application démarre sur :
- HTTP : `http://localhost:5000`
- HTTPS : `https://localhost:5001`

### 4. Vérifier le démarrage

Ouvrir un navigateur et accéder à :

```
https://localhost:5001/.well-known/openid-configuration
```

Vous devriez voir le document de découverte OIDC avec tous les endpoints IdentityServer.

## 🔐 Configuration IdentityServer

### 1. Clés de signature (Certificats X.509)

#### Développement

En développement, IdentityServer génère automatiquement une clé temporaire au démarrage. Aucune action requise.

#### Production

##### Étape 1 : Générer le certificat de signature

**Option A : Avec dotnet dev-certs (rapide, pour staging)**
```powershell
# Créer le dossier des clés
mkdir src/Johodp.Api/keys

# Générer le certificat
dotnet dev-certs https -ep src/Johodp.Api/keys/signing-key.pfx -p "VotreMotDePasseSecurise123!"

# Vérifier la création
dir src/Johodp.Api/keys/signing-key.pfx
```

**Option B : Avec OpenSSL (recommandé pour production)**
```bash
# Générer la clé privée et le certificat
openssl req -x509 -newkey rsa:4096 \
    -keyout temp-key.pem \
    -out temp-cert.pem \
    -days 365 \
    -nodes \
    -subj "/CN=Johodp IdentityServer/O=VotreOrganisation/C=FR"

# Convertir en format PFX
openssl pkcs12 -export \
    -out src/Johodp.Api/keys/signing-key.pfx \
    -inkey temp-key.pem \
    -in temp-cert.pem \
    -passout pass:VotreMotDePasseSecurise123!

# Nettoyer les fichiers temporaires
rm temp-key.pem temp-cert.pem
```

##### Étape 2 : Configurer les permissions

```powershell
# Windows - Restreindre l'accès au fichier
icacls src/Johodp.Api/keys/signing-key.pfx /inheritance:r
icacls src/Johodp.Api/keys/signing-key.pfx /grant:r "$env:USERNAME:(R)"
```

```bash
# Linux/macOS - Restreindre l'accès au fichier
chmod 600 src/Johodp.Api/keys/signing-key.pfx
```

##### Étape 3 : Configurer l'application

**Créer/Modifier `appsettings.Production.json` :**
```json
{
  "IdentityServer": {
    "SigningMethod": "Certificate",
    "SigningKeyPath": "keys/signing-key.pfx",
    "SigningKeyPassword": "VotreMotDePasseSecurise123!"
  }
}
```

⚠️ **Important** : En production, ne stockez JAMAIS le mot de passe en clair !

**Utiliser une variable d'environnement :**
```powershell
# Windows
$env:IDENTITYSERVER_SIGNING_PASSWORD="VotreMotDePasseSecurise123!"

# Linux/macOS
export IDENTITYSERVER_SIGNING_PASSWORD="VotreMotDePasseSecurise123!"
```

**Puis dans `appsettings.Production.json` :**
```json
{
  "IdentityServer": {
    "SigningMethod": "Certificate",
    "SigningKeyPath": "keys/signing-key.pfx",
    "SigningKeyPassword": ""
  }
}
```

Le mot de passe sera lu depuis la variable d'environnement `IDENTITYSERVER_SIGNING_PASSWORD`.

##### Étape 4 : Exclure du contrôle de version

**Vérifier que `.gitignore` contient :**
```
# Signing keys
**/keys/*.pfx
**/keys/*.jwk
```

##### Étape 5 : Tester la configuration

```powershell
# Démarrer l'application
dotnet run --project src/Johodp.Api --launch-profile https

# Dans les logs, vous devriez voir :
# "Using certificate signing credential from: keys/signing-key.pfx"
```

**Vérifier le endpoint de découverte :**
```powershell
curl https://localhost:5001/.well-known/openid-configuration
```

Le JSON retourné doit contenir `jwks_uri` pointant vers les clés publiques.

**Rotation automatique** : Voir `CERTIFICATE_ROTATION.md` pour la rotation sans coupure.

### 2. Configuration des clients OAuth2

Les clients sont configurés dans `src/Johodp.Infrastructure/IdentityServer/IdentityServerConfig.cs`.

#### Clients par défaut

| Client ID                   | Type         | Grant Type                | Usage                     |
| --------------------------- | ------------ | ------------------------- | ------------------------- |
| `johodp-spa`                | Public       | Authorization Code + PKCE | SPA (React, Angular, Vue) |
| `swagger-ui`                | Public       | Authorization Code + PKCE | Documentation Swagger     |
| `johodp-client-credentials` | Confidentiel | Client Credentials        | Services backend (M2M)    |

#### Ajouter un nouveau client

```csharp
// src/Johodp.Infrastructure/IdentityServer/IdentityServerConfig.cs

new Duende.IdentityServer.Models.Client
{
    ClientId = "my-spa",
    ClientName = "My SPA Application",
    
    AllowedGrantTypes = GrantTypes.Code,
    RequirePkce = true,
    RequireClientSecret = false,
    
    RedirectUris = { "http://localhost:4200/callback" },
    PostLogoutRedirectUris = { "http://localhost:4200" },
    AllowedCorsOrigins = { "http://localhost:4200" },
    
    AllowedScopes = {
        IdentityServerConstants.StandardScopes.OpenId,
        IdentityServerConstants.StandardScopes.Profile,
        IdentityServerConstants.StandardScopes.Email,
        "johodp.identity",
        "johodp.api"
    },
    
    AllowOfflineAccess = true, // Refresh tokens
    AccessTokenLifetime = 3600, // 1 heure
    IdentityTokenLifetime = 300, // 5 minutes
    
    RequireConsent = false // Désactiver pour dev
}
```

### 3. Configuration des scopes

#### Identity Scopes (informations utilisateur)

```csharp
new IdentityResources.OpenId(),
new IdentityResources.Profile(),
new IdentityResources.Email(),
new IdentityResource
{
    Name = "johodp.identity",
    DisplayName = "Johodp Identity Claims",
    UserClaims = { "tenant_id", "role", "permission" }
}
```

#### API Scopes (accès aux ressources)

```csharp
new ApiScope("johodp.api", "Johodp API Access")
{
    UserClaims = { "tenant_id", "role", "permission" }
}
```

### 4. Configuration CORS

CORS est géré au niveau tenant. Par défaut, `http://localhost:4200` est autorisé en développement.

**Important** : CORS protège uniquement les navigateurs web, pas les requêtes serveur ou API-to-API.

## 🛡️ Configuration MFA natif

### 1. Vue d'ensemble

Johodp utilise **ASP.NET Core Identity native 2FA** avec TOTP (Time-based One-Time Password) compatible Google Authenticator, Microsoft Authenticator, Authy, etc.

### 2. Activation MFA pour un utilisateur

#### Étape 1 : Provisionner l'authentificateur

```bash
POST /api/auth/mfa/provision
Authorization: Bearer {access_token}

# Réponse
{
  "qrCodeUri": "data:image/png;base64,...",
  "manualEntryKey": "JBSWY3DPEHPK3PXP"
}
```

Le QR code peut être scanné avec une application d'authentification.

#### Étape 2 : Confirmer le code TOTP

```bash
POST /api/auth/mfa/confirm
Authorization: Bearer {access_token}
Content-Type: application/json

{
  "code": "123456"
}

# Réponse
{
  "recoveryCodes": [
    "ab12cd34ef56",
    "gh78ij90kl12",
    ...
  ]
}
```

**Important** : Sauvegarder les codes de récupération (à usage unique).

### 3. Authentification avec MFA

Le flux de connexion devient en deux étapes :

#### Étape 1 : Vérifier le mot de passe

```bash
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "P@ssw0rd!"
}

# Réponse si MFA activé
{
  "requiresTwoFactor": true,
  "message": "MFA code required"
}
```

#### Étape 2 : Vérifier le code TOTP

```bash
POST /api/auth/login/mfa
Content-Type: application/json

{
  "code": "123456",
  "rememberMe": false
}

# Réponse (succès)
{
  "success": true,
  "message": "Login successful",
  "userId": "guid"
}
```

### 4. Désactivation MFA

```bash
POST /api/auth/mfa/disable
Authorization: Bearer {access_token}

# Réponse
{
  "success": true,
  "message": "MFA disabled"
}
```

### 5. Régénération des codes de récupération

```bash
POST /api/auth/mfa/regenerate-recovery-codes
Authorization: Bearer {access_token}

# Réponse
{
  "recoveryCodes": [
    "mn34op56qr78",
    "st90uv12wx34",
    ...
  ]
}
```

### 6. Configuration ASP.NET Identity

La configuration MFA est dans `Program.cs` :

```csharp
builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
{
    // Politique de mot de passe
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    
    // Lockout
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    
    // Tokens
    options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
})
.AddEntityFrameworkStores<JohodpDbContext>()
.AddDefaultTokenProviders();
```

## 📊 Configuration des enrichers Serilog

### 1. Vue d'ensemble

Johodp utilise un enricher personnalisé (`TenantClientEnricher`) pour ajouter automatiquement `TenantId` et `ClientId` à tous les logs.

### 2. Extraction du TenantId

Le `TenantId` est extrait avec la priorité suivante :

1. **acr_values** : `acr_values=tenant:xxx` (query param OIDC)
2. **Claim** : `tenant_id` claim dans le JWT
3. **Query param** : `?tenant=xxx`
4. **Header** : `X-Tenant-Id: xxx`

Exemple d'URL avec tenant :

```
https://localhost:5001/connect/authorize?...&acr_values=tenant:acme-corp
```

### 3. Extraction du ClientId

Le `ClientId` est extrait avec la priorité suivante :

1. **Claim** : `client_id` claim dans le JWT
2. **Query param** : `?client_id=xxx`
3. **Header** : `X-Client-Id: xxx`

### 4. Configuration Serilog

La configuration est dans `Program.cs` :

```csharp
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .Enrich.WithProperty("Application", "Johodp.Api")
        .Enrich.With<TenantClientEnricher>()
        .WriteTo.Console(outputTemplate: 
            "[{Timestamp:HH:mm:ss} {Level:u3}] {TenantId} {ClientId} {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: "logs/johodp-.log",
            rollingInterval: RollingInterval.Day,
            outputTemplate: 
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {TenantId} {ClientId} {Message:lj}{NewLine}{Exception}");
});

// Enregistrer HttpContextAccessor (requis pour l'enricher)
builder.Services.AddHttpContextAccessor();
```

### 5. Exemple de logs enrichis

```
[15:42:31 INF] acme-corp johodp-spa User user@example.com authenticated successfully
[15:42:32 INF] acme-corp johodp-spa Provisioning MFA for user 1bb71afc-e622-42f4-b3fd-df4956ebb3eb
[15:42:33 ERR] contoso johodp-api Tenant mismatch: User tenant is contoso but requested acme
```

### 6. Documentation complète

Voir `LOGGING_ENRICHERS.md` pour plus de détails sur l'implémentation et les cas d'usage.

## 🔗 Configuration OAuth2 Client Credentials

### 1. Client interne (Johodp IdP)

Pour les communications sécurisées depuis l'application tierce vers Johodp :

#### Configuration du client

```csharp
// src/Johodp.Infrastructure/IdentityServer/IdentityServerConfig.cs

new Client
{
    ClientId = "third-party-app-client",
    ClientName = "Third Party Application",
    
    AllowedGrantTypes = GrantTypes.ClientCredentials,
    ClientSecrets = { new Secret("your-secure-secret".Sha256()) },
    
    AllowedScopes = { "johodp.api" },
    
    AccessTokenLifetime = 3600, // 1 heure
    
    Claims = {
        new ClientClaim("client_type", "third_party_app"),
        new ClientClaim("tenant_id", "*") // Ou tenant spécifique
    }
}
```

#### Obtenir un token

```bash
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
&client_id=third-party-app-client
&client_secret=your-secure-secret
&scope=johodp.api

# Réponse
{
  "access_token": "eyJhbGci...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "scope": "johodp.api"
}
```

#### Utiliser le token

```bash
POST /api/users/register
Authorization: Bearer eyJhbGci...
Content-Type: application/json

{
  "email": "newuser@example.com",
  "firstName": "Jean",
  "lastName": "Dupont",
  "password": "TempP@ssw0rd123!",
  "tenantId": "acme"
}
```

### 2. Caching des tokens

Pour améliorer les performances, implémenter un cache de tokens :

```csharp
public class TokenCacheService
{
    private readonly IDistributedCache _cache;
    private readonly HttpClient _httpClient;
    
    public async Task<string> GetAccessTokenAsync()
    {
        var cacheKey = "oauth2:client_credentials:token";
        
        // Vérifier le cache
        var cachedToken = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedToken))
            return cachedToken;
        
        // Obtenir un nouveau token
        var response = await _httpClient.PostAsync("/connect/token", ...);
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        
        // Mettre en cache (avec marge de sécurité de 5 minutes)
        var cacheExpiry = TimeSpan.FromSeconds(tokenResponse.ExpiresIn - 300);
        await _cache.SetStringAsync(cacheKey, tokenResponse.AccessToken, 
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = cacheExpiry });
        
        return tokenResponse.AccessToken;
    }
}
```

Configuration Redis (recommandé pour production) :

```json
{
  "Redis": {
    "Configuration": "localhost:6379",
    "InstanceName": "johodp:"
  }
}
```

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:Configuration"];
    options.InstanceName = builder.Configuration["Redis:InstanceName"];
});
```

## 🌐 Configuration IdP externe

### 1. Vue d'ensemble

Pour protéger les webhooks envoyés par Johodp vers une application tierce, utiliser un **IdP externe** (pas Johodp lui-même).

### 2. Architecture

```
┌──────────────┐                           ┌──────────────┐
│  Johodp IDP  │                           │ External IdP │
│              │                           │ (Azure AD,   │
│              │  1. Request token         │  Keycloak,   │
│              │ ─────────────────────────>│  Auth0...)   │
│              │                           │              │
│              │  2. access_token          │              │
│              │ <─────────────────────────│              │
│              │                           └──────────────┘
│              │
│              │  3. POST /webhooks/user-registered
│              │     Authorization: Bearer {token}
│              │ ─────────────────────────────────────────>
│              │                                          ┌───────────────┐
│              │  4. Validate JWT signature               │ Third Party   │
│              │    Verify: iss, aud, exp, scope          │ Application   │
│              │ <─────────────────────────────────────────│               │
│              │                                          └───────────────┘
└──────────────┘
```

### 3. Configuration dans Johodp

Ajouter la configuration dans `appsettings.json` :

```json
{
  "ExternalIdP": {
    "Authority": "https://external-idp.example.com",
    "ClientId": "johodp-webhook-sender",
    "ClientSecret": "your-external-idp-client-secret",
    "Scope": "webhooks.send",
    "TokenEndpoint": "https://external-idp.example.com/oauth2/token",
    "Audience": "third-party-api"
  }
}
```

### 4. Implémentation du service de notification

```csharp
public class NotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration;
    
    public async Task SendUserRegisteredNotificationAsync(User user)
    {
        // Obtenir un token depuis l'IdP externe
        var accessToken = await GetExternalIdpTokenAsync();
        
        // Envoyer le webhook
        var request = new HttpRequestMessage(HttpMethod.Post, 
            "https://third-party-app.example.com/api/webhooks/user-registered");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            userId = user.Id,
            email = user.Email.Value,
            firstName = user.FirstName,
            lastName = user.LastName,
            tenantId = user.TenantId,
            registeredAt = user.CreatedAt
        });
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
    
    private async Task<string> GetExternalIdpTokenAsync()
    {
        var cacheKey = "external_idp:token";
        
        // Vérifier le cache
        var cachedToken = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedToken))
            return cachedToken;
        
        // Obtenir un nouveau token
        var config = _configuration.GetSection("ExternalIdP");
        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, config["TokenEndpoint"]);
        tokenRequest.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", config["ClientId"]),
            new KeyValuePair<string, string>("client_secret", config["ClientSecret"]),
            new KeyValuePair<string, string>("scope", config["Scope"])
        });
        
        var response = await _httpClient.SendAsync(tokenRequest);
        response.EnsureSuccessStatusCode();
        
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        
        // Mettre en cache
        var cacheExpiry = TimeSpan.FromSeconds(tokenResponse.ExpiresIn - 300);
        await _cache.SetStringAsync(cacheKey, tokenResponse.AccessToken, 
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = cacheExpiry });
        
        return tokenResponse.AccessToken;
    }
}
```

### 5. Retry policy avec Polly

```csharp
builder.Services.AddHttpClient<NotificationService>()
    .AddTransientHttpErrorPolicy(policyBuilder => 
        policyBuilder.WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Log.Warning("Webhook retry {RetryAttempt} after {Delay}ms", retryAttempt, timespan.TotalMilliseconds);
            }))
    .AddTransientHttpErrorPolicy(policyBuilder => 
        policyBuilder.CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromMinutes(1)));
```

### 6. Dead-letter queue

En cas d'échec après tous les retries, enregistrer dans une queue :

```csharp
public class DeadLetterQueue
{
    private readonly IDistributedCache _cache;
    
    public async Task EnqueueFailedWebhookAsync(string eventType, object payload, Exception error)
    {
        var key = $"dlq:webhook:{Guid.NewGuid()}";
        var entry = new
        {
            EventType = eventType,
            Payload = payload,
            Error = error.Message,
            StackTrace = error.StackTrace,
            Timestamp = DateTime.UtcNow,
            RetryCount = 0
        };
        
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(entry));
    }
}
```

Pour retraiter les messages en échec, créer une tâche planifiée (Hangfire, Quartz.NET) :

```csharp
public class RetryDeadLetterQueueJob
{
    public async Task ExecuteAsync()
    {
        // Récupérer les messages DLQ
        // Retenter l'envoi
        // Si succès, supprimer de la queue
        // Sinon, incrémenter le compteur de retry
    }
}
```

## 🔧 Variables d'environnement

### Configuration Docker

Créer un fichier `.env` :

```env
# Base de données
DB_HOST=postgres
DB_PORT=5432
DB_NAME=johodp
DB_USER=postgres
DB_PASSWORD=secure_password_here

# Application
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=https://+:443;http://+:80
ASPNETCORE_Kestrel__Certificates__Default__Path=/app/certs/johodp.pfx
ASPNETCORE_Kestrel__Certificates__Default__Password=cert_password_here

# Redis (pour cache distribué)
REDIS_CONNECTION=redis:6379
REDIS_INSTANCE_NAME=johodp:

# External IdP
EXTERNAL_IDP_AUTHORITY=https://external-idp.example.com
EXTERNAL_IDP_CLIENT_ID=johodp-webhook-sender
EXTERNAL_IDP_CLIENT_SECRET=external_secret_here
EXTERNAL_IDP_SCOPE=webhooks.send

# Logging
SERILOG_MINIMUM_LEVEL=Information
SERILOG_SEQ_URL=http://seq:5341
SERILOG_SEQ_API_KEY=seq_api_key_here
```

### Configuration Kubernetes

Créer des Secrets :

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: johodp-secrets
type: Opaque
stringData:
  db-password: secure_password_here
  external-idp-secret: external_secret_here
  cert-password: cert_password_here
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: johodp-config
data:
  DB_HOST: "postgres-service"
  DB_PORT: "5432"
  DB_NAME: "johodp"
  DB_USER: "postgres"
  REDIS_CONNECTION: "redis-service:6379"
  EXTERNAL_IDP_AUTHORITY: "https://external-idp.example.com"
  EXTERNAL_IDP_CLIENT_ID: "johodp-webhook-sender"
  EXTERNAL_IDP_SCOPE: "webhooks.send"
```

Référencer dans le Deployment :

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: johodp-api
spec:
  template:
    spec:
      containers:
      - name: api
        image: johodp/api:latest
        envFrom:
        - configMapRef:
            name: johodp-config
        env:
        - name: DB_PASSWORD
          valueFrom:
            secretKeyRef:
              name: johodp-secrets
              key: db-password
        - name: EXTERNAL_IDP_CLIENT_SECRET
          valueFrom:
            secretKeyRef:
              name: johodp-secrets
              key: external-idp-secret
```

## ✅ Vérification de l'installation

### 1. Health checks

Johodp inclut des health checks pour vérifier l'état du système :

```bash
# Health check basique
curl http://localhost:5000/health

# Health check détaillé (UI)
curl http://localhost:5000/health-ui

# Réponse attendue
{
  "status": "Healthy",
  "checks": [
    {
      "name": "PostgreSQL",
      "status": "Healthy",
      "duration": "00:00:00.123"
    },
    {
      "name": "IdentityServer",
      "status": "Healthy",
      "duration": "00:00:00.045"
    },
    {
      "name": "Redis",
      "status": "Healthy",
      "duration": "00:00:00.012"
    }
  ],
  "totalDuration": "00:00:00.180"
}
```

### 2. Vérifier IdentityServer

```bash
# Document de découverte OIDC
curl https://localhost:5001/.well-known/openid-configuration

# Vérifier les endpoints
{
  "issuer": "https://localhost:5001",
  "authorization_endpoint": "https://localhost:5001/connect/authorize",
  "token_endpoint": "https://localhost:5001/connect/token",
  "userinfo_endpoint": "https://localhost:5001/connect/userinfo",
  "end_session_endpoint": "https://localhost:5001/connect/endsession",
  "jwks_uri": "https://localhost:5001/.well-known/openid-configuration/jwks",
  "grant_types_supported": [
    "authorization_code",
    "client_credentials",
    "refresh_token"
  ],
  "code_challenge_methods_supported": [
    "S256"
  ]
}
```

### 3. Test de connexion

```bash
# Créer un utilisateur
curl -X POST http://localhost:5000/api/users/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "firstName": "Test",
    "lastName": "User",
    "password": "P@ssw0rd!"
  }'

# Se connecter
curl -i -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "P@ssw0rd!"
  }'

# Vérifier le cookie dans la réponse
# Set-Cookie: .AspNetCore.Identity.Application=...
```

### 4. Test OAuth2 PKCE

Voir `src/Johodp.Api/httpTest/pkceconnection.http` pour un test complet.

```bash
# 1. Générer code_verifier et code_challenge
# (Voir section PKCE du README.md)

# 2. Initier l'autorisation dans un navigateur
https://localhost:5001/connect/authorize?response_type=code&client_id=johodp-spa&redirect_uri=http%3A%2F%2Flocalhost%3A4200%2Fcallback&scope=openid%20profile%20email%20johodp.api&code_challenge=YOUR_CHALLENGE&code_challenge_method=S256

# 3. Après connexion, échanger le code contre un token
curl -X POST https://localhost:5001/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=authorization_code" \
  -d "client_id=johodp-spa" \
  -d "code=AUTHORIZATION_CODE" \
  -d "redirect_uri=http://localhost:4200/callback" \
  -d "code_verifier=YOUR_VERIFIER"
```

### 5. Vérifier les logs

```bash
# Console logs
tail -f logs/johodp-*.log

# Rechercher les logs d'un tenant spécifique
grep "acme-corp" logs/johodp-*.log

# Vérifier l'enrichissement
# Les logs devraient contenir TenantId et ClientId
[15:42:31 INF] acme-corp johodp-spa User authenticated successfully
```

## 🐛 Troubleshooting

### Problème : Échec de connexion à PostgreSQL

**Symptôme** :
```
Npgsql.NpgsqlException: Failed to connect to [::1]:5432
```

**Solution** :
```bash
# Vérifier que PostgreSQL est en cours d'exécution
docker ps | grep postgres

# Tester la connexion
psql -h localhost -U postgres -d johodp

# Vérifier la chaîne de connexion dans appsettings.json
```

### Problème : Échec des migrations

**Symptôme** :
```
System.InvalidOperationException: Unable to resolve service for type 'Microsoft.EntityFrameworkCore.DbContextOptions'
```

**Solution** :
```bash
# S'assurer que la chaîne de connexion est configurée
# Vérifier que Johodp.Infrastructure.csproj contient Microsoft.EntityFrameworkCore.Design
dotnet add src/Johodp.Infrastructure package Microsoft.EntityFrameworkCore.Design

# Réexécuter les migrations
dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api
```

### Problème : IdentityServer ne démarre pas

**Symptôme** :
```
Duende.IdentityServer.Configuration.DuendeIdentityServerException: License key validation failed
```

**Solution** :
```bash
# Vérifier la licence Duende IdentityServer (gratuit pour dev)
# Télécharger la clé de licence depuis https://duendesoftware.com/products/identityserver

# Ajouter au appsettings.json
{
  "Duende": {
    "LicenseKey": "YOUR_LICENSE_KEY"
  }
}
```

### Problème : CORS bloque les requêtes SPA

**Symptôme** :
```
Access to fetch at 'http://localhost:5000/api/auth/login' from origin 'http://localhost:4200' has been blocked by CORS policy
```

**Solution** :
```csharp
// Vérifier la configuration CORS dans Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpa", builder =>
    {
        builder.WithOrigins("http://localhost:4200")
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials(); // Important pour les cookies
    });
});

app.UseCors("AllowSpa");
```

### Problème : Enricher ne capture pas le TenantId

**Symptôme** :
```
[15:42:31 INF]   User authenticated successfully
# TenantId et ClientId sont vides
```

**Solution** :
```csharp
// Vérifier que HttpContextAccessor est enregistré
builder.Services.AddHttpContextAccessor();

// Vérifier que l'enricher est configuré
.Enrich.With<TenantClientEnricher>()

// Vérifier le log template
outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {TenantId} {ClientId} {Message:lj}{NewLine}{Exception}"
```

### Problème : MFA QR code ne s'affiche pas

**Symptôme** :
```
GET /api/auth/mfa/provision
{
  "qrCodeUri": "",
  "manualEntryKey": "JBSWY3DPEHPK3PXP"
}
```

**Solution** :
```bash
# Vérifier que QRCoder est installé
dotnet add src/Johodp.Infrastructure package QRCoder

# Vérifier TotpService.GenerateQrCodeUri
# Le format doit être: otpauth://totp/Johodp:{email}?secret={key}&issuer=Johodp
```

### Problème : Webhook vers l'application tierce échoue

**Symptôme** :
```
System.Net.Http.HttpRequestException: 401 Unauthorized
```

**Solution** :
```bash
# Vérifier la configuration External IdP
# Tester l'obtention du token manuellement
curl -X POST https://external-idp.example.com/oauth2/token \
  -d "grant_type=client_credentials" \
  -d "client_id=johodp-webhook-sender" \
  -d "client_secret=your_secret" \
  -d "scope=webhooks.send"

# Vérifier que le token est inclus dans les requêtes webhook
# Authorization: Bearer {access_token}

# Vérifier les logs de retry Polly
```

### Plus de ressources

- Documentation complète : `README.md`
- Dépannage : `TROUBLESHOOTING.md`
- Architecture : `ARCHITECTURE.md`
- Enrichers : `LOGGING_ENRICHERS.md`
- Health checks : `HEALTH_CHECKS.md`
- Rotation des certificats : `CERTIFICATE_ROTATION.md`

---

**Installation validée** ✅

Pour toute question ou problème non résolu, consulter les issues GitHub ou contacter l'équipe de développement.
