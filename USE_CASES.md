# 📋 Cas d'Usage de Johodp Identity Provider

## Vue d'ensemble

Johodp est un Identity Provider multi-tenant basé sur OAuth2/OIDC, conçu pour permettre aux applications tierces de déléguer l'authentification et la gestion des utilisateurs tout en conservant le contrôle sur qui peut accéder à leurs services.

---

## 🎯 Cas d'Usage Principaux

### UC-01: Création d'un Client OAuth2 (Application Tierce)

**Acteur Principal:** Administrateur système

**Préconditions:**
- L'administrateur a accès à l'API Johodp
- Un ClientName unique est disponible

**Scénario Principal:**
1. L'administrateur envoie une requête POST `/api/clients` avec:
   - `clientName`: Identifiant unique du client (ex: "my-app")
   - `allowedScopes`: Liste de scopes OAuth2 (ex: ["openid", "profile", "email"])
   - `requireConsent`: true/false (demander consentement à l'utilisateur)
2. Le système crée un agrégat `Client` dans l'état suivant:
   - `RequireClientSecret = true` (PKCE avec client secret)
   - `RequirePkce = true` (Protection PKCE obligatoire)
   - `IsActive = true`
3. Le système retourne le `ClientDto` avec un `ClientId` (GUID)
4. **Note:** Le client est créé SANS tenant associé (pas de redirect URIs)
5. Le client n'est PAS visible pour IdentityServer tant qu'il n'a pas de tenant

**Règles de Gestion:**
- RG-CLIENT-01: Un client ne peut pas être créé avec un tenant (tenant optionnel supprimé)
- RG-CLIENT-02: Un clientName doit être unique dans le système
- RG-CLIENT-03: Un client sans tenant n'est pas visible pour IdentityServer (sécurité)
- RG-CLIENT-04: Les scopes doivent être des valeurs valides (openid, profile, email, api)

**Postconditions:**
- Un client est créé mais non fonctionnel (besoin d'un tenant)
- Le client n'apparaît pas dans IdentityServer

---

### UC-02: Création d'un Tenant pour un Client

**Acteur Principal:** Administrateur système

**Préconditions:**
- Un client existe déjà (UC-01 complété)
- Le ClientName du client est connu

**Scénario Principal:**
1. L'administrateur envoie POST `/api/tenant` avec:
   - `name`: Identifiant du tenant (ex: "acme-corp")
   - `displayName`: Nom affiché (ex: "ACME Corporation")
   - `clientId`: ClientName du client existant (OBLIGATOIRE)
   - `allowedReturnUrls`: Liste des URLs de redirection (ex: ["http://localhost:4200/callback"])
   - `allowedCorsOrigins`: Liste des origines CORS (ex: ["http://localhost:4200"])
   - Branding optionnel: primaryColor, secondaryColor, logoUrl, customCss
   - Localisation optionnelle: timezone, currency, supportedLanguages
2. Le système vérifie que le client existe
3. Le système crée l'agrégat `Tenant` avec:
   - Association bidirectionnelle avec le client
   - Validation des URLs de redirection (format URI absolu)
   - Validation des CORS origins (format URI autorité uniquement, pas de path)
4. Le système met à jour le client pour ajouter le tenant dans `AssociatedTenantIds`
5. Le système persiste les changements
6. Le client devient VISIBLE pour IdentityServer (a des redirect URIs)

**Règles de Gestion:**
- RG-TENANT-01: Un tenant DOIT avoir un client associé (ClientId obligatoire)
- RG-TENANT-02: Un tenant ne peut être associé qu'à UN SEUL client (relation 1-1)
- RG-TENANT-03: Le client doit exister AVANT la création du tenant
- RG-TENANT-04: Un tenant doit avoir au moins une URL de redirection
- RG-TENANT-05: Les CORS origins doivent être des URIs d'autorité uniquement (pas de path)
  * ✅ Valide: `http://localhost:4200`, `https://app.acme.com`
  * ❌ Invalide: `http://localhost:4200/callback`, `https://app.acme.com/path`
- RG-TENANT-06: AllowedCorsOrigins géré au niveau Tenant (migration depuis Client)
- RG-TENANT-07: CustomClientStore agrège CORS depuis tous les tenants associés au client
- RG-TENANT-06: Un nom de tenant doit être unique dans le système

**Postconditions:**
- Le tenant est créé et actif
- Le client devient visible pour IdentityServer
- Les redirect URIs et CORS origins sont agrégés dynamiquement

---

### UC-03: Récupération Dynamique d'un Client par IdentityServer

**Acteur Principal:** IdentityServer (système)

**Préconditions:**
- Un client existe avec au moins un tenant associé
- Une requête OAuth2 arrive avec le ClientName

**Scénario Principal:**
1. IdentityServer appelle `CustomClientStore.FindClientByIdAsync(clientName)`
2. Le système récupère le `Client` depuis la base de données
3. Le système récupère TOUS les tenants associés (`AssociatedTenantIds`)
4. Le système agrège dynamiquement:
   - `RedirectUris`: Union de tous les `AllowedReturnUrls` des tenants
   - `AllowedCorsOrigins`: Union de tous les `AllowedCorsOrigins` des tenants
   - `PostLogoutRedirectUris`: Mêmes valeurs que RedirectUris
5. **Cas particulier 1:** Si le client n'a aucun tenant → retourne `null` (non visible)
6. **Cas particulier 2:** Si les tenants n'ont aucune redirect URI → retourne `null` (non visible)
7. Le système retourne un `Duende.IdentityServer.Models.Client` configuré

**Règles de Gestion:**
- RG-DYNAMIC-01: Les redirect URIs sont agrégées en temps réel (pas de cache)
- RG-DYNAMIC-02: Les CORS origins sont agrégées en temps réel (pas de cache)
- RG-DYNAMIC-03: Un client sans tenant n'est jamais visible
- RG-DYNAMIC-04: Un client avec tenants mais sans redirect URIs n'est jamais visible
- RG-DYNAMIC-05: Les redirections sont dédupliquées (même URL dans plusieurs tenants = une seule entrée)

**Postconditions:**
- IdentityServer reçoit un client valide OU null
- Le client est prêt pour le flux OAuth2/OIDC

---

### UC-04: Flux d'Onboarding Utilisateur (Application Tierce)

**Acteur Principal:** Utilisateur final

**Préconditions:**
- Un tenant existe avec un client associé
- L'application tierce a une notification URL configurée

**Scénario Principal:**
1. L'utilisateur clique sur "Créer un compte" dans l'application tierce
2. L'application redirige vers `/account/onboarding?acr_values=tenant:acme-corp`
3. Johodp affiche le formulaire d'onboarding avec le branding du tenant (logo, couleurs)
4. L'utilisateur remplit: email, firstName, lastName
5. L'utilisateur soumet le formulaire
6. Le système vérifie que l'email n'existe pas déjà
7. Le système envoie une notification HTTP POST vers l'app tierce:
   ```json
   {
     "requestId": "uuid",
     "tenantId": "acme-corp",
     "email": "user@example.com",
     "firstName": "John",
     "lastName": "Doe"
   }
   ```
8. Le système affiche la page "En attente de validation"
9. **Scénario asynchrone:** L'app tierce valide (règles métier)
10. L'app tierce appelle POST `/api/users/register`:
    ```json
    {
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "tenantId": "acme-corp",
      "createAsPending": true
    }
    ```
11. Le système crée l'utilisateur en statut `PendingActivation`
12. Le système génère un token d'activation
13. **TODO:** Le système envoie un email avec le lien d'activation
14. L'utilisateur clique sur le lien d'activation

**Règles de Gestion:**
- RG-ONBOARD-01: L'email doit être unique dans tout le système
- RG-ONBOARD-02: La notification à l'app tierce est "fire-and-forget" (pas de retry)
- RG-ONBOARD-03: L'utilisateur ne peut pas s'auto-activer (doit passer par validation tierce)
- RG-ONBOARD-04: Le tenant doit être actif (`IsActive = true`)
- RG-ONBOARD-05: Le branding du tenant est appliqué (CSS, logo, couleurs)

**Postconditions:**
- Un utilisateur en statut `PendingActivation` est créé
- Un token d'activation est généré et prêt à être envoyé par email

---

### UC-05: Activation de Compte Utilisateur

**Acteur Principal:** Utilisateur final

**Préconditions:**
- Un utilisateur existe en statut `PendingActivation`
- L'utilisateur a reçu un email avec un token d'activation

**Scénario Principal:**
1. L'utilisateur clique sur le lien d'activation:
   `/account/activate?token=<token>&userId=<guid>&tenant=acme-corp`
2. Johodp affiche le formulaire d'activation avec:
   - Email masqué (ex: `j***n@example.com`)
   - Branding du tenant
   - Champs de mot de passe (nouveau + confirmation)
3. L'utilisateur entre et confirme son mot de passe
4. L'utilisateur soumet le formulaire
5. Le système vérifie le token avec `UserManager.VerifyUserTokenAsync`
6. Le système hache le mot de passe avec `IPasswordHasher`
7. Le système appelle `user.SetPasswordHash(hashedPassword)`
8. Le système appelle `user.Activate()` (domain event: `UserActivatedEvent`)
9. Le système confirme l'email avec `UserManager.ConfirmEmailAsync`
10. Le système change le statut de `PendingActivation` à `Active`
11. Le système connecte automatiquement l'utilisateur
12. Le système redirige vers la page de succès

**Règles de Gestion:**
- RG-ACTIVATE-01: Le token ne peut être utilisé qu'une seule fois
- RG-ACTIVATE-02: Le token expire après 24h (configurable)
- RG-ACTIVATE-03: L'utilisateur doit être en statut `PendingActivation`
- RG-ACTIVATE-04: Le mot de passe doit respecter les règles de complexité
- RG-ACTIVATE-05: L'utilisateur est automatiquement connecté après activation

**Postconditions:**
- L'utilisateur passe en statut `Active`
- L'email est confirmé (`EmailConfirmed = true`)
- Un cookie de session est créé
- L'utilisateur peut maintenant se connecter normalement

---

### UC-06: Authentification OAuth2 avec PKCE (SPA)

**Acteur Principal:** Utilisateur final via Application SPA

**Préconditions:**
- Un client existe avec un tenant configuré
- Un utilisateur actif existe dans le système
- L'application SPA est configurée avec le client OAuth2

**Scénario Principal:**
1. L'utilisateur clique sur "Se connecter" dans la SPA
2. La SPA génère un `code_verifier` et calcule le `code_challenge` (PKCE)
3. La SPA redirige vers:
   ```
   /connect/authorize?
     client_id=my-app&
     response_type=code&
     scope=openid profile email&
     redirect_uri=http://localhost:4200/callback&
     code_challenge=<challenge>&
     code_challenge_method=S256&
     acr_values=tenant:acme-corp
   ```
4. IdentityServer vérifie le client via `CustomClientStore`
5. L'utilisateur est redirigé vers `/account/login` (pas authentifié)
6. L'utilisateur entre email et mot de passe
7. Le système vérifie les credentials via `UserManager.CheckPasswordAsync`
8. Le système vérifie que l'utilisateur a accès au tenant demandé
9. Le système crée une session (cookie "Cookies")
10. IdentityServer génère un `authorization_code`
11. IdentityServer redirige vers: `http://localhost:4200/callback?code=<code>`
12. La SPA échange le code contre un token:
    ```
    POST /connect/token
    {
      "grant_type": "authorization_code",
      "code": "<code>",
      "redirect_uri": "http://localhost:4200/callback",
      "client_id": "my-app",
      "code_verifier": "<original_verifier>"
    }
    ```
13. IdentityServer valide le PKCE (code_verifier vs code_challenge)
14. IdentityServer retourne:
    ```json
    {
      "access_token": "eyJ...",
      "id_token": "eyJ...",
      "refresh_token": "...",
      "expires_in": 3600,
      "token_type": "Bearer"
    }
    ```
15. La SPA stocke les tokens et peut appeler l'API

**Règles de Gestion:**
- RG-OAUTH-01: PKCE est obligatoire (RequirePkce = true)
- RG-OAUTH-02: Le redirect_uri DOIT être dans AllowedReturnUrls du tenant
- RG-OAUTH-03: L'origine CORS DOIT être dans AllowedCorsOrigins du tenant
- RG-OAUTH-04: L'utilisateur DOIT avoir accès au tenant demandé (TenantIds)
- RG-OAUTH-05: Le code d'autorisation expire après 5 minutes
- RG-OAUTH-06: L'access_token expire après 1 heure (configurable)
- RG-OAUTH-07: Le refresh_token permet de renouveler l'access_token (sliding 15 jours)

**Postconditions:**
- L'utilisateur est authentifié dans la SPA
- La SPA a un access_token pour appeler l'API
- La SPA a un refresh_token pour renouveler la session

---

### UC-07: Appel API Protégé avec Access Token

**Acteur Principal:** Application SPA

**Préconditions:**
- La SPA a obtenu un access_token (UC-06 complété)
- L'API Johodp expose des endpoints protégés

**Scénario Principal:**
1. La SPA appelle une API protégée:
   ```
   GET /api/users/me
   Authorization: Bearer eyJ...
   ```
2. Le middleware JWT d'ASP.NET Core valide le token:
   - Signature valide (clé de signature IdentityServer)
   - Token non expiré
   - Issuer valide (IdentityServer)
   - Audience valide (API)
3. Le middleware extrait les claims du token:
   - `sub`: User ID
   - `email`: Email
   - `role`: Rôles
   - `scope`: Scopes autorisés
4. Le controller retourne les données demandées
5. La SPA reçoit la réponse JSON

**Règles de Gestion:**
- RG-API-01: Le token DOIT être signé par IdentityServer
- RG-API-02: Le token ne peut pas être expiré
- RG-API-03: Les scopes du token doivent correspondre à l'endpoint appelé
- RG-API-04: Les erreurs de validation retournent 401 Unauthorized

**Postconditions:**
- Les données sont retournées à la SPA
- Le token reste valide pour d'autres appels

---

### UC-08: Renouvellement de Token avec Refresh Token

**Acteur Principal:** Application SPA

**Préconditions:**
- La SPA a un refresh_token valide
- L'access_token est expiré ou proche de l'expiration

**Scénario Principal:**
1. La SPA détecte que l'access_token va expirer (< 5 minutes)
2. La SPA appelle:
   ```
   POST /connect/token
   {
     "grant_type": "refresh_token",
     "refresh_token": "<refresh_token>",
     "client_id": "my-app"
   }
   ```
3. IdentityServer valide le refresh_token:
   - Token non expiré
   - Token non révoqué
   - Client ID correspond
4. IdentityServer génère un nouvel access_token ET un nouveau refresh_token
5. IdentityServer révoque l'ancien refresh_token (one-time use)
6. IdentityServer retourne:
   ```json
   {
     "access_token": "eyJ... (nouveau)",
     "refresh_token": "... (nouveau)",
     "expires_in": 3600
   }
   ```
7. La SPA remplace les anciens tokens par les nouveaux

**Règles de Gestion:**
- RG-REFRESH-01: Les refresh_tokens sont "one-time use" (usage unique)
- RG-REFRESH-02: Le refresh_token expire après 15 jours (sliding)
- RG-REFRESH-03: Chaque renouvellement réinitialise le délai de 15 jours
- RG-REFRESH-04: Un refresh_token révoqué ne peut plus être utilisé

**Postconditions:**
- La SPA a un nouvel access_token valide
- La SPA a un nouveau refresh_token
- L'ancien refresh_token est révoqué

---

### UC-09: Gestion Multi-Tenant pour un Utilisateur

**Acteur Principal:** Administrateur système

**Préconditions:**
- Un utilisateur existe dans le système
- Plusieurs tenants existent

**Scénario Principal:**
1. L'administrateur appelle POST `/api/users/{userId}/tenants/{tenantId}`
2. Le système récupère l'utilisateur
3. Le système vérifie que le tenant existe
4. Le système appelle `user.AddTenantId(tenantId)` (domain)
5. Le système sauvegarde les changements
6. L'utilisateur peut maintenant s'authentifier avec ce tenant

**Scénario Alternatif:** Retrait d'accès
1. L'administrateur appelle DELETE `/api/users/{userId}/tenants/{tenantId}`
2. Le système appelle `user.RemoveTenantId(tenantId)`
3. L'utilisateur ne peut plus s'authentifier avec ce tenant

**Règles de Gestion:**
- RG-MULTITENANT-01: Un utilisateur peut avoir accès à plusieurs tenants
- RG-MULTITENANT-02: Un utilisateur avec `TenantIds = ["*"]` a accès à tous les tenants
- RG-MULTITENANT-03: À la connexion, l'utilisateur DOIT avoir le tenant demandé dans sa liste
- RG-MULTITENANT-04: Un utilisateur sans tenant ne peut pas se connecter

**Postconditions:**
- L'utilisateur a accès au tenant spécifié
- L'utilisateur peut s'authentifier via ce tenant

---

### UC-10: Personnalisation du Branding par Tenant

**Acteur Principal:** Application SPA

**Préconditions:**
- Un tenant existe avec du branding configuré

**Scénario Principal:**
1. La SPA appelle GET `/api/tenant/{tenantId}/branding.css`
2. Le système récupère le tenant
3. Le système génère un fichier CSS dynamique avec:
   - `--primary-color`: Couleur primaire
   - `--secondary-color`: Couleur secondaire
   - `--logo-base64`: URL du logo
   - `--image-base64`: URL de l'image de fond
   - Custom CSS du tenant
4. Le système retourne le CSS avec Content-Type: `text/css`
5. La SPA inclut ce CSS dans sa page de login

**Règles de Gestion:**
- RG-BRAND-01: Le CSS est généré dynamiquement à chaque requête
- RG-BRAND-02: Les valeurs par défaut sont utilisées si non configurées
- RG-BRAND-03: Le custom CSS est injecté après les variables CSS

**Postconditions:**
- La page de login affiche le branding du tenant
- L'expérience utilisateur est personnalisée

---

### UC-11: Récupération des Informations de Localisation

**Acteur Principal:** Application SPA

**Préconditions:**
- Un tenant existe avec des paramètres de localisation

**Scénario Principal:**
1. La SPA appelle GET `/api/tenant/{tenantId}/language`
2. Le système retourne:
   ```json
   {
     "tenantId": "acme-corp",
     "defaultLanguage": "fr-FR",
     "supportedLanguages": ["fr-FR", "en-US"],
     "dateFormat": "dd/MM/yyyy",
     "timeFormat": "HH:mm",
     "timezone": "Europe/Paris",
     "currency": "EUR"
   }
   ```
3. La SPA configure son système i18n avec ces valeurs

**Règles de Gestion:**
- RG-I18N-01: Le defaultLanguage est obligatoire
- RG-I18N-02: Les supportedLanguages incluent toujours le defaultLanguage
- RG-I18N-03: Le timezone et currency ont des valeurs par défaut

**Postconditions:**
- La SPA affiche les dates, heures et montants dans le format du tenant

---

## 🔐 Règles de Sécurité Transversales

### SEC-01: Validation des Redirect URIs
- **Règle:** Seules les URLs configurées dans `AllowedReturnUrls` des tenants sont acceptées
- **Impact:** Empêche les attaques Open Redirect
- **Validation:** IdentityServer vérifie automatiquement via CustomClientStore

### SEC-02: CORS Origins
- **Règle:** Seules les origines configurées dans `AllowedCorsOrigins` peuvent appeler l'API
- **Impact:** Empêche les requêtes cross-origin non autorisées
- **Validation:** Middleware CORS d'ASP.NET Core

### SEC-03: PKCE Obligatoire
- **Règle:** PKCE est requis pour tous les clients (RequirePkce = true)
- **Impact:** Protection contre l'interception du code d'autorisation
- **Validation:** IdentityServer refuse les requêtes sans PKCE

### SEC-04: Token Expiration
- **Règle:** Access tokens expirent après 1h, refresh tokens après 15 jours
- **Impact:** Limite la durée de vie des tokens compromis
- **Validation:** IdentityServer vérifie automatiquement

### SEC-05: Isolation Tenant
- **Règle:** Un utilisateur ne peut accéder qu'aux tenants dans sa liste TenantIds
- **Impact:** Empêche l'accès cross-tenant non autorisé
- **Validation:** AccountController vérifie à chaque connexion

---

## 📊 Diagramme de Séquence Complet

```
SPA              IdP (Johodp)         CustomClientStore    Database
 |                    |                       |                |
 |-- Auth Request --->|                       |                |
 |    (PKCE)          |                       |                |
 |                    |-- Get Client -------->|                |
 |                    |                       |-- Query ------>|
 |                    |                       |<-- Client -----|
 |                    |                       |-- Get Tenants->|
 |                    |                       |<-- Tenants ----|
 |                    |<-- Aggregate URIs ----|                |
 |                    |                       |                |
 |<-- Redirect to Login-|                     |                |
 |                    |                       |                |
 |-- Login Form ----->|                       |                |
 |    (credentials)   |                       |                |
 |                    |-- Verify Password --->|                |
 |                    |<-- User Valid --------|                |
 |                    |-- Create Session ---->|                |
 |<-- Authorization Code-|                    |                |
 |                    |                       |                |
 |-- Token Request -->|                       |                |
 |    (code + PKCE)   |                       |                |
 |                    |-- Validate PKCE ----->|                |
 |                    |-- Generate Tokens --->|                |
 |<-- Access Token ---|                       |                |
 |    + Refresh Token |                       |                |
 |                    |                       |                |
 |-- API Call ------->|                       |                |
 |    (Bearer token)  |                       |                |
 |                    |-- Validate Token ---->|                |
 |<-- Protected Data -|                       |                |
```

---

## 🎭 Scénarios d'Erreur

### ERR-01: Client sans Tenant
- **Situation:** Un client est créé mais aucun tenant n'est associé
- **Comportement:** CustomClientStore retourne `null`
- **Résultat:** IdentityServer rejette la requête OAuth2 (client inconnu)

### ERR-02: Tenant sans Redirect URIs
- **Situation:** Un tenant est créé mais sans AllowedReturnUrls
- **Comportement:** CustomClientStore retourne `null`
- **Résultat:** IdentityServer rejette la requête OAuth2 (client invalide)

### ERR-03: Utilisateur sans Accès au Tenant
- **Situation:** Un utilisateur essaie de se connecter à un tenant non autorisé
- **Comportement:** AccountController refuse la connexion
- **Résultat:** Message "User does not have access to this tenant"

### ERR-04: Redirect URI Non Autorisée
- **Situation:** Une SPA demande une redirect_uri non dans AllowedReturnUrls
- **Comportement:** IdentityServer rejette la requête
- **Résultat:** Erreur OAuth2 "invalid_request"

### ERR-05: Token Expiré
- **Situation:** Une SPA utilise un access_token expiré
- **Comportement:** Le middleware JWT rejette la requête
- **Résultat:** 401 Unauthorized

### ERR-06: Activation avec Token Invalide
- **Situation:** Un utilisateur essaie d'activer son compte avec un token expiré
- **Comportement:** UserManager.VerifyUserTokenAsync retourne false
- **Résultat:** Message "Invalid or expired activation token"

---

## 🧪 Scénarios de Test

### TEST-01: Workflow Complet SPA
1. Créer client
2. Créer tenant avec redirect URIs + CORS
3. Créer utilisateur en PendingActivation
4. Activer l'utilisateur
5. Flux OAuth2 complet avec PKCE
6. Appel API avec access_token
7. Renouvellement avec refresh_token

### TEST-02: Multi-Tenant
1. Créer 2 tenants (tenant-A, tenant-B)
2. Créer utilisateur avec accès à tenant-A uniquement
3. Tenter connexion avec tenant-A → Succès
4. Tenter connexion avec tenant-B → Refusé
5. Ajouter tenant-B à l'utilisateur
6. Tenter connexion avec tenant-B → Succès

### TEST-03: Sécurité CORS
1. Configurer tenant avec CORS origin = `http://localhost:4200` (AllowedCorsOrigins au niveau Tenant)
2. Tenter requête depuis `http://localhost:4200` → Accepté
3. Tenter requête depuis `http://evil.com` dans navigateur → Refusé (CORS)
4. **⚠️ Tenter requête avec curl depuis n'importe où → Accepté (CORS ne protège pas !)**
5. **✅ Solution:** Implémenter authentication + authorization pour vraie sécurité

### TEST-04: Branding Dynamique
1. Créer tenant-A avec logo rouge
2. Créer tenant-B avec logo bleu
3. Récupérer `/api/tenant/tenant-A/branding.css` → CSS rouge
4. Récupérer `/api/tenant/tenant-B/branding.css` → CSS bleu

---

## 📚 Références

- Architecture DDD: `ARCHITECTURE.md`
- Flux de compte: `ACCOUNT_FLOWS.md`
- Endpoints API: `API_ENDPOINTS.md`
- Onboarding: `ONBOARDING_FLOW.md`
