# 📖 User Stories - Johodp Identity Provider

## Vue d'ensemble

Ce document liste toutes les User Stories nécessaires pour construire le système Johodp Identity Provider, organisées par epic et priorité.

---

## 🎯 Epic 1: Gestion des Clients OAuth2

### US-1.1: Créer un Client OAuth2 (DOIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** créer un nouveau client OAuth2  
**Afin que** les applications tierces puissent s'intégrer avec Johodp

**Critères d'acceptation:**
- [ ] Je peux envoyer POST `/api/clients` avec clientName et allowedScopes
- [ ] Le système génère un ClientId unique (GUID)
- [ ] Le client est créé avec RequirePkce=true et RequireClientSecret=true
- [ ] Le client est dans l'état IsActive=true
- [ ] Le client n'a aucun tenant associé initialement
- [ ] Le système refuse si le clientName existe déjà (409 Conflict)
- [ ] Le système valide que les scopes sont valides (openid, profile, email, api)

**Tests d'acceptation:**
```http
POST /api/clients
{
  "clientName": "my-spa-app",
  "allowedScopes": ["openid", "profile", "email"],
  "requireConsent": true
}
→ 201 Created avec ClientDto
```

**DoD (Definition of Done):**
- Code implémenté dans ClientsController.Create()
- Tests unitaires pour CreateClientCommand
- Tests d'intégration avec base de données
- Documentation API mise à jour

---

### US-1.2: Consulter un Client par ID (DOIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** récupérer les détails d'un client par son ID  
**Afin de** vérifier sa configuration

**Critères d'acceptation:**
- [ ] Je peux appeler GET `/api/clients/{clientId}`
- [ ] Le système retourne le ClientDto avec tous les détails
- [ ] Le système retourne 404 si le client n'existe pas
- [ ] Les tenants associés sont inclus (AssociatedTenantIds)

**Tests d'acceptation:**
```http
GET /api/clients/550e8400-e29b-41d4-a716-446655440000
→ 200 OK avec ClientDto
```

---

### US-1.3: Consulter un Client par Nom (DOIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** récupérer un client par son nom  
**Afin de** vérifier rapidement sa configuration sans connaître son GUID

**Critères d'acceptation:**
- [ ] Je peux appeler GET `/api/clients/by-name/{clientName}`
- [ ] Le système retourne le ClientDto correspondant
- [ ] Le système retourne 404 si le clientName n'existe pas

**Tests d'acceptation:**
```http
GET /api/clients/by-name/my-spa-app
→ 200 OK avec ClientDto
```

---

### US-1.4: Mettre à Jour un Client (DEVRAIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** modifier les scopes et paramètres d'un client  
**Afin de** ajuster sa configuration sans le recréer

**Critères d'acceptation:**
- [ ] Je peux envoyer PUT `/api/clients/{clientId}` avec UpdateClientDto
- [ ] Le système met à jour allowedScopes si fourni
- [ ] Le système met à jour requireConsent si fourni
- [ ] Le système met à jour associatedTenantIds si fourni
- [ ] Le système retourne 404 si le client n'existe pas
- [ ] Le système refuse les associations à des tenants inexistants

**Tests d'acceptation:**
```http
PUT /api/clients/550e8400-e29b-41d4-a716-446655440000
{
  "allowedScopes": ["openid", "profile", "email", "api"],
  "requireConsent": false
}
→ 200 OK avec ClientDto mis à jour
```

---

### US-1.5: Supprimer un Client (DEVRAIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** supprimer un client obsolète  
**Afin de** nettoyer le système

**Critères d'acceptation:**
- [ ] Je peux appeler DELETE `/api/clients/{clientId}`
- [ ] Le système supprime le client de la base de données
- [ ] Le système retourne 204 No Content en cas de succès
- [ ] Le système retourne 404 si le client n'existe pas
- [ ] Les tenants associés sont également dissociés

**Tests d'acceptation:**
```http
DELETE /api/clients/550e8400-e29b-41d4-a716-446655440000
→ 204 No Content
```

---

## 🏢 Epic 2: Gestion des Tenants

### US-2.1: Créer un Tenant avec Client Obligatoire (DOIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** créer un tenant associé à un client existant  
**Afin de** configurer les redirections et le branding pour une organisation

**Critères d'acceptation:**
- [ ] Je peux envoyer POST `/api/tenant` avec CreateTenantDto
- [ ] Le champ clientId est OBLIGATOIRE
- [ ] Le système vérifie que le client existe avant création
- [ ] Le système refuse si le client n'existe pas (400 Bad Request)
- [ ] Le système crée l'association bidirectionnelle (Tenant ↔ Client)
- [ ] Le système valide les AllowedReturnUrls (format URI absolu)
- [ ] Le système valide les AllowedCorsOrigins (format autorité uniquement)
- [ ] Le tenant doit avoir au moins une URL de redirection
- [ ] Le système refuse si le nom de tenant existe déjà (409 Conflict)
- [ ] Je peux définir `userVerificationEndpoint` (webhook) pour la validation d'inscription
- [ ] `userVerificationEndpoint` DOIT être HTTPS en production
- [ ] Le système stocke le webhook et l'utilise lors des demandes d'onboarding (Ref UC-04)

**Tests d'acceptation:**
```http
POST /api/tenant
{
  "name": "acme-corp-example-com",
  "tenantUrl": "https://acme-corp.example.com",
  "displayName": "ACME Corporation",
  "clientId": "my-spa-app",
  "allowedReturnUrls": ["http://localhost:4200/callback"],
  "allowedCorsOrigins": ["http://localhost:4200"],
  "primaryColor": "#ff0000",
  "logoUrl": "https://acme.com/logo.png"
}
→ 201 Created avec TenantDto
# Note: 'name' est dérivé de 'tenantUrl' (https://acme-corp.example.com → acme-corp-example-com)
```

**DoD:**
- Code implémenté dans TenantController.Create()
- Tests unitaires pour CreateTenantCommand
- Validation des URLs avec regex
- Tests d'intégration avec client existant
- Documentation API mise à jour

---

### US-2.2: Consulter Tous les Tenants (DOIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** lister tous les tenants  
**Afin de** avoir une vue d'ensemble du système

**Critères d'acceptation:**
- [ ] Je peux appeler GET `/api/tenant`
- [ ] Le système retourne une liste de TenantDto
- [ ] Les tenants inactifs sont inclus
- [ ] La liste peut être vide si aucun tenant existe

**Tests d'acceptation:**
```http
GET /api/tenant
→ 200 OK avec liste de TenantDto
```

---

### US-2.3: Consulter un Tenant par ID (DOIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** récupérer les détails d'un tenant par son ID  
**Afin de** vérifier sa configuration complète

**Critères d'acceptation:**
- [ ] Je peux appeler GET `/api/tenant/{id}`
- [ ] Le système retourne le TenantDto avec tous les détails
- [ ] Les informations de branding sont incluses
- [ ] Les AllowedReturnUrls et AllowedCorsOrigins sont inclus
- [ ] Le ClientId associé est inclus
- [ ] Le système retourne 404 si le tenant n'existe pas

**Tests d'acceptation:**
```http
GET /api/tenant/550e8400-e29b-41d4-a716-446655440000
→ 200 OK avec TenantDto complet
```

---

### US-2.4: Consulter un Tenant par Nom (DOIT AVOIR)
**En tant qu'** application tierce  
**Je veux** récupérer un tenant par son nom  
**Afin de** charger sa configuration de branding

**Critères d'acceptation:**
- [ ] Je peux appeler GET `/api/tenant/by-name/{name}`
- [ ] Le système retourne le TenantDto correspondant
- [ ] Le système retourne 404 si le nom n'existe pas
- [ ] L'endpoint est accessible publiquement (AllowAnonymous)

**Tests d'acceptation:**
```http
GET /api/tenant/by-name/acme-corp
→ 200 OK avec TenantDto
```

---

### US-2.5: Mettre à Jour un Tenant (DEVRAIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** modifier la configuration d'un tenant  
**Afin de** ajuster le branding ou les URLs de redirection

**Critères d'acceptation:**
- [ ] Je peux envoyer PUT `/api/tenant/{id}` avec UpdateTenantDto
- [ ] Le système met à jour displayName si fourni
- [ ] Le système met à jour le branding (couleurs, logo, CSS) si fourni
- [ ] Le système remplace AllowedReturnUrls si fourni
- [ ] Le système remplace AllowedCorsOrigins si fourni
- [ ] Le système met à jour clientId si fourni (avec validation)
- [ ] Le système gère la dissociation/association du client si clientId change
- [ ] Le système retourne 404 si le tenant n'existe pas
- [ ] Le système valide que le nouveau client existe

**Tests d'acceptation:**
```http
PUT /api/tenant/550e8400-e29b-41d4-a716-446655440000
{
  "displayName": "ACME Corp (Updated)",
  "allowedReturnUrls": ["http://localhost:4200/callback", "https://app.acme.com/callback"],
  "primaryColor": "#0000ff"
}
→ 200 OK avec TenantDto mis à jour
```

---

### US-2.6: Supprimer un Tenant (DEVRAIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** supprimer un tenant obsolète  
**Afin de** nettoyer le système

**Critères d'acceptation:**
- [ ] Je peux appeler DELETE `/api/tenant/{id}`
- [ ] Le système supprime le tenant de la base de données
- [ ] Le système dissocie le tenant du client associé
- [ ] Le système retourne 204 No Content en cas de succès
- [ ] Le système retourne 404 si le tenant n'existe pas

**Tests d'acceptation:**
```http
DELETE /api/tenant/550e8400-e29b-41d4-a716-446655440000
→ 204 No Content
```

---

### US-2.7: Récupérer le CSS de Branding d'un Tenant (DOIT AVOIR)
**En tant qu'** application SPA  
**Je veux** récupérer le CSS de branding d'un tenant  
**Afin de** personnaliser l'apparence de ma page de connexion

**Critères d'acceptation:**
- [ ] Je peux appeler GET `/api/tenant/{tenantId}/branding.css`
- [ ] Le système génère un fichier CSS avec des variables CSS
- [ ] Les variables incluent: --primary-color, --secondary-color, --logo-base64, --image-base64
- [ ] Le customCss du tenant est inclus dans le fichier
- [ ] Le Content-Type de la réponse est "text/css"
- [ ] Le système retourne 404 si le tenant n'existe pas
- [ ] L'endpoint est accessible publiquement (AllowAnonymous)
- [ ] Génération dynamique (pas de cache), valeurs par défaut si absent (Ref UC-10)

**Tests d'acceptation:**
```http
GET /api/tenant/acme-corp/branding.css
→ 200 OK avec Content-Type: text/css
```

---

### US-2.8: Récupérer les Paramètres de Localisation d'un Tenant (DEVRAIT AVOIR)
**En tant qu'** application SPA  
**Je veux** récupérer les paramètres de langue et localisation  
**Afin de** configurer mon système i18n

**Critères d'acceptation:**
- [ ] Je peux appeler GET `/api/tenant/{tenantId}/language`
- [ ] Le système retourne defaultLanguage, supportedLanguages, timezone, currency
- [ ] Le système retourne également dateFormat et timeFormat
- [ ] Le système retourne 404 si le tenant n'existe pas
- [ ] L'endpoint est accessible publiquement (AllowAnonymous)
- [ ] supportedLanguages inclut toujours defaultLanguage (Ref UC-11)

**Tests d'acceptation:**
```http
GET /api/tenant/acme-corp/language
→ 200 OK avec objet JSON de localisation
```

---

## 👤 Epic 3: Gestion des Utilisateurs

### US-3.1: Créer un Utilisateur en Attente d'Activation (DOIT AVOIR)
**En tant qu'** application tierce  
**Je veux** créer un utilisateur en statut PendingActivation  
**Afin que** l'utilisateur puisse activer son compte plus tard

**Critères d'acceptation:**
- [ ] Je peux envoyer POST `/api/users/register` avec RegisterUserCommand
- [ ] Le champ createAsPending est forcé à true pour les appels API
- [ ] Le système crée l'utilisateur avec Status = PendingActivation
- [ ] Le système génère un token d'activation via UserManager
- [ ] Le système retourne userId, email, status et message
- [ ] Le système refuse si l'email existe déjà (409 Conflict)
- [ ] Le tenantId est obligatoire
- [ ] L'utilisateur est ajouté au tenant spécifié
- [ ] Requiert access_token avec scope administratif (Ref UC-04 RG-ONBOARD-08)

**Tests d'acceptation:**
```http
POST /api/users/register
{
  "email": "john.doe@acme.com",
  "firstName": "John",
  "lastName": "Doe",
  "tenantId": "acme-corp",
  "createAsPending": true
}
→ 201 Created avec { userId, email, status: "PendingActivation" }
```

**DoD:**
- Code implémenté dans UsersController.Register()
- Tests unitaires pour RegisterUserCommand
- Génération du token d'activation
- Tests d'intégration avec tenant existant
- Log du token en mode développement
- Documentation API mise à jour

---

### US-3.2: Consulter un Utilisateur par ID (DOIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** récupérer les détails d'un utilisateur  
**Afin de** vérifier son statut et ses informations

**Critères d'acceptation:**
- [ ] Je peux appeler GET `/api/users/{userId}`
- [ ] Le système retourne le UserDto avec tous les détails
- [ ] Les tenants de l'utilisateur sont inclus (TenantIds)
- [ ] Le statut de l'utilisateur est visible (Status)
- [ ] Le système retourne 404 si l'utilisateur n'existe pas

**Tests d'acceptation:**
```http
GET /api/users/550e8400-e29b-41d4-a716-446655440000
→ 200 OK avec UserDto
```

---

### US-3.3: Ajouter un Utilisateur à un Tenant (DEVRAIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** ajouter un utilisateur existant à un tenant  
**Afin de** lui donner accès à une nouvelle organisation

**Critères d'acceptation:**
- [ ] Je peux envoyer POST `/api/users/{userId}/tenants/{tenantId}`
- [ ] Le système vérifie que l'utilisateur existe
- [ ] Le système vérifie que le tenant existe
- [ ] Le système appelle user.AddTenantId(tenantId)
- [ ] Le système retourne 200 OK avec message de succès
- [ ] Le système retourne 404 si utilisateur ou tenant inexistant
- [ ] Le système refuse si l'utilisateur a déjà accès au tenant
- [ ] Supporte valeur spéciale `"*"` pour accès global (Ref UC-09 RG-MULTITENANT-02)

**Tests d'acceptation:**
```http
POST /api/users/550e8400-e29b-41d4-a716-446655440000/tenants/acme-corp-example-com
→ 200 OK avec { message: "User added to tenant successfully" }
# Note: acme-corp-example-com est l'URL nettoyée de https://acme-corp.example.com
```

---

### US-3.4: Retirer un Utilisateur d'un Tenant (DEVRAIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** retirer l'accès d'un utilisateur à un tenant  
**Afin de** révoquer ses permissions

**Critères d'acceptation:**
- [ ] Je peux appeler DELETE `/api/users/{userId}/tenants/{tenantId}`
- [ ] Le système vérifie que l'utilisateur existe
- [ ] Le système appelle user.RemoveTenantId(tenantId)
- [ ] Le système retourne 204 No Content en cas de succès
- [ ] Le système retourne 404 si utilisateur ou tenant inexistant
- [ ] L'utilisateur ne peut plus se connecter avec ce tenant
- [ ] Si l'utilisateur avait `"*"`, retrait explicite remplace par liste sans ce tenant

**Tests d'acceptation:**
```http
DELETE /api/users/550e8400-e29b-41d4-a716-446655440000/tenants/acme-corp-example-com
→ 204 No Content
# Note: acme-corp-example-com est l'URL nettoyée de https://acme-corp.example.com
```

---

### US-3.5: Consulter les Tenants d'un Utilisateur (DEVRAIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** voir la liste des tenants d'un utilisateur  
**Afin de** connaître ses accès

**Critères d'acceptation:**
- [ ] Je peux appeler GET `/api/users/{userId}/tenants`
- [ ] Le système retourne la liste des TenantIds
- [ ] Le système retourne une liste vide si aucun tenant
- [ ] Le système retourne 404 si l'utilisateur n'existe pas

**Tests d'acceptation:**
```http
GET /api/users/550e8400-e29b-41d4-a716-446655440000/tenants
→ 200 OK avec { userId, tenants: ["acme-corp", "contoso"] }
```

---

## 🔐 Epic 4: Onboarding et Activation

### US-4.1: Afficher le Formulaire d'Onboarding avec Branding (DOIT AVOIR)
**En tant qu'** utilisateur final  
**Je veux** voir un formulaire d'inscription personnalisé  
**Afin de** créer un compte dans l'organisation

**Critères d'acceptation:**
- [ ] Je peux accéder à GET `/account/onboarding?acr_values=tenant:acme-corp`
- [ ] Le système extrait le tenantId depuis acr_values
- [ ] Le système charge les informations du tenant
- [ ] Le système affiche le formulaire avec le branding (logo, couleurs)
- [ ] Le formulaire contient: email, firstName, lastName
- [ ] Le système retourne 400 Bad Request si aucun tenant spécifié
- [ ] Le système retourne 400 Bad Request si le tenant n'existe pas ou inactif

**Tests d'acceptation:**
```
GET /account/onboarding?acr_values=tenant:acme-corp
→ 200 OK avec vue HTML brandée
```

**DoD:**
- Vue Razor créée avec OnboardingViewModel
- Branding CSS appliqué dynamiquement
- Validation des paramètres acr_values
- Tests E2E avec Playwright ou Selenium

---

### US-4.2: Soumettre une Demande d'Onboarding (DOIT AVOIR)
**En tant qu'** utilisateur final  
**Je veux** soumettre ma demande de création de compte  
**Afin que** l'application tierce valide ma demande

**Critères d'acceptation:**
- [ ] Je peux soumettre POST `/account/onboarding` avec OnboardingViewModel
- [ ] Le système valide que l'email n'existe pas déjà
- [ ] Le système génère un requestId unique
- [ ] Le système envoie une notification HTTP POST à l'app tierce
- [ ] La notification contient: requestId, tenantId, email, firstName, lastName
- [ ] Le système affiche la page "En attente de validation"
- [ ] Le système retourne une erreur si l'email existe déjà
- [ ] Le système ne crée PAS l'utilisateur (c'est l'app tierce qui le fera)
- [ ] La notification inclut une signature HMAC (X-Johodp-Signature) (Ref UC-04 RG-ONBOARD-02)
- [ ] L'app tierce doit répondre sous 5 minutes (timeout) (Ref UC-04 RG-ONBOARD-03)
- [ ] Message d'erreur spécifique en cas de timeout (RG-ONBOARD-04)
- [ ] Flux asynchrone: création via `/api/users/register` si validation réussie

**Tests d'acceptation:**
```http
POST /account/onboarding
{
  "tenantId": "acme-corp",
  "email": "john.doe@acme.com",
  "firstName": "John",
  "lastName": "Doe"
}
→ 200 OK avec vue "OnboardingPending"
```

**DoD:**
- AccountController.Onboarding() POST implémenté
- NotificationService.NotifyAccountRequestAsync() créé
- Tests unitaires avec mock de INotificationService
- Tests d'intégration avec webhook simulé
- Documentation du format de notification

---

### US-4.3: Afficher le Formulaire d'Activation (DOIT AVOIR)
**En tant qu'** utilisateur final  
**Je veux** activer mon compte via le lien reçu par email  
**Afin de** définir mon mot de passe et accéder au système

**Critères d'acceptation:**
- [ ] Je peux accéder à GET `/account/activate?token=<token>&userId=<guid>&tenant=acme-corp`
- [ ] Le système vérifie que l'utilisateur existe
- [ ] Le système vérifie que l'utilisateur est en statut PendingActivation
- [ ] Le système charge le branding du tenant
- [ ] Le système affiche l'email masqué (ex: j***n@example.com)
- [ ] Le formulaire contient: password, confirmPassword
- [ ] Le système retourne 400 Bad Request si token ou userId manquant
- [ ] Le système retourne 400 Bad Request si l'utilisateur n'est pas en PendingActivation

**Tests d'acceptation:**
```
GET /account/activate?token=ABC123&userId=550e8400-e29b-41d4-a716-446655440000&tenant=acme-corp
→ 200 OK avec vue d'activation brandée
```

---

### US-4.4: Activer un Compte Utilisateur (DOIT AVOIR)
**En tant qu'** utilisateur final  
**Je veux** définir mon mot de passe et activer mon compte  
**Afin de** pouvoir me connecter

**Critères d'acceptation:**
- [ ] Je peux soumettre POST `/account/activate` avec ActivateViewModel
- [ ] Le système vérifie le token via UserManager.VerifyUserTokenAsync
- [ ] Le système hache le mot de passe avec IPasswordHasher
- [ ] Le système appelle user.SetPasswordHash(hash)
- [ ] Le système appelle user.Activate() (déclenche UserActivatedEvent)
- [ ] Le système confirme l'email via UserManager.ConfirmEmailAsync
- [ ] Le système change Status de PendingActivation à Active
- [ ] Le système connecte automatiquement l'utilisateur
- [ ] Le système affiche la page de succès
- [ ] Le système retourne une erreur si le token est invalide ou expiré
- [ ] Le système retourne une erreur si les mots de passe ne correspondent pas
- [ ] Token utilisable une seule fois, expiration configurable (24h) (Ref UC-05 RG-ACTIVATE-02)

**Tests d'acceptation:**
```http
POST /account/activate
{
  "token": "ABC123",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "tenantId": "acme-corp",
  "newPassword": "SecureP@ss123",
  "confirmPassword": "SecureP@ss123"
}
→ 200 OK avec vue "ActivateSuccess" + cookie de session
```

**DoD:**
- AccountController.Activate() POST implémenté
- User.Activate() dans domain avec événement
- Tests unitaires pour validation token
- Tests d'intégration E2E complets
- Vérification que l'utilisateur est connecté après activation

---

### US-4.5: Activer un Compte via API (DEVRAIT AVOIR)
**En tant qu'** application mobile  
**Je veux** activer un compte via API  
**Afin de** permettre l'activation sans navigateur web

**Critères d'acceptation:**
- [ ] Je peux envoyer POST `/api/account/activate` avec ActivateApiRequest
- [ ] L'endpoint ne requiert pas de token anti-forgery (AllowAnonymous)
- [ ] Le système effectue les mêmes validations que la version web
- [ ] Le système retourne un objet JSON avec userId, email, status
- [ ] Le système NE connecte PAS l'utilisateur (pas de cookie)
- [ ] Le système retourne 400 Bad Request avec détails d'erreur

**Tests d'acceptation:**
```http
POST /api/account/activate
{
  "token": "ABC123",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "tenantId": "acme-corp-example-com",
  "newPassword": "SecureP@ss123",
  "confirmPassword": "SecureP@ss123"
}
→ 200 OK avec { userId, email, status: "Active" }
# Note: tenantId est l'URL nettoyée (https://acme-corp.example.com → acme-corp-example-com)
```

---

## 🔑 Epic 5: Authentification et Session

### US-5.1: Afficher le Formulaire de Connexion (DOIT AVOIR)
**En tant qu'** utilisateur final  
**Je veux** voir un formulaire de connexion  
**Afin de** m'authentifier dans le système

**Critères d'acceptation:**
- [ ] Je peux accéder à GET `/account/login?returnUrl=<url>`
- [ ] Le système extrait le tenantId depuis acr_values dans returnUrl
- [ ] Le système affiche le formulaire avec email et password
- [ ] Le formulaire inclut le branding si un tenant est détecté
- [ ] Le returnUrl est préservé dans ViewData

**Tests d'acceptation:**
```
GET /account/login?returnUrl=/connect/authorize?acr_values=tenant:acme-corp-example-com
→ 200 OK avec formulaire de login
# Note: acme-corp-example-com dérivé de https://acme-corp.example.com
```

---

### US-5.2: Se Connecter avec Email et Mot de Passe (DOIT AVOIR)
**En tant qu'** utilisateur final  
**Je veux** me connecter avec mon email et mot de passe  
**Afin d'** accéder à mes ressources

**Critères d'acceptation:**
- [ ] Je peux soumettre POST `/account/login` avec LoginViewModel
- [ ] Le système extrait le tenantId depuis acr_values dans returnUrl
- [ ] Le système vérifie les credentials via UserManager.CheckPasswordAsync
- [ ] Le système vérifie que l'utilisateur a accès au tenant demandé
- [ ] Le système crée une session avec cookie "Cookies" (7 jours)
- [ ] Le système redirige vers returnUrl en cas de succès
- [ ] Le système retourne une erreur si credentials invalides
- [ ] Le système retourne une erreur si l'utilisateur n'a pas accès au tenant
- [ ] Le système détecte si MFA est requis (user.RequiresMFA())
- [ ] Refuse connexion si utilisateur sans tenant (Ref UC-06 / UC-09 RG-MULTITENANT-04)

**Tests d'acceptation:**
```http
POST /account/login
{
  "email": "john.doe@acme.com",
  "password": "SecureP@ss123"
}
→ 302 Redirect vers returnUrl + cookie de session
```

**DoD:**
- AccountController.Login() POST implémenté
- CustomSignInManager vérifie MFA
- Validation de l'accès tenant
- Tests E2E avec différents scénarios
- Log des tentatives de connexion

---

### US-5.3: Se Connecter via API (DEVRAIT AVOIR)
**En tant qu'** application mobile  
**Je veux** me connecter via API  
**Afin d'** obtenir une session pour les appels suivants

**Critères d'acceptation:**
- [ ] Je peux envoyer POST `/api/auth/login` avec LoginApiRequest
- [ ] Le système extrait le tenantId depuis query param acr_values
- [ ] Le système valide les credentials
- [ ] Le système vérifie l'accès au tenant
- [ ] Le système crée un cookie de session
- [ ] Le système retourne JSON { message, email }
- [ ] Le système retourne 401 Unauthorized si credentials invalides
- [ ] Vérifie tenantId présent dans TenantIds (Ref UC-06 / UC-09)

**Tests d'acceptation:**
```http
POST /api/auth/login?acr_values=tenant:acme-corp-example-com
{
  "email": "john.doe@acme.com",
  "password": "SecureP@ss123"
}
→ 200 OK avec { message: "Login successful", email: "..." }
# Note: acme-corp-example-com dérivé de https://acme-corp.example.com
```

---

### US-5.4: Se Déconnecter (DOIT AVOIR)
**En tant qu'** utilisateur final  
**Je veux** me déconnecter  
**Afin de** terminer ma session de manière sécurisée

**Critères d'acceptation:**
- [ ] Je peux accéder à GET `/account/logout`
- [ ] Le système efface le cookie "Cookies"
- [ ] Le système efface le cookie "oidc" (IdentityServer)
- [ ] Le système redirige vers la page de login
- [ ] Les tokens IdentityServer sont révoqués

**Tests d'acceptation:**
```
GET /account/logout
→ 302 Redirect vers /account/login + cookies effacés
```

---

### US-5.5: Demander une Réinitialisation de Mot de Passe (DEVRAIT AVOIR)
**En tant qu'** utilisateur final  
**Je veux** demander un lien de réinitialisation  
**Afin de** récupérer l'accès à mon compte

**Critères d'acceptation:**
- [ ] Je peux accéder à GET `/account/forgot-password`
- [ ] Je peux soumettre POST `/account/forgot-password` avec ForgotPasswordViewModel
- [ ] Le système génère un token via UserManager.GeneratePasswordResetTokenAsync
- [ ] En DEV: Le token est affiché dans la console
- [ ] En PROD: Un email est envoyé avec le lien de reset
- [ ] Le système affiche la page de confirmation
- [ ] Le système ne révèle pas si l'email existe (sécurité)

**Tests d'acceptation:**
```http
POST /account/forgot-password
{
  "email": "john.doe@acme.com"
}
→ 302 Redirect vers /account/forgot-password-confirmation
```

---

### US-5.6: Réinitialiser un Mot de Passe (DEVRAIT AVOIR)
**En tant qu'** utilisateur final  
**Je veux** définir un nouveau mot de passe  
**Afin de** récupérer l'accès à mon compte

**Critères d'acceptation:**
- [ ] Je peux accéder à GET `/account/reset-password?token=<token>`
- [ ] Le formulaire contient: email, password, confirmPassword
- [ ] Je peux soumettre POST `/account/reset-password` avec ResetPasswordViewModel
- [ ] Le système valide le token
- [ ] Le système réinitialise le mot de passe via UserManager.ResetPasswordAsync
- [ ] Le système affiche la page de confirmation
- [ ] Le système retourne une erreur si le token est invalide ou expiré
- [ ] Le système retourne une erreur si les mots de passe ne correspondent pas

**Tests d'acceptation:**
```http
POST /account/reset-password
{
  "email": "john.doe@acme.com",
  "token": "ABC123",
  "password": "NewSecureP@ss123",
  "confirmPassword": "NewSecureP@ss123"
}
→ 302 Redirect vers /account/reset-password-confirmation
```

---

## 🔗 Epic 6: Intégration IdentityServer

### US-6.1: Charger un Client Dynamiquement depuis la Base (DOIT AVOIR)
**En tant qu'** IdentityServer  
**Je veux** charger un client depuis CustomClientStore  
**Afin d'** utiliser la configuration dynamique

**Critères d'acceptation:**
- [ ] IdentityServer appelle CustomClientStore.FindClientByIdAsync(clientName)
- [ ] Le système récupère le Client depuis la base de données
- [ ] Le système récupère TOUS les tenants associés
- [ ] Le système agrège RedirectUris depuis tous les AllowedReturnUrls des tenants
- [ ] Le système agrège AllowedCorsOrigins depuis tous les AllowedCorsOrigins des tenants
- [ ] **⚠️ CORS protège UNIQUEMENT les navigateurs (pas curl/Postman/applications serveur)**
- [ ] Les CORS origins sont normalisées (schéma + autorité, pas de path)
- [ ] Le système déduplique les URLs
- [ ] Le système retourne null si le client n'a aucun tenant
- [ ] Le système retourne null si aucun tenant n'a de redirect URIs
- [ ] Le système mappe vers Duende.IdentityServer.Models.Client
- [ ] Ref UC-03 pour agrégation dynamique sans cache

**Tests d'acceptation:**
```csharp
var client = await customClientStore.FindClientByIdAsync("my-spa-app");
Assert.NotNull(client);
Assert.Contains("http://localhost:4200/callback", client.RedirectUris);
Assert.Equal(GrantTypes.Code, client.AllowedGrantTypes);
Assert.True(client.RequirePkce);
```

**DoD:**
- CustomClientStore.FindClientByIdAsync() implémenté
- Agrégation des redirect URIs et CORS origins
- Tests unitaires avec plusieurs tenants
- Tests d'intégration avec base de données
- Logging des clients null (sécurité)

---

### US-6.2: Valider une Redirect URI OAuth2 (DOIT AVOIR)
**En tant qu'** IdentityServer  
**Je veux** valider les redirect URIs  
**Afin de** prévenir les attaques Open Redirect

**Critères d'acceptation:**
- [ ] IdentityServer reçoit une redirect_uri dans la requête /authorize
- [ ] Le système charge le client via CustomClientStore
- [ ] Le système vérifie que redirect_uri est dans client.RedirectUris
- [ ] Le système refuse la requête si redirect_uri n'est pas autorisée
- [ ] Le système retourne une erreur OAuth2 "invalid_request"

**Tests d'acceptation:**
```http
GET /connect/authorize?redirect_uri=http://evil.com/callback
→ 400 Bad Request avec error=invalid_request
```

---

### US-6.3: Générer un Authorization Code avec PKCE (DOIT AVOIR)
**En tant qu'** IdentityServer  
**Je veux** générer un code d'autorisation après authentification  
**Afin de** permettre le flux Authorization Code

**Critères d'acceptation:**
- [ ] L'utilisateur est authentifié (cookie de session valide)
- [ ] IdentityServer reçoit une requête /authorize avec code_challenge
- [ ] Le système valide le client et la redirect_uri
- [ ] Le système génère un authorization_code unique
- [ ] Le système stocke code_challenge associé au code
- [ ] Le système redirige vers redirect_uri?code=<code>
- [ ] Le code expire après 5 minutes

**Tests d'acceptation:**
```http
GET /connect/authorize?client_id=my-spa-app&response_type=code&code_challenge=xyz&redirect_uri=...
→ 302 Redirect vers http://localhost:4200/callback?code=ABC123
```

---

### US-6.4: Échanger un Code contre des Tokens (DOIT AVOIR)
**En tant qu'** application SPA  
**Je veux** échanger mon authorization code contre des tokens  
**Afin d'** obtenir un access_token et refresh_token

**Critères d'acceptation:**
- [ ] Je peux envoyer POST `/connect/token` avec grant_type=authorization_code
- [ ] Le body contient: code, redirect_uri, client_id, code_verifier
- [ ] Le système vérifie que le code est valide et non expiré
- [ ] Le système valide PKCE: SHA256(code_verifier) == code_challenge
- [ ] Le système génère un access_token JWT signé
- [ ] Le système génère un refresh_token
- [ ] Le système génère un id_token JWT (OIDC)
- [ ] Le système retourne JSON avec tokens et expires_in
- [ ] Le système révoque le code (usage unique)

**Tests d'acceptation:**
```http
POST /connect/token
{
  "grant_type": "authorization_code",
  "code": "ABC123",
  "redirect_uri": "http://localhost:4200/callback",
  "client_id": "my-spa-app",
  "code_verifier": "original_verifier"
}
→ 200 OK avec { access_token, refresh_token, id_token, expires_in }
```

**DoD:**
- Validation PKCE implémentée
- Génération de tokens JWT
- Signature avec clé RSA
- Claims inclus dans tokens (sub, email, role, scope)
- Tests unitaires pour validation PKCE
- Tests d'intégration E2E complets

---

### US-6.5: Valider un Access Token JWT (DOIT AVOIR)
**En tant qu'** API Johodp  
**Je veux** valider les access tokens JWT  
**Afin de** protéger mes endpoints

**Critères d'acceptation:**
- [ ] Le middleware JWT vérifie la signature du token
- [ ] Le middleware vérifie que le token n'est pas expiré (exp claim)
- [ ] Le middleware vérifie l'issuer (iss = IdentityServer URL)
- [ ] Le middleware vérifie l'audience (aud = API)
- [ ] Le middleware extrait les claims (sub, email, role, scope)
- [ ] Le middleware peuple HttpContext.User avec les claims
- [ ] Le middleware retourne 401 Unauthorized si validation échoue

**Tests d'acceptation:**
```http
GET /api/users/me
Authorization: Bearer eyJ...
→ 200 OK avec données utilisateur (si token valide)
→ 401 Unauthorized (si token invalide/expiré)
```

---

### US-6.6: Renouveler un Access Token avec Refresh Token (DOIT AVOIR)
**En tant qu'** application SPA  
**Je veux** renouveler mon access token expirant grâce à un refresh token  
**Afin de** maintenir la session sans ré-authentification (Ref UC-08)

**Critères d'acceptation:**
- [ ] Je peux envoyer POST `/connect/token` avec `grant_type=refresh_token`
- [ ] Le body contient refresh_token et client_id
- [ ] Le système valide que le refresh_token n'est pas expiré
- [ ] Le système valide que le refresh_token n'est pas révoqué
- [ ] Le système valide correspondance du client
- [ ] Le système révoque l'ancien refresh_token (usage unique)
- [ ] Le système retourne nouvel access_token + nouveau refresh_token + expires_in
- [ ] Le système applique fenêtre glissante (15 jours) sur le refresh_token
- [ ] Retourne 400 ou 401 si token invalide/expiré/révoqué

**Tests d'acceptation:**
```http
POST /connect/token
{
  "grant_type": "refresh_token",
  "refresh_token": "rft123",
  "client_id": "my-spa-app"
}
→ 200 OK avec nouveaux tokens
```

**DoD:**
- Validation one-time use en place
- Révocation précédente entrée persistée
- Tests unitaires (expiration, révocation, renouvellement)
- Documentation mise à jour

---

### US-6.7: Appeler une API protégée avec Access Token (DOIT AVOIR)
**En tant qu'** application SPA  
**Je veux** accéder à un endpoint protégé avec un access token valide  
**Afin de** récupérer des données sécurisées (Ref UC-07)

**Critères d'acceptation:**
- [ ] Je peux appeler GET `/api/users/me` avec header Authorization Bearer
- [ ] Middleware vérifie signature, expiration, issuer, audience
- [ ] Le système extrait claims (sub, email, role, scope)
- [ ] Retourne 200 avec UserDto si valide
- [ ] Retourne 401 en cas d'échec de validation

**Tests d'acceptation:**
```http
GET /api/users/me
Authorization: Bearer eyJ...
→ 200 OK
```

**DoD:**
- Tests d'intégration token valide/expiré
- Documentation sécurité (SEC-01..SEC-05) référencée

---

## 🛠️ Epic 7: Authentification Machine-to-Machine

### US-7.1: Obtenir un Token d'Administration (Client Credentials) (DOIT AVOIR)
**En tant qu'** application tierce  
**Je veux** obtenir un access token via le flux client credentials  
**Afin que** je puisse appeler les APIs d'administration (Ref UC-00)

**Critères d'acceptation:**
- [ ] Je peux envoyer POST `/connect/token` avec `grant_type=client_credentials`
- [ ] Le système valide client_id + client_secret
- [ ] Le système vérifie autorisation du scope demandé (ex: `johodp.admin`)
- [ ] Le système génère access_token (exp 1h) sans refresh_token
- [ ] Retourne 401 si client_secret invalide
- [ ] Scope `johodp.admin` permet création clients, tenants, utilisateurs

**Tests d'acceptation:**
```http
POST /connect/token
{
  "grant_type": "client_credentials",
  "client_id": "third-party-app",
  "client_secret": "s3cr3t",
  "scope": "johodp.admin"
}
→ 200 OK avec access_token
```

**DoD:**
- Stockage sécurisé du client_secret (hashé)
- Journalisation du client_id pour audit
- Tests unitaires validation scope/secret
- Documentation mise à jour (API_ENDPOINTS.md)
**En tant qu'** application SPA  
**Je veux** renouveler mon access token  
**Afin de** maintenir ma session sans redemander credentials

**Critères d'acceptation:**
- [ ] Je peux envoyer POST `/connect/token` avec grant_type=refresh_token
- [ ] Le body contient: refresh_token, client_id
- [ ] Le système vérifie que le refresh_token est valide et non révoqué
- [ ] Le système génère un NOUVEAU access_token
- [ ] Le système génère un NOUVEAU refresh_token
- [ ] Le système révoque l'ancien refresh_token (one-time use)
- [ ] Le système retourne JSON avec nouveaux tokens
- [ ] Le système retourne 400 Bad Request si refresh_token invalide

**Tests d'acceptation:**
```http
POST /connect/token
{
  "grant_type": "refresh_token",
  "refresh_token": "old_token",
  "client_id": "my-spa-app"
}
→ 200 OK avec { access_token: "new", refresh_token: "new", expires_in: 3600 }
```

**DoD:**
- RefreshTokenUsage = OneTimeOnly configuré
- Tests de renouvellement multiples
- Tests de révocation de refresh_tokens
- Vérification sliding expiration (15 jours)

---

### US-6.7: Stocker les Tokens de manière Persistante (DOIT AVOIR)
**En tant qu'** IdentityServer  
**Je veux** stocker les tokens dans PostgreSQL  
**Afin de** supporter la scalabilité et le clustering

**Critères d'acceptation:**
- [ ] Les authorization codes sont stockés dans PersistedGrants
- [ ] Les refresh tokens sont stockés dans PersistedGrants
- [ ] Les device codes sont stockés dans DeviceCodes
- [ ] Les clés de signature sont stockées dans Keys
- [ ] Le cleanup automatique s'exécute toutes les heures (3600s)
- [ ] Les tokens expirés sont supprimés automatiquement
- [ ] Les tokens révoqués sont supprimés de la base

**Tests d'acceptation:**
```sql
SELECT * FROM "PersistedGrants" WHERE "Type" = 'refresh_token';
→ Refresh tokens présents
```

**DoD:**
- Duende.IdentityServer.EntityFramework.Storage configuré
- Migration AddIdentityServerOperationalStore appliquée
- Tests de cleanup automatique
- Tests de révocation de tokens
- Configuration CleanupOptions

---

## 🔔 Epic 7: Notifications

### US-7.1: Envoyer une Notification à l'Application Tierce (DOIT AVOIR)
**En tant que** système Johodp  
**Je veux** notifier l'application tierce lors d'une demande d'onboarding  
**Afin que** l'app puisse valider et créer l'utilisateur

**Critères d'acceptation:**
- [ ] Le système appelle INotificationService.NotifyAccountRequestAsync
- [ ] Le service obtient un `access_token` depuis un **IdP externe** via Client Credentials
- [ ] Configuration IdP externe dans appsettings: `ExternalIdP:Authority`, `ExternalIdP:ClientId`, `ExternalIdP:ClientSecret`, `ExternalIdP:Scope`
- [ ] Le service demande le scope configuré (ex: `webhook.notify` ou scope spécifique par tenant)
- [ ] Le service envoie POST vers `tenant.WebhookUrl` avec `Authorization: Bearer <token>`
- [ ] Le body contient: `requestId`, `tenantId`, `email`, `firstName`, `lastName`, `timestamp`
- [ ] Le token est mis en cache (IMemoryCache/IDistributedCache) et renouvelé automatiquement avant expiration
- [ ] L'appel est asynchrone (fire-and-forget avec queue si échec)
- [ ] Le système retente 3 fois avec backoff exponentiel (1s, 2s, 4s) en cas d'échec réseau
- [ ] Le système log les succès/échecs avec: `requestId`, `webhookUrl`, `statusCode`, `duration`, `retries`, `idp_issuer`
- [ ] Le système ne bloque pas l'onboarding en cas d'échec webhook
- [ ] Le système stocke les webhooks échoués dans une dead-letter queue pour retry manuel
- [ ] Si l'IdP externe retourne 401/403, le service log une alerte critique (mauvaise config)

**Configuration appsettings.json:**
```json
{
  "ExternalIdP": {
    "Authority": "https://external-idp.example.com",
    "ClientId": "johodp-webhook-client",
    "ClientSecret": "external-secret-xyz",
    "Scope": "webhook.notify",
    "TokenEndpoint": "https://external-idp.example.com/oauth/token"
  },
  "Webhook": {
    "TimeoutSeconds": 5,
    "MaxRetries": 3,
    "BackoffSeconds": [1, 2, 4]
  }
}
```

**Tests d'acceptation:**
```http
# 1. Johodp obtient token depuis IdP EXTERNE
POST https://external-idp.example.com/oauth/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&
client_id=johodp-webhook-client&
client_secret=external-secret-xyz&
scope=webhook.notify

→ 200 OK 
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "scope": "webhook.notify"
}

# Token JWT décodé (émis par IdP externe):
{
  "iss": "https://external-idp.example.com",
  "aud": "third-party-webhooks",
  "sub": "johodp-webhook-client",
  "client_id": "johodp-webhook-client",
  "scope": "webhook.notify",
  "exp": 1732534200,
  "iat": 1732530600
}

# 2. Johodp appelle webhook avec token de l'IdP externe
POST https://app.acme.com/api/account-requests
Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "requestId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "tenantId": "acme-corp",
  "email": "john.doe@acme.com",
  "firstName": "John",
  "lastName": "Doe",
  "timestamp": "2025-11-25T10:30:00Z"
}

→ App tierce valide token JWT (issuer = IdP externe) puis retourne 200 OK
```

**DoD:**
- INotificationService interface créée avec méthode NotifyAccountRequestAsync
- NotificationService implémentation avec:
  - HttpClient configuré avec Polly retry policy (3 tentatives + circuit breaker)
  - OAuth2 client pour IdP externe (IdentityModel.Client ou custom HttpClient)
  - Token manager avec cache distribué (Redis) pour support multi-instances
  - Dead-letter queue pour webhooks échoués (table DB `WebhookFailures` ou Redis Stream)
- Configuration appsettings avec section `ExternalIdP`
- Validation configuration au démarrage (Authority accessible, credentials valides)
- Tests unitaires avec mock HttpClient + token expiré/invalide
- Tests d'intégration avec IdP externe en staging + webhook simulé
- Logging structuré (Serilog) avec enrichers TenantId/RequestId + `idp_issuer`
- Monitoring métriques (taux succès webhook, taux succès IdP, durée, retries) via Prometheus
- Alertes si taux échec IdP > 5% (credentials expirés/révoqués)
- Documentation architecture avec diagramme séquence (Johodp → IdP Externe → App Tierce)

---

## 🏗️ Epic 10: User Stories pour l'Application Tierce (Webhook Consumer)

> **Contexte:** L'application tierce reçoit des notifications de Johodp lors des demandes d'inscription (onboarding). Elle doit valider la demande selon ses règles métier, puis créer l'utilisateur dans Johodp via l'API si accepté.

---

### US-10.1: Recevoir une Notification d'Onboarding (DOIT AVOIR)
**En tant qu'** application tierce  
**Je veux** recevoir un webhook POST lors d'une demande d'inscription  
**Afin de** valider la demande selon mes règles métier

**Critères d'acceptation:**
- [ ] Mon endpoint `POST /api/account-requests` reçoit le payload JSON
- [ ] Le payload contient: `requestId`, `tenantId`, `email`, `firstName`, `lastName`, `timestamp`
- [ ] L'endpoint est **protégé par OAuth2** (nécessite Bearer token valide)
- [ ] Je valide le token JWT reçu: signature, expiration, issuer (Johodp), audience, scope (`johodp.webhook`)
- [ ] Je vérifie que le `tenantId` correspond à mon organisation
- [ ] Je vérifie le `timestamp` (< 5 minutes pour prévenir replay)
- [ ] Je retourne `200 OK` immédiatement (< 5s) pour accuser réception
- [ ] Je retourne `401 Unauthorized` si token invalide/expiré
- [ ] Je lance un traitement asynchrone pour validation métier
- [ ] Je log la réception avec `requestId`, `email`, `tenantId`, timestamp, IP source, claims JWT

**Payload reçu:**
```json
{
  "requestId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "tenantId": "acme-corp",
  "email": "john.doe@acme.com",
  "firstName": "John",
  "lastName": "Doe",
  "timestamp": "2025-11-25T10:30:00Z"
}
```

**Headers reçus:**
```
Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json
User-Agent: Johodp/1.0
```

**Token JWT décodé:**
```json
{
  "iss": "https://johodp.example.com",
  "aud": "third-party-webhook",
  "sub": "johodp-notification-service",
  "scope": "johodp.webhook",
  "client_id": "johodp-internal",
  "exp": 1732534200,
  "iat": 1732530600
}
```

**DoD:**
- Endpoint webhook implémenté avec validation JWT OAuth2
- Configuration JWT authentication middleware (issuer, audience, signing key)
- Traitement asynchrone (queue/background job)
- Logging structuré avec `requestId` + claims JWT
- Tests unitaires validation token (expiré, signature invalide, scope manquant)
- Tests d'intégration avec token simulé
- Documentation endpoint webhook + format token

---

### US-10.2: Valider une Demande selon Règles Métier (DOIT AVOIR)
**En tant qu'** application tierce  
**Je veux** valider la demande d'inscription selon mes règles  
**Afin de** décider si je crée l'utilisateur dans Johodp

**Critères d'acceptation:**
- [ ] Je vérifie que l'email respecte le format de mon organisation (ex: `@acme.com`)
- [ ] Je vérifie que l'email n'existe pas déjà dans mon CRM
- [ ] Je vérifie que le domaine email est autorisé (whitelist/blacklist)
- [ ] Je peux appliquer des règles personnalisées (ex: département, rôle)
- [ ] Je peux rejeter la demande avec un motif (email invalide, domaine non autorisé, doublon)
- [ ] Je log la décision avec: `requestId`, `decision` (accepted/rejected), `reason`, `duration`
- [ ] Je stocke la demande dans ma base avec statut `pending_validation`

**Exemples de règles:**
```typescript
// Règle 1: Email domaine autorisé
if (!email.endsWith('@acme.com')) {
  reject('INVALID_DOMAIN');
}

// Règle 2: Pas de doublon CRM
if (await crmService.userExists(email)) {
  reject('DUPLICATE_CRM');
}

// Règle 3: Vérifier liste noire
if (await blacklist.contains(email)) {
  reject('BLACKLISTED');
}
```

**DoD:**
- Moteur de règles configurables (JSON/YAML)
- Logging des décisions avec raison
- Tests unitaires par règle
- Métriques (% accepté/rejeté, durée validation)

---

### US-10.3: Créer un Utilisateur dans Johodp via API (DOIT AVOIR)
**En tant qu'** application tierce  
**Je veux** créer l'utilisateur dans Johodp si la validation réussit  
**Afin que** l'utilisateur reçoive l'email d'activation

**Critères d'acceptation:**
- [ ] J'obtiens un `access_token` via **Client Credentials** (`grant_type=client_credentials`)
- [ ] J'utilise mon `client_id` et `client_secret` configurés dans Johodp
- [ ] Je demande le scope `johodp.admin` pour accès API création utilisateurs
- [ ] Je cache le token et le rafraîchis avant expiration (exp - 5 min)
- [ ] J'appelle `POST /api/users/register` avec `Authorization: Bearer <token>`
- [ ] Le body contient: `email`, `firstName`, `lastName`, `tenantId`, `createAsPending=true`
- [ ] Johodp valide le token JWT (signature, expiration, scope `johodp.admin`)
- [ ] Johodp crée l'utilisateur avec `Status=PendingActivation`
- [ ] Johodp génère le token d'activation et envoie l'email
- [ ] Je reçois `201 Created` avec `userId`, `email`, `status`
- [ ] Je mets à jour ma base: `requestId` → `userId`, `status=user_created`
- [ ] Je log le succès avec: `requestId`, `userId`, `email`, `duration`, `token_age`
- [ ] En cas d'échec (409 Conflict, 400 Bad Request, 401 Unauthorized), je log l'erreur et notifie l'admin
- [ ] En cas de 401, je force le renouvellement du token et retry une fois

**Appels API:**
```http
# 1. Obtenir access token via Client Credentials
POST https://johodp.example.com/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&
client_id=third-party-app&
client_secret=s3cr3tK3y123!&
scope=johodp.admin

→ 200 OK 
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "scope": "johodp.admin"
}

# Token JWT décodé:
{
  "iss": "https://johodp.example.com",
  "aud": "johodp-api",
  "sub": "third-party-app",
  "client_id": "third-party-app",
  "scope": "johodp.admin",
  "exp": 1732534200,
  "iat": 1732530600
}

# 2. Créer utilisateur avec token
POST https://johodp.example.com/api/users/register
Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "email": "john.doe@acme.com",
  "firstName": "John",
  "lastName": "Doe",
  "tenantId": "acme-corp",
  "createAsPending": true
}

→ 201 Created 
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "john.doe@acme.com",
  "status": "PendingActivation"
}
```

**DoD:**
- HttpClient configuré avec retry policy (Polly) - 3 tentatives avec backoff exponentiel
- Service de gestion token avec cache mémoire (expiration - 5 min)
- Renouvellement automatique du token si 401 Unauthorized
- Logging des appels API (request/response, duration, token_claims)
- Tests unitaires avec mock HttpClient + token expiré/invalide
- Tests d'intégration avec Johodp en staging (flow complet)
- Gestion des erreurs 409 (doublon), 400 (validation), 401 (token expiré/invalide), 403 (scope insuffisant), 500 (erreur serveur)
- Monitoring durée appels + taux erreur par endpoint

---

### US-10.4: Gérer le Timeout de Validation (DEVRAIT AVOIR)
**En tant qu'** application tierce  
**Je veux** gérer le timeout de validation (5 min)  
**Afin de** ne pas bloquer le système Johodp

**Critères d'acceptation:**
- [ ] Si ma validation dépasse 5 minutes, Johodp affiche un message d'erreur à l'utilisateur
- [ ] Je peux quand même créer l'utilisateur après le timeout (création asynchrone)
- [ ] Je log le timeout avec: `requestId`, `duration`, `reason`
- [ ] Je stocke la demande avec statut `timeout_validation`
- [ ] Je peux re-traiter manuellement les demandes en timeout
- [ ] Je notifie l'admin en cas de timeout répétés

**DoD:**
- Mécanisme de retry manuel (dashboard admin)
- Alertes automatiques (email/Slack) si > 10% timeout
- Monitoring durée validation (P50, P95, P99)

---

### US-10.5: Rejeter une Demande et Notifier l'Utilisateur (DEVRAIT AVOIR)
**En tant qu'** application tierce  
**Je veux** rejeter une demande invalide et notifier l'utilisateur  
**Afin que** l'utilisateur comprenne pourquoi sa demande a été refusée

**Critères d'acceptation:**
- [ ] Si je rejette la demande, je stocke le motif dans ma base
- [ ] J'envoie un email à l'utilisateur avec le motif (ex: "Domaine email non autorisé")
- [ ] Je log le rejet avec: `requestId`, `email`, `reason`, `timestamp`
- [ ] L'utilisateur peut contacter le support avec le `requestId` pour clarification
- [ ] Je peux configurer des motifs de rejet personnalisés par tenant

**Exemple d'email de rejet:**
```
Objet: Demande d'inscription refusée - ACME Corp

Bonjour John,

Votre demande d'inscription (ID: a1b2c3d4) a été refusée pour la raison suivante:
"Domaine email non autorisé. Veuillez utiliser une adresse @acme.com."

Si vous pensez qu'il s'agit d'une erreur, contactez notre support à support@acme.com
en indiquant l'ID de demande.

Cordialement,
L'équipe ACME Corp
```

**DoD:**
- Template email configurable par motif
- Logging des rejets avec raison
- Dashboard admin pour voir les rejets
- Tests E2E avec vérification email

---

### US-10.6: Dashboard Admin pour Gérer les Demandes (DEVRAIT AVOIR)
**En tant qu'** administrateur de l'app tierce  
**Je veux** voir toutes les demandes d'onboarding  
**Afin de** monitorer et gérer manuellement les cas particuliers

**Critères d'acceptation:**
- [ ] Je peux voir la liste des demandes avec: `requestId`, `email`, `status`, `timestamp`
- [ ] Les statuts incluent: `pending_validation`, `accepted`, `rejected`, `timeout_validation`, `user_created`
- [ ] Je peux filtrer par statut, tenant, date
- [ ] Je peux rechercher par email ou requestId
- [ ] Je peux voir les détails d'une demande (payload, décision, logs)
- [ ] Je peux re-traiter manuellement une demande en timeout
- [ ] Je peux forcer l'acceptation/rejet d'une demande
- [ ] Je peux voir les métriques: nombre total, % accepté, % rejeté, durée moyenne

**DoD:**
- Dashboard web avec authentification
- API backend pour CRUD demandes
- Tests E2E avec Playwright
- Exports CSV/Excel pour reporting

---

### US-10.7: Synchroniser les Utilisateurs Existants (POURRAIT AVOIR)
**En tant qu'** application tierce  
**Je veux** synchroniser mes utilisateurs existants vers Johodp  
**Afin de** migrer vers le nouveau système d'authentification

**Critères d'acceptation:**
- [ ] Je peux lancer un script de migration en batch
- [ ] Le script lit mes utilisateurs depuis le CRM/DB
- [ ] Le script appelle `POST /api/users/register` pour chaque utilisateur
- [ ] Le script respecte un rate limit (ex: 10 req/s)
- [ ] Le script log les succès/échecs avec `email`, `userId`, `status`
- [ ] Le script génère un rapport de migration (total, succès, échecs)
- [ ] Le script gère les doublons (409 Conflict) en les ignorant
- [ ] Le script envoie automatiquement l'email d'activation pour chaque utilisateur

**DoD:**
- Script CLI avec progress bar
- Logging structuré (JSON lines)
- Rapport final avec statistiques
- Tests avec dataset simulé (1000 users)
- Documentation migration step-by-step

---

### US-10.8: Logger les Appels Webhook avec Contexte (DOIT AVOIR)
**En tant qu'** application tierce  
**Je veux** logger tous les événements webhook avec contexte complet  
**Afin de** faciliter le débogage et l'audit

**Critères d'acceptation:**
- [ ] Chaque réception webhook loggée avec:
  - `requestId`, `tenantId`, `email`, `timestamp`, `ipSource`, `userAgent`
- [ ] Chaque décision de validation loggée avec:
  - `requestId`, `decision` (accepted/rejected), `reason`, `duration`, `rules_evaluated`
- [ ] Chaque appel API Johodp loggé avec:
  - `requestId`, `method`, `endpoint`, `statusCode`, `duration`, `response`
- [ ] Les erreurs loggées avec:
  - `requestId`, `error_type`, `error_message`, `stack_trace`, `context`
- [ ] Les logs structurés en JSON pour parsing facile
- [ ] Les logs incluent TenantId et ClientId (via enricher si applicable)
- [ ] Les logs sensibles (email, nom) sont masqués en production (RGPD)

**Exemples de logs:**
```json
{
  "timestamp": "2025-11-25T10:30:45.123Z",
  "level": "INFO",
  "message": "Webhook received",
  "requestId": "a1b2c3d4",
  "tenantId": "acme-corp",
  "email": "j***n@acme.com",
  "ipSource": "192.168.1.1",
  "userAgent": "Johodp/1.0"
}

{
  "timestamp": "2025-11-25T10:30:47.456Z",
  "level": "INFO",
  "message": "Validation completed",
  "requestId": "a1b2c3d4",
  "decision": "accepted",
  "duration": 2.3,
  "rules_evaluated": ["domain_check", "crm_duplicate", "blacklist"]
}

{
  "timestamp": "2025-11-25T10:30:50.789Z",
  "level": "INFO",
  "message": "User created in Johodp",
  "requestId": "a1b2c3d4",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "j***n@acme.com",
  "apiDuration": 3.2
}
```

**DoD:**
- Logger structuré (Serilog, Winston, etc.)
- Enricher custom pour TenantId/RequestId
- Masquage PII en production
- Sink vers ElasticSearch/Seq/Loki
- Dashboard de monitoring (Grafana/Kibana)

---

### US-10.9: Métriques et Monitoring (DEVRAIT AVOIR)
**En tant qu'** administrateur de l'app tierce  
**Je veux** voir des métriques temps réel  
**Afin de** surveiller la santé du système

**Critères d'acceptation:**
- [ ] Je peux voir le nombre de demandes reçues (dernière heure, jour, semaine)
- [ ] Je peux voir le taux d'acceptation/rejet (%)
- [ ] Je peux voir la durée moyenne de validation (P50, P95, P99)
- [ ] Je peux voir le taux d'erreur API Johodp (%)
- [ ] Je peux voir le nombre de timeouts (5 min)
- [ ] Je reçois des alertes si:
  - Taux d'erreur > 5%
  - Durée validation P95 > 4 min
  - Taux timeout > 10%
  - Taux rejet > 50%
- [ ] Les métriques sont exposées via Prometheus `/metrics`

**Métriques exposées:**
```
# HELP onboarding_requests_total Total webhook requests received
# TYPE onboarding_requests_total counter
onboarding_requests_total{tenant="acme-corp",status="accepted"} 1234

# HELP onboarding_validation_duration_seconds Validation duration
# TYPE onboarding_validation_duration_seconds histogram
onboarding_validation_duration_seconds_bucket{le="1.0"} 800
onboarding_validation_duration_seconds_bucket{le="2.0"} 950
onboarding_validation_duration_seconds_bucket{le="5.0"} 1200

# HELP johodp_api_errors_total Total API errors
# TYPE johodp_api_errors_total counter
johodp_api_errors_total{endpoint="/api/users/register",status="500"} 3
```

**DoD:**
- Métriques Prometheus implémentées
- Dashboard Grafana avec alertes
- Tests de charge (100 req/s)
- Documentation monitoring

---

### US-10.10: Tests de Charge et Résilience (DEVRAIT AVOIR)
**En tant que** développeur  
**Je veux** tester la résilience de mon webhook  
**Afin de** garantir la disponibilité en production

**Critères d'acceptation:**
- [ ] Je peux simuler 100 requêtes/seconde pendant 10 minutes
- [ ] Le système répond en < 200ms (P95)
- [ ] Le système gère les pics de charge sans perte de requêtes
- [ ] Le système applique un rate limit (429 Too Many Requests) si dépassement
- [ ] Les requêtes en attente sont mises en queue (Redis/RabbitMQ)
- [ ] Les requêtes échouées sont retentées automatiquement (exponential backoff)
- [ ] Le système graceful shutdown (termine les requêtes en cours avant arrêt)

**DoD:**
- Tests de charge avec k6/Gatling/Locust
- Queue avec Redis/RabbitMQ/SQS
- Circuit breaker (Polly) pour appels Johodp API
- Tests de resilience (chaos engineering)
- Documentation scaling (horizontal/vertical)

---

## 📊 Résumé Epic 10 - Application Tierce

| User Story | Story Points | Priorité | Sprint |
|------------|--------------|----------|--------|
| US-10.1 - Recevoir webhook | 5 | DOIT AVOIR | Sprint 3 |
| US-10.2 - Valider règles métier | 8 | DOIT AVOIR | Sprint 3 |
| US-10.3 - Créer utilisateur API | 8 | DOIT AVOIR | Sprint 3 |
| US-10.4 - Gérer timeout | 3 | DEVRAIT AVOIR | Sprint 4 |
| US-10.5 - Rejeter et notifier | 5 | DEVRAIT AVOIR | Sprint 4 |
| US-10.6 - Dashboard admin | 13 | DEVRAIT AVOIR | Sprint 5 |
| US-10.7 - Migration batch | 8 | POURRAIT AVOIR | Sprint 6 |
| US-10.8 - Logging structuré | 5 | DOIT AVOIR | Sprint 3 |
| US-10.9 - Métriques monitoring | 8 | DEVRAIT AVOIR | Sprint 5 |
| US-10.10 - Tests charge | 5 | DEVRAIT AVOIR | Sprint 6 |
| **TOTAL Epic 10** | **68 SP** | - | **~3-4 sprints** |

---

## 🔗 Architecture Webhook (Référence)

```mermaid
sequenceDiagram
  participant U as Utilisateur Final
  participant Johodp as Johodp IdP
  participant ExtIdP as IdP Externe
  participant Webhook as App Tierce (Webhook)
  participant Queue as Queue (Redis/RMQ)
  participant Worker as Background Worker
  participant CRM as CRM/DB Interne
  participant JohodpAPI as Johodp API (UserManager)
  
  U->>Johodp: POST /account/onboarding
  
  Note over Johodp,ExtIdP: 1. Obtenir token depuis IdP externe
  Johodp->>ExtIdP: POST /oauth/token (client_credentials)
  ExtIdP-->>Johodp: access_token (scope: webhook.notify)
  
  Note over Johodp,Webhook: 2. Envoyer notification avec token IdP externe
  Johodp->>Webhook: POST /api/account-requests<br/>Authorization: Bearer <token_externe>
  Webhook->>Webhook: Valider JWT (issuer = IdP externe)
  Webhook->>Queue: Enqueue validation job
  Webhook-->>Johodp: 200 OK (< 5s)
  Johodp-->>U: "En attente validation"
  
  Note over Queue,Worker: 3. Traitement asynchrone par app tierce
  Queue->>Worker: Traiter job
  Worker->>CRM: Vérifier règles métier
  CRM-->>Worker: Accepté/Rejeté
  
  alt Accepté
    Note over Worker,JohodpAPI: 4. App tierce crée user dans Johodp
    Worker->>Johodp: POST /connect/token (client_credentials)
    Johodp-->>Worker: access_token (scope: johodp.admin)
    Worker->>JohodpAPI: POST /api/users/register<br/>Authorization: Bearer <token_johodp>
    JohodpAPI-->>Worker: 201 Created (userId)
    JohodpAPI->>U: Email activation
    Worker->>Worker: Log succès + Update DB
  else Rejeté
    Worker->>U: Email rejet (raison)
    Worker->>Worker: Log rejet + Update DB
  end
```

**Note Architecture:**
- **IdP Externe protège:** Johodp → App Tierce (webhooks)
- **Johodp IdP protège:** App Tierce → Johodp API (création utilisateurs)
- Deux systèmes OAuth2 distincts, deux ensembles de credentials

---

## 📊 Epic 8: Administration et Monitoring

### US-8.1: Logger les Tentatives de Connexion (DEVRAIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** voir les logs de connexion  
**Afin de** détecter les tentatives d'intrusion

**Critères d'acceptation:**
- [ ] Chaque tentative de login est loggée avec: email, tenantId, timestamp, résultat
- [ ] Les échecs sont loggés en Warning
- [ ] Les succès sont loggés en Information
- [ ] Les logs incluent l'IP source (HttpContext.Connection.RemoteIpAddress)

**Tests d'acceptation:**
```
[2025-11-24 12:00:00] [INF] Successful login for user: john.doe@acme.com, tenant: acme-corp
[2025-11-24 12:05:00] [WRN] Failed login attempt for user: hacker@evil.com
```

---

### US-8.2: Logger les Créations d'Utilisateurs (DEVRAIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** voir les logs de création d'utilisateurs  
**Afin de** tracer les inscriptions

**Critères d'acceptation:**
- [ ] Chaque création d'utilisateur est loggée avec: email, tenantId, status, timestamp
- [ ] Les notifications envoyées sont loggées
- [ ] Les activations réussies sont loggées
- [ ] Les échecs d'activation sont loggés en Error

---

### US-8.3: Logger les Appels CustomClientStore (DEVRAIT AVOIR)
**En tant qu'** administrateur système  
**Je veux** voir les logs des requêtes IdentityServer  
**Afin de** comprendre les flux OAuth2

**Critères d'acceptation:**
- [ ] Chaque appel FindClientByIdAsync est loggé avec clientName
- [ ] Les clients null (sans tenant ou sans URIs) sont loggés en Warning
- [ ] Les agrégations de redirect URIs sont loggées en Debug

---

## 🧪 Epic 9: Tests et Qualité

### US-9.1: Tests Unitaires Domain (DOIT AVOIR)
**En tant que** développeur  
**Je veux** tester la logique domain  
**Afin de** garantir la qualité du code

**Critères d'acceptation:**
- [ ] Tests pour User.Create()
- [ ] Tests pour User.Activate()
- [ ] Tests pour User.AddTenantId() / RemoveTenantId()
- [ ] Tests pour UserStatus.CanActivate(), CanLogin()
- [ ] Tests pour Client.Create()
- [ ] Tests pour Tenant.Create()
- [ ] Tests pour Tenant.AddAllowedReturnUrl() avec validation
- [ ] Tests pour Tenant.AddAllowedCorsOrigin() avec validation
- [ ] Couverture de code > 80% sur domain

---

### US-9.2: Tests d'Intégration Commands (DOIT AVOIR)
**En tant que** développeur  
**Je veux** tester les commands avec base de données  
**Afin de** vérifier les transactions

**Critères d'acceptation:**
- [ ] Tests pour CreateClientCommand avec transaction
- [ ] Tests pour CreateTenantCommand avec association bidirectionnelle
- [ ] Tests pour RegisterUserCommand avec génération token
- [ ] Tests pour UpdateTenantCommand avec changement de client
- [ ] Tests avec base de données in-memory ou Testcontainers

---

### US-9.3: Tests E2E OAuth2 Flow (DOIT AVOIR)
**En tant que** développeur  
**Je veux** tester le flux OAuth2 complet  
**Afin de** valider l'intégration IdentityServer

**Critères d'acceptation:**
- [ ] Test: Créer client → créer tenant → créer utilisateur → activer → login → authorize → token → API call
- [ ] Test: Renouvellement avec refresh_token
- [ ] Test: Révocation de refresh_token
- [ ] Test: Expiration de access_token
- [ ] Tests avec Playwright ou Selenium

---

## 📈 Priorisation et Sprints Suggérés

### Sprint 1 (2 semaines) - Fondations
- **Objectif:** API CRUD clients/tenants + base DDD
- US-1.1, US-1.2, US-1.3
- US-2.1, US-2.2, US-2.3, US-2.4
- US-9.1 (tests domain)

### Sprint 2 (2 semaines) - Gestion Utilisateurs
- **Objectif:** CRUD utilisateurs + multi-tenant
- US-3.1, US-3.2, US-3.3, US-3.4, US-3.5
- US-9.2 (tests commands)

### Sprint 3 (3 semaines) - Onboarding & Activation
- **Objectif:** Flux onboarding complet
- US-4.1, US-4.2, US-4.3, US-4.4, US-4.5
- US-7.1 (notifications)
- US-2.7, US-2.8 (branding)

### Sprint 4 (3 semaines) - Authentification
- **Objectif:** Login/logout + password reset
- US-5.1, US-5.2, US-5.3, US-5.4
- US-5.5, US-5.6 (password reset)

### Sprint 5 (4 semaines) - IdentityServer
- **Objectif:** OAuth2/OIDC complet
- US-6.1, US-6.2, US-6.3, US-6.4
- US-6.5, US-6.6, US-6.7
- US-9.3 (tests E2E)

### Sprint 6 (1 semaine) - Administration & Monitoring
- **Objectif:** Logs et observabilité
- US-8.1, US-8.2, US-8.3
- US-1.4, US-1.5, US-2.5, US-2.6 (CRUD complet)

---

## 📋 Estimation Globale

| Epic | User Stories | Story Points | Priorité |
|------|--------------|--------------|----------|
| Epic 1 - Clients | 5 US | 13 | DOIT AVOIR |
| Epic 2 - Tenants | 8 US | 21 | DOIT AVOIR |
| Epic 3 - Utilisateurs | 5 US | 13 | DOIT AVOIR |
| Epic 4 - Onboarding | 5 US | 21 | DOIT AVOIR |
| Epic 5 - Authentification | 6 US | 21 | DOIT AVOIR |
| Epic 6 - IdentityServer | 7 US | 34 | DOIT AVOIR |
| Epic 7 - Notifications | 1 US | 5 | DOIT AVOIR |
| Epic 8 - Administration | 3 US | 8 | DEVRAIT AVOIR |
| Epic 9 - Tests | 3 US | 21 | DOIT AVOIR |
| **TOTAL** | **43 US** | **157 SP** | **~6 sprints** |

---

## 🎯 Critères d'Acceptation Globaux

Pour que le projet soit considéré comme "Done":

1. ✅ Tous les endpoints API documentés dans `API_ENDPOINTS.md` sont implémentés
2. ✅ Le flux OAuth2 Authorization Code + PKCE fonctionne E2E
3. ✅ Le flux d'onboarding complet fonctionne (notification → activation)
4. ✅ Le branding par tenant est fonctionnel
5. ✅ Les tokens sont stockés de manière persistante (PostgreSQL)
6. ✅ CustomClientStore agrège dynamiquement les redirect URIs et CORS
7. ✅ Les logs sont configurés (Serilog + PostgreSQL ou ELK)
8. ✅ Tests unitaires + intégration + E2E couvrent > 70% du code
9. ✅ Documentation technique complète (`README.md`, `ARCHITECTURE.md`, etc.)
10. ✅ L'application peut être déployée en production (Docker + PostgreSQL)

---

## 📚 Références

- [Cas d'Usage Détaillés](USE_CASES.md)
- [Architecture DDD](ARCHITECTURE.md)
- [Flux de Compte](ACCOUNT_FLOWS.md)
- [Endpoints API](API_ENDPOINTS.md)
- [Flux d'Onboarding](ONBOARDING_FLOW.md)

---

## 📊 Diagrammes Mermaid (Principaux Flux)

### Flux Onboarding + Validation + Création Pending (US-4.1 / US-4.2 / US-3.1)
```mermaid
sequenceDiagram
  participant U as Utilisateur
  participant IdP as Johodp (Pages)
  participant App as Application Tierce (Webhook)
  participant API as Johodp API
  U->>IdP: GET /account/onboarding?tenant
  IdP-->>U: Formulaire (email, nom, prénom)
  U->>IdP: POST /account/onboarding
  IdP->>App: POST verify-user (HMAC)
  App->>App: Vérification règles métier
  alt Accepté
    App->>API: POST /api/users/register (PendingActivation)
    API->>API: Créer User(Status=PendingActivation)
    API->>U: Email d'activation envoyé
  else Refus / Timeout
    IdP-->>U: Message attente / réessayer
  end
```

### Flux Activation (US-4.3 / US-4.4 / US-4.5)
```mermaid
sequenceDiagram
  participant U as Utilisateur
  participant IdP as Johodp (AccountController)
  participant Store as UserStore
  U->>IdP: GET /account/activate?token&userId
  IdP-->>U: Formulaire mot de passe
  U->>IdP: POST /account/activate
  IdP->>Store: VerifyUserTokenAsync
  Store-->>IdP: OK
  IdP->>Store: SetPasswordHash + Activate + ConfirmEmail
  IdP-->>U: Succès + session
```

### Flux Login OAuth2 Authorization Code + PKCE (US-5.1 / US-5.2 / US-6.3 / US-6.4)
```mermaid
sequenceDiagram
  participant SPA as Application SPA
  participant IdP as IdentityServer/Johodp
  participant CS as CustomClientStore
  participant DB as DB
  SPA->>SPA: Générer code_verifier + code_challenge
  SPA->>IdP: /connect/authorize (PKCE + tenant)
  IdP->>CS: FindClientByIdAsync
  CS->>DB: Charger client + tenants
  DB-->>CS: Données
  CS-->>IdP: Client agrégé
  IdP-->>SPA: Redirection login
  SPA->>IdP: POST /account/login (credentials)
  IdP->>DB: Vérifier user + tenant access
  IdP-->>SPA: Redirect callback?code=XYZ
  SPA->>IdP: POST /connect/token (code + code_verifier)
  IdP->>IdP: Vérifier PKCE + code
  IdP-->>SPA: access_token + id_token + refresh_token
```

### Flux Refresh Token (US-6.6) & API Protégée (US-6.7 / US-5.3)
```mermaid
sequenceDiagram
  participant SPA as SPA
  participant IdP as IdentityServer
  participant API as Johodp API
  SPA->>API: GET /api/users/me (Bearer access_token)
  API->>API: Validation JWT (signature, exp, scope)
  API-->>SPA: 200 OK UserDto
  SPA->>IdP: POST /connect/token (grant_type=refresh_token)
  IdP->>IdP: Vérifier refresh_token (non révoqué)
  IdP-->>SPA: Nouveaux tokens
```

### Flux Multi-Tenant (US-3.3 / US-3.4 / US-3.5) & Accès Global
```mermaid
flowchart LR
  Admin[Admin] --> Add[POST /api/users/{u}/tenants/{t}]
  Add --> Domain[User.AddTenantId]
  Domain --> Persist[Save]
  Persist --> Access[User autorisé]
  Admin --> Remove[DELETE /api/users/{u}/tenants/{t}]
  Remove --> DomainRem[User.RemoveTenantId]
  DomainRem --> PersistRem[Save]
  PersistRem --> Revoked[Accès révoqué]
  style Access fill:#c3f3c3,stroke:#2e7
  style Revoked fill:#f9d2d2,stroke:#a33
```

### Vue d'État Utilisateur (PendingActivation → Active)
```mermaid
stateDiagram-v2
  [*] --> PendingActivation
  PendingActivation --> Active: Activation réussite
```

---
