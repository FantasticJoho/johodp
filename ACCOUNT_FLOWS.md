# Flux de Gestion de Compte et Mots de Passe

Ce document décrit les flux de gestion de compte disponibles dans le fournisseur d'identité Johodp.

## Vue d'ensemble

Johodp fournit un système complet de gestion de compte basé sur ASP.NET Core Identity, intégré à l'architecture Domain-Driven Design.

**Architecture :** API uniquement (headless/API-first), aucun formulaire web ou interface utilisateur fourni par le fournisseur d'identité.

**Flux d'inscription :**
1. Le client appelle `POST /api/auth/register` (soumet une demande d'inscription)
2. L'IDP notifie l'application tierce via webhook (fire-and-forget, timeout 5s)
3. L'application tierce valide et appelle `POST /api/users/register` (crée l'utilisateur en `PendingActivation`)
4. Le système déclenche `UserPendingActivationEvent` (événement de domaine)
5. Le gestionnaire d'événement envoie automatiquement l'email d'activation
6. L'utilisateur reçoit l'email, clique sur le lien, appelle `POST /api/auth/activate` avec token + mot de passe
7. Le compte devient `Active`, l'utilisateur peut se connecter

Toute création d'utilisateur déclenche automatiquement l'envoi d'un email d'activation via l'architecture événementielle.

## Endpoints

### Flux d'Inscription et d'Activation Utilisateur

#### Inscription via API (`/api/users/register`)
**Flux principal pour l'intégration d'applications tierces**

- **POST** — Créer un nouveau compte utilisateur (en attente d'activation)
  - L'application externe appelle cet endpoint après avoir reçu et validé une demande d'inscription
  - Corps de la requête :
    ```json
    {
      "email": "user@example.com",
      "firstName": "Jean",
      "lastName": "Dupont",
      "tenantId": "acme-corp",
      "requestId": "optional-tracking-id"
    }
    ```
  - Crée l'agrégat `User` du domaine avec `Status = PendingActivation`
  - Déclenche `UserPendingActivationEvent` (événement de domaine)
  - Le gestionnaire d'événement (`SendActivationEmailHandler`) automatiquement :
    - Génère le token d'activation via `UserManager.GenerateEmailConfirmationTokenAsync()`
    - Envoie l'email d'activation via `IEmailService` (actuellement logué dans la console)
  - Retourne :
    ```json
    {
      "userId": "guid",
      "email": "user@example.com",
      "status": "PendingActivation",
      "message": "Utilisateur créé avec succès. Un email d'activation sera envoyé."
    }
    ```

#### Activer le Compte (`/api/auth/activate`)
- **POST** — Activer le compte utilisateur avec token et définir le mot de passe
  - Corps de la requête :
    ```json
    {
      "token": "activation-token-from-email",
      "userId": "user-guid",
      "newPassword": "MotDePasse123!",
      "confirmPassword": "MotDePasse123!"
    }
    ```
  - Confirme l'email via `UserManager.ConfirmEmailAsync(user, token)`
  - Définit le mot de passe via `UserManager.AddPasswordAsync(user, newPassword)`
  - Met à jour le statut utilisateur à `Active`
  - Retourne 200 OK en cas de succès
  - Le token expire après 24 heures (configurable)

### Flux d'Authentification

#### Connexion (`/api/auth/login`)
- **POST** — Authentifier l'utilisateur par email et mot de passe (API JSON)
  - Corps de la requête :
    ```json
    {
      "email": "user@example.com",
      "password": "MotDePasse123!",
      "tenantName": "acme-corp"  // Requis : identifiant du tenant
    }
    ```
  - **Flux sans MFA :**
    - Vérifie le hash du mot de passe via `UserManager.CheckPasswordAsync`
    - Définit un cookie de session sécurisé (`.AspNetCore.Identity.Application`)
    - Retourne 200 OK avec message de succès
  
  - **Flux avec MFA requis (si `Client.RequireMfa = true`) :**
    - Vérifie credentials (email + password + tenant)
    - Détecte si MFA est requis via `IMfaService.IsMfaRequiredForUserAsync()`
    - **Si MFA pas encore enrollé** :
      - Retourne 200 OK avec `mfaEnrollmentRequired: true` et `redirectUrl: /mfa/enroll`
      - Utilisateur doit compléter enrollment (Parcours 1)
    - **Si MFA déjà enrollé** :
      - Crée cookie temporaire `pending_mfa` (userId|clientId|timestamp, 5 min)
      - Retourne 200 OK avec `mfaVerificationRequired: true` et `redirectUrl: /mfa-verification`
      - Utilisateur doit entrer code TOTP (Parcours 2)
  
  - Paramètres du cookie session : HttpOnly, Secure (production), SameSite=Lax, expiration glissante de 7 jours
  - Retourne 401 Unauthorized si les identifiants sont invalides

#### Déconnexion (`/api/auth/logout`)
- **POST** — Déconnecter et effacer la session
- Efface les cookies d'authentification
- Retourne 200 OK

### Endpoints MFA/TOTP

#### Enrollment TOTP (Parcours 1)

##### Initier l'enrollment (`/api/auth/mfa/enroll`)
- **POST** — Génère une clé TOTP et QR code pour enrollment
- **Authentification requise** : Cookie session
- Vérifie que MFA est requis pour le client de l'utilisateur
- Génère nouvelle clé authenticator via `UserManager.ResetAuthenticatorKeyAsync()`
- Retourne :
  ```json
  {
    "sharedKey": "JBSWY3DPEHPK3PXP",
    "qrCodeUri": "otpauth://totp/Johodp:user@example.com?secret=JBSWY3DPEHPK3PXP&issuer=Johodp",
    "manualEntryKey": "JBSW Y3DP EHPK 3PXP"
  }
  ```
- L'utilisateur scanne le QR code avec Microsoft Authenticator

##### Vérifier l'enrollment (`/api/auth/mfa/verify-enrollment`)
- **POST** — Vérifie le code TOTP et active MFA
- **Authentification requise** : Cookie session
- Corps de la requête :
  ```json
  {
    "code": "123456"
  }
  ```
- Vérifie le code via `UserManager.VerifyTwoFactorTokenAsync()`
- Active 2FA : `UserManager.SetTwoFactorEnabledAsync(user, true)`
- Active MFA dans le domaine : `domainUser.EnableMFA()`
- Génère 10 recovery codes
- Retourne :
  ```json
  {
    "message": "Two-factor authentication enabled successfully",
    "recoveryCodes": ["12345678", "87654321", ...]
  }
  ```

#### Login avec TOTP (Parcours 2)

##### Vérifier code TOTP (`/api/auth/mfa-verify`)
- **POST** — Vérifie le code TOTP après login initial
- **Authentification requise** : Cookie temporaire `pending_mfa`
- Corps de la requête :
  ```json
  {
    "totpCode": "123456"
  }
  ```
- Lit cookie `pending_mfa` (userId|clientId|timestamp)
- Vérifie expiration (< 5 minutes)
- Valide TOTP via `UserManager.VerifyTwoFactorTokenAsync()`
- Crée session authentifiée : `SignInWithClaimsAsync(user, [mfa_verified=true])`
- Supprime cookie `pending_mfa`
- Retourne URL de redirection vers `/connect/authorize` pour OAuth2 flow
- **Flux complet** : Cookie session → IdentityServer génère JWT avec claims MFA

##### Statut MFA (`/api/auth/mfa/status`)
- **GET** — Obtenir le statut MFA de l'utilisateur connecté
- **Authentification requise** : Bearer token ou cookie session
- Retourne :
  ```json
  {
    "mfaEnabled": true,
    "enrolledAt": "2024-12-05T10:30:00Z",
    "recoveryCodesRemaining": 8,
    "isMfaRequired": true,
    "clientRequiresMfa": true
  }
  ```

#### Lost Device Recovery (Parcours 3)

##### Étape 1 : Initier récupération (`/api/auth/mfa/lost-device`)
- **POST** — Demande de réinitialisation MFA
- **Authentification** : Aucune (public)
- Corps de la requête :
  ```json
  {
    "email": "user@example.com"
  }
  ```
- Génère token de vérification d'identité (1 heure)
- Envoie email avec lien de vérification
- Retourne toujours succès (ne révèle pas si email existe)

##### Étape 2 : Vérifier identité (`/api/auth/mfa/verify-identity`)
- **POST** — Valide le token et questions de sécurité
- Corps de la requête :
  ```json
  {
    "token": "abc123xyz789",
    "securityAnswers": {
      "birthCity": "Paris",
      "firstPet": "Rex"
    }
  }
  ```
- Valide token (1h expiration)
- Vérifie réponses aux questions de sécurité
- Génère `verified_identity` token (30 min)
- Retourne :
  ```json
  {
    "verifiedToken": "def456uvw012",
    "expiresIn": "30 minutes",
    "message": "Identity verified. You can now reset your MFA enrollment."
  }
  ```

##### Étape 3 : Réinitialiser enrollment (`/api/auth/mfa/reset-enrollment`)
- **POST** — Désactive MFA et force re-enrollment
- Corps de la requête :
  ```json
  {
    "verifiedToken": "def456uvw012"
  }
  ```
- Valide `verified_identity` token (30 min expiration)
- Désactive MFA : `SetTwoFactorEnabledAsync(user, false)`
- Désactive dans domaine : `domainUser.DisableMFA()`
- Envoie email de confirmation
- Au prochain login, utilisateur sera forcé à re-enroll (Parcours 1)

##### Désactiver MFA volontairement (`/api/auth/mfa/disable`)
- **POST** — Désactive MFA (seulement si non requis par client)
- **Authentification requise** : Bearer token
- Corps de la requête :
  ```json
  {
    "password": "MotDePasse123!"
  }
  ```
- Vérifie password
- Vérifie que `Client.RequireMfa = false`
- Désactive MFA si autorisé
- Retourne 409 Conflict si MFA est requis par policy

### Configuration IdentityServer

**Architecture :** Fournisseur d'identité headless avec endpoints API uniquement.

**Configuration de l'Interaction Utilisateur :**
```csharp
services.AddIdentityServer(options =>
{
    options.UserInteraction.LoginUrl = "/api/auth/login";
    options.UserInteraction.LoginReturnUrlParameter = "returnUrl";
});
```

Lorsque IdentityServer détecte un utilisateur non authentifié pendant une requête d'autorisation OAuth2, il redirige vers `/api/auth/login?returnUrl={authorize_url}`. 

**Flux :**
1. Le client navigue vers `/connect/authorize` (non authentifié)
2. IdentityServer redirige vers `/api/auth/login?returnUrl=...`
3. L'application cliente gère l'interface utilisateur de connexion (peut être SPA, application mobile, etc.)
4. Après connexion réussie, le client redirige vers `returnUrl`
5. IdentityServer complète l'autorisation et retourne le code/tokens

**Implémentation actuelle :**
- Endpoint de connexion : `/api/auth/login` (API JSON)
- Les clients fournissent leur propre interface utilisateur de connexion
- Aucun consentement requis (`RequireConsent = false` sur tous les clients)
- Pas de pages d'erreur (erreurs retournées comme réponses JSON)

**Note :** Votre application cliente doit :
- Détecter le paramètre de requête `returnUrl`
- Afficher le formulaire de connexion à l'utilisateur
- Appeler `POST /api/auth/login` pour authentifier
- Rediriger vers `returnUrl` après authentification réussie

### Récupération de Mot de Passe

#### Mot de Passe Oublié (`/api/auth/forgot-password`)
- **POST** — Initier la réinitialisation du mot de passe
  - Corps de la requête :
    ```json
    {
      "email": "user@example.com",
      "tenantName": "acme-corp"  // Requis : identifiant du tenant
    }
    ```
  - Génère le token de réinitialisation via `UserManager.GeneratePasswordResetTokenAsync(user)`
  - **Mode développement :** Token logué dans la console et retourné dans la réponse (via `IEmailService`)
  - **Production :** Token envoyé par email uniquement, non retourné dans la réponse
  - Retourne toujours un message de succès (ne révèle pas si l'email existe pour des raisons de sécurité)

#### Réinitialiser le Mot de Passe (`/api/auth/reset-password`)
- **POST** — Appliquer un nouveau mot de passe avec le token
  - Corps de la requête :
    ```json
    {
      "email": "user@example.com",
      "tenantName": "acme-corp",  // Requis : identifiant du tenant
      "token": "reset-token-from-email",
      "password": "NouveauMotDePasse123!",
      "confirmPassword": "NouveauMotDePasse123!"
    }
    ```
  - Valide la correspondance de confirmation du mot de passe
  - Réinitialise le mot de passe via `UserManager.ResetPasswordAsync(user, token, newPassword)`
  - Retourne 200 OK en cas de succès
  - Retourne 400 Bad Request si le token est invalide ou expiré

### Confirmation Pages

- **ForgotPasswordConfirmation** (`/account/forgot-password-confirmation`) — Informs user to check their email
- **ResetPasswordConfirmation** (`/account/reset-password-confirmation`) — Confirms password has been reset; user can now log in

## Architecture des Services Email

### Interface IEmailService

Located in `src/Johodp.Application/Common/Interfaces/IEmailService.cs`:

```csharp
public interface IEmailService
{
    /// Envoie un email d'activation avec token
    Task<bool> SendActivationEmailAsync(
        string email, string firstName, string lastName, 
        string activationToken, Guid userId, string? tenantId = null);
    
    /// Envoie un email de réinitialisation de mot de passe
    Task<bool> SendPasswordResetEmailAsync(
        string email, string firstName, 
        string resetToken, Guid userId);
    
    /// Envoie un email de bienvenue après activation
    Task<bool> SendWelcomeEmailAsync(
        string email, string firstName, string lastName, 
        string? tenantName = null);
    
    /// Envoyeur d'email générique
    Task<bool> SendEmailAsync(
        string email, string subject, string body);
}
```

### Implémentation EmailService

Située dans `src/Johodp.Infrastructure/Services/EmailService.cs` :

**Comportement actuel (Développement) :**
- Logue tous les détails d'email dans la console :
  - Destinataire email
  - Ligne de sujet
  - URL d'activation/réinitialisation
  - Corps HTML complet avec template professionnel
- Retourne `true` (simule un envoi réussi)

**Pour activer l'envoi réel d'emails :**
1. Ajouter un package de fournisseur email (ex. `MailKit`, `SendGrid`, `AWS.SimpleEmail`)
2. Mettre à jour le constructeur `EmailService` pour injecter le client email
3. Remplacer `await Task.CompletedTask` par l'appel SMTP/API réel
4. Configurer les identifiants dans `appsettings.json`

Exemple de structure de template :
```html
<html>
  <body style="gradient background">
    <h1>Activez Votre Compte</h1>
    <p>Bonjour {firstName} {lastName},</p>
    <p>Cliquez sur le bouton ci-dessous pour activer :</p>
    <a href="{activationUrl}" class="button">Activer</a>
    <p>Le lien expire dans 24 heures.</p>
  </body>
</html>
```

### IUserActivationService

Situé dans `src/Johodp.Application/Common/Interfaces/IUserActivationService.cs` :

Fait le pont entre la couche Application et l'Infrastructure (ASP.NET Identity) :

```csharp
public interface IUserActivationService
{
    /// Génère un token d'activation et envoie l'email
    Task<bool> SendActivationEmailAsync(
        Guid userId, string email, string firstName, 
        string lastName, string? tenantId = null);
    
    /// Active le compte utilisateur avec le token
    Task<bool> ActivateUserAsync(
        Guid userId, string activationToken, string newPassword);
}
```

### Implémentation UserActivationService

Située dans `src/Johodp.Infrastructure/Services/UserActivationService.cs` :

**Responsabilités :**
1. Récupère l'utilisateur depuis `UserManager<User>`
2. Génère le token d'activation via `GenerateEmailConfirmationTokenAsync()`
3. Appelle `IEmailService.SendActivationEmailAsync()`
4. Pour l'activation : confirme l'email, définit le mot de passe, active l'utilisateur

**Avantages architecturaux :**
- **Séparation propre :** La couche Application ne dépend pas d'ASP.NET Identity
- **Testable :** Peut mocker `IUserActivationService` dans les tests
- **Réutilisable :** N'importe quelle partie du système peut déclencher des emails d'activation

## Flux Email Piloté par Événements

### Flux d'Inscription (Complet)

```
1. POST /api/users/register
   ↓
2. RegisterUserCommandHandler
   ↓
3. User.Create() → User aggregate created (Status: PendingActivation)
   ↓
4. UserPendingActivationEvent added to aggregate
   ↓
5. DomainEventPublisher publishes event to EventBus
   ↓
6. DomainEventProcessor processes events asynchronously
   ↓
7. SendActivationEmailHandler.HandleAsync()
   ↓
8. IUserActivationService.SendActivationEmailAsync()
   ↓
9. UserManager generates activation token
   ↓
10. IEmailService.SendActivationEmailAsync()
   ↓
11. [EMAIL] Logs to console (dev) or sends via SMTP (prod)
```

### Événements Clés

**UserPendingActivationEvent** (Couche Domain) :
```csharp
public class UserPendingActivationEvent : DomainEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string? TenantId { get; set; }
}
```

**SendActivationEmailHandler** (Couche Application) :
```csharp
public class SendActivationEmailHandler : IEventHandler<UserPendingActivationEvent>
{
    private readonly IUserActivationService _userActivationService;
    
    public async Task HandleAsync(UserPendingActivationEvent @event, ...)
    {
        await _userActivationService.SendActivationEmailAsync(
            @event.UserId,
            @event.Email, 
            @event.FirstName,
            @event.LastName,
            @event.TenantId);
    }
}
```

**Avantages de cette architecture :**
- ✅ Envoi automatique d'email lors de la création d'utilisateur depuis n'importe quelle source
- ✅ Découplé : Les contrôleurs n'ont pas besoin de connaître les emails
- ✅ Testable : Mocker les gestionnaires d'événements dans les tests
- ✅ Extensible : Ajouter plus de gestionnaires pour la création d'utilisateur (analytiques, webhooks, etc.)



## Gestion de Session

### Authentification par Cookie

- **Schéma :** "Cookies"
- **Durée :** 7 jours depuis la dernière activité (expiration glissante)
- **HttpOnly :** Oui (sécurisé contre XSS)
- **Secure :** Oui (HTTPS uniquement en production)
- **SameSite :** Lax (protection CSRF)
- **LoginPath :** `/api/auth/login` (redirection sur 401)
- **LogoutPath :** `/api/auth/logout`
- **AccessDeniedPath :** N/A (l'API retourne 403 JSON)

### Claims dans la Session

Le cookie de session transporte des claims incluant :
- `sub` — Sujet (ID utilisateur)
- `email` — Adresse email
- `given_name` — Prénom
- `family_name` — Nom de famille
- `role` — Rôles utilisateur (depuis l'agrégat de domaine)
- `permission` — Permissions utilisateur (depuis l'agrégat de domaine)
- `scope` — Portée/organisation utilisateur

## Intégration au Domaine

### Agrégat User (Mis à jour)

```csharp
// src/Johodp.Domain/Users/Aggregates/User.cs

public class User : AggregateRoot
{
    public UserId Id { get; private set; }
    public Email Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string? PasswordHash { get; private set; }
    public UserStatus Status { get; private set; }  // NEW: PendingActivation, Active, Suspended, Deleted
    public bool EmailConfirmed { get; private set; }
    public DateTime CreatedAt { get; private set; }
    
    private readonly List<UserTenant> _userTenants = new();
    public IReadOnlyList<UserTenant> UserTenants => _userTenants.AsReadOnly();
    
    // Computed property pour compatibilité
    public IReadOnlyList<TenantId> TenantIds => _userTenants
        .Select(ut => ut.TenantId)
        .ToList()
        .AsReadOnly();
    
    /// Crée un utilisateur en état d'activation en attente
    public static User Create(
        string email, 
        string firstName, 
        string lastName,
        string? tenantId = null,
        bool createAsPending = true)
    {
        var user = new User
        {
            Id = UserId.CreateUnique(),
            Email = Email.Create(email),
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = false,
            Status = createAsPending ? UserStatus.PendingActivation : UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        
        // Note : Les tenants sont maintenant ajoutés via AddTenant(tenantId, role, scope)
        // après validation par l'application tierce
        
        if (createAsPending)
        {
            // L'événement déclenche automatiquement l'envoi d'email
            user.AddDomainEvent(new UserPendingActivationEvent(
                user.Id.Value,
                user.Email.Value,
                user.FirstName,
                user.LastName,
                tenantId
            ));
        }
        else
        {
            user.AddDomainEvent(new UserRegisteredEvent(
                user.Id.Value,
                user.Email.Value,
                user.FirstName,
                user.LastName
            ));
        }
        
        return user;
    }
    
    public void SetPasswordHash(string? hash)
    {
        PasswordHash = hash;
    }
    
    public void Activate()
    {
        Status = UserStatus.Active;
        EmailConfirmed = true;
        AddDomainEvent(new UserActivatedEvent(Id.Value, Email.Value));
    }
    
    public void Suspend()
    {
        Status = UserStatus.Suspended;
    }
}
```

### Implémentation UserStore

Le `UserStore` (dans `src/Johodp.Infrastructure/Identity/UserStore.cs`) implémente les stores ASP.NET Identity pour persister les données utilisateur dans le domaine :

```csharp
public class UserStore : 
    IUserStore<User>,
    IUserPasswordStore<User>,
    IUserEmailStore<User>
{
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.CommitAsync();
        return IdentityResult.Success;
    }
    
    public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        _unitOfWork.Users.Update(user);
        await _unitOfWork.CommitAsync();
        return IdentityResult.Success;
    }
    
    public async Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.SetPasswordHash(passwordHash);
    }
    
    public async Task<string?> GetPasswordHashAsync(User user, CancellationToken cancellationToken)
    {
        return user.PasswordHash;
    }
    
    // Méthodes additionnelles pour email, confirmation, etc.
}
```

### CustomSignInManager (Mis à jour)

Le `CustomSignInManager` (dans `src/Johodp.Infrastructure/Identity/CustomSignInManager.cs`) étend le SignInManager standard pour s'intégrer au domaine et appliquer le MFA spécifique au client :

```csharp
public class CustomSignInManager : SignInManager<User>
{
    private readonly IMfaAuthenticationService _mfaService;
    private readonly ITenantRepository _tenantRepository;
    
    public override async Task<SignInResult> PasswordSignInAsync(
        string userName, string password, bool isPersistent, bool lockoutOnFailure)
    {
        var user = await UserManager.FindByEmailAsync(userName);
        if (user == null)
            return SignInResult.Failed;
        
        // Vérifier si l'utilisateur est actif
        if (user.Status != UserStatus.Active)
            return SignInResult.NotAllowed;
        
        if (!await UserManager.CheckPasswordAsync(user, password))
            return SignInResult.Failed;
        
        // L'application du MFA spécifique au client est gérée séparément
        // via IMfaAuthenticationService dans AccountController
        
        await SignInAsync(user, isPersistent);
        return SignInResult.Success;
    }
}
```

### Intégration MFA

Le MFA est appliqué **par client**, pas par rôle utilisateur. 

#### Architecture MFA/TOTP

**3 Parcours implémentés :**

1. **Parcours 1 - Onboarding (Enrollment)** :
   - Utilisateur login → MFA requis mais pas enrollé
   - `/mfa/enroll` → génère QR code
   - Utilisateur scanne avec Microsoft Authenticator
   - `/mfa/verify-enrollment` → valide premier code TOTP
   - MFA activé + 10 recovery codes générés

2. **Parcours 2 - Login avec TOTP** :
   - Utilisateur login → MFA requis et enrollé
   - Cookie `pending_mfa` créé (5 min, contient userId|clientId|timestamp)
   - Redirection vers `/mfa-verification`
   - `/mfa-verify` → valide code TOTP
   - Session authentifiée créée avec claim `mfa_verified=true`
   - Redirection vers OAuth2 `/connect/authorize` → génération JWT

3. **Parcours 3 - Lost Device Recovery** :
   - Utilisateur clique "J'ai perdu mon authenticator"
   - `/mfa/lost-device` → envoie email avec token (1h)
   - `/mfa/verify-identity` → valide token + questions sécurité → génère `verified_identity` (30 min)
   - `/mfa/reset-enrollment` → désactive MFA
   - Prochain login force re-enrollment (retour Parcours 1)

#### Flux MFA avec OAuth2

Voir le diagramme complet dans `ARCHITECTURE.md` section "🔐 Flux MFA/TOTP avec OAuth2".

**Points clés** :
- Cookie `pending_mfa` : temporaire (5 min), preuve que password est validé
- Cookie session `.AspNetCore.Identity.Application` : contient claim `mfa_verified=true`
- `IdentityServerProfileService` : injecte claims `mfa_verified` et `mfa_enabled` dans JWT
- JWT final contient : `sub`, `email`, `tenant_id`, `role`, `scope`, `mfa_verified`, `mfa_enabled`

#### Configuration

```csharp
// IMfaService - Logique métier MFA
services.AddScoped<IMfaService, MfaService>();

// Détecte si MFA est requis pour un utilisateur
bool mfaRequired = await _mfaService.IsMfaRequiredForUserAsync(user);

// Vérifie Client.RequireMfa flag
var tenant = await _tenantRepository.GetByIdAsync(user.TenantId);
var client = await _clientRepository.GetByIdAsync(tenant.ClientId);
if (client?.RequireMfa == true)
{
    // MFA requis → vérifier si enrollé → créer pending_mfa cookie
}
```

#### Endpoints implémentés

- ✅ `POST /mfa/enroll` - Génère QR code TOTP
- ✅ `POST /mfa/verify-enrollment` - Valide enrollment + génère recovery codes
- ✅ `POST /mfa-verify` - Vérifie TOTP pendant login (cookie-based)
- ✅ `GET /mfa/status` - Statut MFA utilisateur
- ✅ `POST /mfa/lost-device` - Initie récupération (envoie email)
- ✅ `POST /mfa/verify-identity` - Valide identité (token + questions)
- ✅ `POST /mfa/reset-enrollment` - Réinitialise MFA
- ✅ `POST /mfa/disable` - Désactive MFA volontairement (si autorisé)

#### Tests

Voir `src/Johodp.Api/httpTest/complete-workflow.http` STEP 20 pour tests complets des 3 parcours.

## Tests des Flux de Compte

### Test de l'Inscription et Activation Utilisateur (Implémentation Actuelle)

```bash
# Exécuter l'application
dotnet run --project src/Johodp.Api

# L'API tourne maintenant sur http://localhost:5000
```

#### Test de l'Inscription Utilisateur via API
```powershell
# Créer un nouvel utilisateur
$body = @{
    email = 'newuser@example.com'
    firstName = 'Jean'
    lastName = 'Dupont'
    tenantId = 'acme-corp'
} | ConvertTo-Json

Invoke-WebRequest -Uri "http://localhost:5000/api/users/register" `
    -Method POST `
    -Body $body `
    -ContentType 'application/json'

# Réponse :
# {
#   "userId": "guid",
#   "email": "newuser@example.com",
#   "status": "PendingActivation",
#   "message": "Utilisateur créé avec succès. Un email d'activation sera envoyé."
# }

# Vérifier les logs console pour les détails de l'email :
# [EMAIL] Envoi d'un email d'activation à newuser@example.com
# [EMAIL] Sujet: Activez votre compte
# [EMAIL] URL d'activation: http://localhost:5000/account/activate?token=...
# [EMAIL] Corps: <email HTML complet>
# [EMAIL] ✅ Email d'activation logué avec succès
```

#### Test d'Activation de Compte
```powershell
# Extraire le token d'activation des logs console
$activationBody = @{
    token = 'ACTIVATION_TOKEN_FROM_LOGS'
    userId = 'USER_GUID_FROM_REGISTRATION'
    newPassword = 'MotDePasse123!'
    confirmPassword = 'MotDePasse123!'
} | ConvertTo-Json

Invoke-WebRequest -Uri "http://localhost:5000/api/auth/activate" `
    -Method POST `
    -Body $activationBody `
    -ContentType 'application/json'

# Réponse : 200 OK
# L'utilisateur est maintenant Active et peut se connecter
```

#### Test de Connexion
```powershell
# Connexion avec l'utilisateur activé
$loginBody = @{
    email = 'newuser@example.com'
    password = 'MotDePasse123!'
    tenantName = 'acme-corp'
} | ConvertTo-Json

$session = $null
$response = Invoke-WebRequest -Uri "http://localhost:5000/api/auth/login" `
    -Method POST `
    -Body $loginBody `
    -ContentType 'application/json' `
    -SessionVariable session

# Le cookie est défini dans $session
$session.Cookies.GetCookies("http://localhost:5000")
# Sortie : cookie .AspNetCore.Identity.Application
```

#### Test du Flux OAuth2 PKCE Complet
```powershell
# Après connexion, tester l'autorisation
$authUrl = "http://localhost:5000/connect/authorize?" + 
    "response_type=code&" +
    "client_id=johodp-spa&" +
    "redirect_uri=http://localhost:4200/callback&" +
    "scope=openid profile email johodp.identity johodp.api&" +
    "code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&" +
    "code_challenge_method=S256&" +
    "state=random-state&" +
    "nonce=random-nonce"

$authResponse = Invoke-WebRequest -Uri $authUrl `
    -WebSession $session `
    -MaximumRedirection 0 `
    -ErrorAction SilentlyContinue

# Extraire le code d'autorisation de l'en-tête Location de redirection
$code = ($authResponse.Headers.Location -split 'code=')[1] -split '&' | Select-Object -First 1

# Échanger le code contre des tokens
$tokenBody = "grant_type=authorization_code&" +
    "client_id=johodp-spa&" +
    "code=$code&" +
    "redirect_uri=http://localhost:4200/callback&" +
    "code_verifier=dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"

$tokenResponse = Invoke-WebRequest -Uri "http://localhost:5000/connect/token" `
    -Method POST `
    -Body $tokenBody `
    -ContentType 'application/x-www-form-urlencoded'

$tokens = $tokenResponse.Content | ConvertFrom-Json
# $tokens.access_token - Token d'accès JWT
# $tokens.id_token - Token d'identité OIDC
# $tokens.refresh_token - Token de rafraîchissement
```

### Tests Locaux (Mode Développement)

### Test des Formulaires Web Hérités (Si Activés)

```bash
# Exécuter l'application
dotnet run --project src/Johodp.Api

# Naviguer vers la page de connexion
# http://localhost:5000/account/login
```

#### Test d'Inscription
1. Cliquer sur le lien "S'inscrire" sur la page de connexion
2. Entrer email, prénom, nom, mot de passe
3. Soumettre — l'utilisateur est créé et connecté automatiquement
4. Vérifier que le cookie de session est défini (voir Outils de Développement du navigateur > Application > Cookies)

#### Test de Connexion
1. Se déconnecter ou ouvrir une fenêtre incognito
2. Aller sur `/account/login`
3. Entrer l'email et le mot de passe de l'inscription
4. Soumettre — l'utilisateur est connecté, cookie de session créé

#### Test de Réinitialisation de Mot de Passe (Développement)

1. Demander une réinitialisation de mot de passe :
```powershell
$body = @{
    email = 'user@example.com'
    tenantName = 'acme-corp'
} | ConvertTo-Json

$response = Invoke-WebRequest -Uri "http://localhost:5000/api/auth/forgot-password" `
    -Method POST `
    -Body $body `
    -ContentType 'application/json'

# La réponse dev inclut le token
$result = $response.Content | ConvertFrom-Json
$token = $result.token
```

2. Réinitialiser le mot de passe avec le token :
```powershell
$resetBody = @{
    email = 'user@example.com'
    tenantName = 'acme-corp'
    token = $token
    password = 'NouveauMotDePasse123!'
    confirmPassword = 'NouveauMotDePasse123!'
} | ConvertTo-Json

Invoke-WebRequest -Uri "http://localhost:5000/api/auth/reset-password" `
    -Method POST `
    -Body $resetBody `
    -ContentType 'application/json'

# Réponse : { "message": "Réinitialisation du mot de passe réussie" }
```

3. Se connecter avec le nouveau mot de passe :
```powershell
$loginBody = @{
    email = 'user@example.com'
    tenantName = 'acme-corp'
    password = 'NouveauMotDePasse123!'
} | ConvertTo-Json

Invoke-WebRequest -Uri "http://localhost:5000/api/auth/login" `
    -Method POST `
    -Body $loginBody `
    -ContentType 'application/json'
```

#### Test de l'Application du MFA
1. Inscrire un utilisateur
2. Assigner l'utilisateur à un rôle avec `RequiresMFA = true` (par ex. via base de données ou endpoint admin)
3. Se déconnecter
4. Tenter de se connecter avec les identifiants de cet utilisateur
5. Attendre `SignInResult.RequiresTwoFactor` — l'UI devrait rediriger vers le défi 2FA (pas encore implémenté)

## Notifications Email

### Implémentation Actuelle (Développement)

Tous les emails sont **logुés dans la console** avec tous les détails :
- Adresse email du destinataire
- Ligne de sujet
- URL d'activation/réinitialisation avec token
- Corps HTML complet (style professionnel)

**Exemple de sortie console :**
```
[EMAIL] Envoi d'un email d'activation à user@example.com (Utilisateur : Jean Dupont, UserId: guid, Tenant: acme-corp)
[EMAIL] Sujet : Activez votre compte
[EMAIL] URL d'activation : http://localhost:5000/account/activate?token=CfDJ8...&userId=guid&tenant=acme-corp
[EMAIL] Corps :
<!DOCTYPE html>
<html>
<head>
    <style>
        .container { max-width: 600px; margin: 0 auto; }
        .header { background: linear-gradient(135deg, #667eea, #764ba2); }
        .button { background: #667eea; color: white; padding: 12px 30px; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>Activez votre compte</h1>
        </div>
        <div class="content">
            <p>Bonjour Jean Dupont,</p>
            <p>Cliquez sur le bouton pour activer :</p>
            <a href="..." class="button">Activer mon compte</a>
            <p>Ce lien expire dans 24 heures.</p>
        </div>
    </div>
</body>
</html>
[EMAIL] ✅ Email d'activation logué avec succès pour user@example.com
```

### Configuration Production

Pour activer **l'envoi réel d'emails**, mettre à jour `EmailService.cs` :

#### Option 1 : SMTP (MailKit)
```csharp
// Installer : dotnet add package MailKit
public class EmailService : IEmailService
{
    private readonly ISmtpClient _smtpClient;
    private readonly IConfiguration _config;
    
    public async Task<bool> SendActivationEmailAsync(...)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Johodp", "noreply@johodp.com"));
        message.To.Add(new MailboxAddress($"{firstName} {lastName}", email));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = body };
        
        await _smtpClient.ConnectAsync(_config["Smtp:Host"], 587, SecureSocketOptions.StartTls);
        await _smtpClient.AuthenticateAsync(_config["Smtp:Username"], _config["Smtp:Password"]);
        await _smtpClient.SendAsync(message);
        await _smtpClient.DisconnectAsync(true);
        
        return true;
    }
}
```

#### Option 2 : SendGrid
```csharp
// Installer : dotnet add package SendGrid
public class EmailService : IEmailService
{
    private readonly ISendGridClient _sendGridClient;
    
    public async Task<bool> SendActivationEmailAsync(...)
    {
        var msg = new SendGridMessage
        {
            From = new EmailAddress("noreply@johodp.com", "Johodp"),
            Subject = subject,
            HtmlContent = body
        };
        msg.AddTo(new EmailAddress(email, $"{firstName} {lastName}"));
        
        var response = await _sendGridClient.SendEmailAsync(msg);
        return response.IsSuccessStatusCode;
    }
}
```

#### Option 3 : AWS SES
```csharp
// Installer : dotnet add package AWSSDK.SimpleEmail
public class EmailService : IEmailService
{
    private readonly IAmazonSimpleEmailService _sesClient;
    
    public async Task<bool> SendActivationEmailAsync(...)
    {
        var request = new SendEmailRequest
        {
            Source = "noreply@johodp.com",
            Destination = new Destination { ToAddresses = new List<string> { email } },
            Message = new Message
            {
                Subject = new Content(subject),
                Body = new Body { Html = new Content(body) }
            }
        };
        
        var response = await _sesClient.SendEmailAsync(request);
        return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
    }
}
```

### Configuration (appsettings.json)

```json
{
  "Email": {
    "Provider": "SMTP",  // or "SendGrid" or "AWS"
    "BaseUrl": "https://yourapp.com",
    "From": "noreply@johodp.com",
    "FromName": "Johodp Identity Platform"
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "EnableSsl": true
  },
  "SendGrid": {
    "ApiKey": "SG.your-api-key"
  },
  "AWS": {
    "Region": "us-east-1",
    "AccessKey": "your-access-key",
    "SecretKey": "your-secret-key"
  }
}
```

## Notifications Email (Futur)

Actuellement, les tokens de réinitialisation de mot de passe sont logués dans la console en développement. Pour activer les notifications email :

### Extension de la Fonctionnalité Email

L'`IEmailService` supporte déjà les emails de réinitialisation de mot de passe. Pour les utiliser :

1. Dans l'action POST `ForgotPassword`, appeler :
   ```csharp
   var token = await _userManager.GeneratePasswordResetTokenAsync(user);
   await _emailService.SendPasswordResetEmailAsync(
       user.Email.Value, 
       user.FirstName, 
       token, 
       user.Id.Value);
   ```

2. Emails de bienvenue après activation :
   ```csharp
   // Dans AccountController.Activate après activation réussie
   await _emailService.SendWelcomeEmailAsync(
       user.Email.Value,
       user.FirstName,
       user.LastName,
       tenantName);
   ```

Tous les templates d'email sont déjà implémentés dans `EmailService.cs` avec un style HTML professionnel.

## Considérations de Sécurité

- **Hachage de Mot de Passe :** Utilise `IPasswordHasher<TUser>` (PBKDF2 par défaut, personnalisable)
- **Expiration des Tokens :** 
  - Les tokens d'activation expirent après 24 heures (configuré via `DataProtectionTokenProviderOptions`)
  - Les tokens de réinitialisation de mot de passe expirent après 24 heures (par défaut)
  - Les tokens sont à usage unique et invalidés après utilisation réussie
- **Protection CSRF :** Cookie SameSite=Lax ; Tokens anti-forgery sur les formulaires (si formulaires activés)
- **HTTPS Uniquement :** Flag Secure défini en production (`CookieSecurePolicy.SameAsRequest`)
- **Timeout de Session :** Expiration glissante de 7 jours (personnalisable via `ExpireTimeSpan`)
- **Support MFA :** 
  - Appliqué **par client** (pas par rôle utilisateur)
  - Vérifié via le flag `client.RequireMfa` dans la base de données
  - Intégré avec `IMfaAuthenticationService`
- **Énumération d'Email :** 
  - Mot de passe oublié ne révèle intentionnellement pas si l'email existe (bonne pratique de sécurité)
  - L'inscription retourne 201 Created même si l'utilisateur est en attente de validation externe
- **Validation du Statut Utilisateur :**
  - Seuls les utilisateurs `Active` peuvent se connecter
  - Les utilisateurs `PendingActivation` sont bloqués jusqu'à activation complète
  - Les utilisateurs `Suspended` et `Deleted` ne peuvent pas s'authentifier
- **Sécurité des Cookies :**
  - HttpOnly : Oui (prévient les attaques XSS)
  - Secure : Oui en production (HTTPS uniquement)
  - SameSite : Lax (protection CSRF tout en permettant les flux OAuth2)
  - Nom : `.AspNetCore.Identity.Application`
- **Sécurité OAuth2 :**
  - PKCE requis pour tous les flux authorization code
  - Secrets client optionnels (SPAs publiques utilisent PKCE sans secrets)
  - URIs de redirection validées contre la configuration du tenant
  - Paramètre state requis (protection CSRF)
  - Paramètre nonce recommandé (prévention d'attaque par rejeu)

## Configuration

Toute la configuration Identity et authentification se trouve dans `src/Johodp.Api/Extensions/ServiceCollectionExtensions.cs` :

```csharp
// ASP.NET Identity Core with domain User aggregate
services.AddIdentityCore<User>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireDigit = false;
    options.SignIn.RequireConfirmedEmail = false;  // Set to true to enforce email confirmation
})
.AddSignInManager<CustomSignInManager>()
.AddUserStore<UserStore>()
.AddDefaultTokenProviders();

// Configure activation token lifespan (24 hours)
services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(24);
});

// Application cookie for web sessions
services.ConfigureApplicationCookie(opts =>
{
    opts.Cookie.Name = ".AspNetCore.Identity.Application";
    opts.Cookie.SameSite = SameSiteMode.Lax;
    opts.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    opts.Cookie.HttpOnly = true;
    opts.ExpireTimeSpan = TimeSpan.FromDays(7);
    opts.SlidingExpiration = true;
});

// Register email and activation services
services.AddScoped<IEmailService, EmailService>();
services.AddScoped<IUserActivationService, UserActivationService>();

// Domain event infrastructure
services.AddSingleton<IEventBus, ChannelEventBus>();
services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
services.AddHostedService<DomainEventProcessor>();

// Event handlers (registered as scoped)
services.AddScoped<IEventHandler<UserPendingActivationEvent>, 
    SendActivationEmailHandler>();
services.AddScoped<IEventHandler<UserActivatedEvent>, 
    UserActivatedEventHandler>();

// IdentityServer with custom client store (dynamic loading from DB)
services.AddIdentityServer()
    .AddInMemoryApiScopes(IdentityServerConfig.GetApiScopes())
    .AddInMemoryApiResources(IdentityServerConfig.GetApiResources())
    .AddInMemoryIdentityResources(IdentityServerConfig.GetIdentityResources())
    .AddOperationalStore(options =>
    {
        options.ConfigureDbContext = b =>
            b.UseNpgsql(connectionString,
                sql => sql.MigrationsAssembly("Johodp.Infrastructure"));
        options.DefaultSchema = "dbo";
        options.EnableTokenCleanup = true;
        options.TokenCleanupInterval = 3600; // 1 hour
    })
    .AddAspNetIdentity<User>()
    .AddDeveloperSigningCredential();

// Custom client store (loads clients dynamically from database)
services.AddScoped<IClientStore, CustomClientStore>();

// Profile service (maps domain user to OIDC claims)
services.AddScoped<IProfileService, IdentityServerProfileService>();
```

## Résumé de l'Architecture

### Statut d'Implémentation Actuel

✅ **Implémenté :**
- Inscription utilisateur via API avec validation d'application externe
- Génération et log automatique d'email d'activation
- Architecture pilotée par événements pour l'envoi d'emails
- Activation de compte avec token et configuration de mot de passe
- Connexion avec authentification tenant-aware
- **MFA/TOTP complet avec 3 parcours** :
  - Parcours 1 : Onboarding (enrollment avec QR code + recovery codes)
  - Parcours 2 : Login avec TOTP (cookie-based flow → OAuth2 JWT)
  - Parcours 3 : Lost Device Recovery (email verification + identity questions)
- Flux authorization code OAuth2/OIDC + PKCE
- Chargement dynamique de clients depuis la base de données
- Support multi-tenant avec URIs de redirection spécifiques au tenant
- Gestion de session avec cookies sécurisés
- Claims MFA dans JWT (`mfa_verified`, `mfa_enabled`)
- Domain-driven design avec frontières d'agrégats appropriées
- Séparation architecture propre (Domain → Application → Infrastructure → API)

⏳ **En Développement :**
- Livraison réelle d'emails (SMTP/SendGrid/AWS SES)
- Chiffrement cookie `pending_mfa` avec Data Protection API
- Token storage pour identity verification (UserTokens vs Redis)
- Questions de sécurité pour lost device recovery
- Emails de sécurité (MFA disabled, reset confirmation)

📋 **Planifié :**
- Formulaires d'inscription web (faire l'intégration avec cette api)
- Portail admin pour la gestion des utilisateurs 
- Journalisation d'audit pour les événements d'authentification
- Rate Limiting sur les endpoints MFA (max 5 tentatives TOTP)
- Verrouillage de compte après échecs de tentatives
- Liens de vérification d'email


## Références

- [Documentation ASP.NET Core Identity](https://learn.microsoft.com/fr-fr/aspnet/core/security/authentication/identity/)
- [Documentation Duende IdentityServer](https://docs.duendesoftware.com/identityserver/v7/)
- [RFC 7636 OAuth 2.0 PKCE](https://datatracker.ietf.org/doc/html/rfc7636)
- [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html)
- [RFC 6238 TOTP: Time-Based One-Time Password](https://datatracker.ietf.org/doc/html/rfc6238)
- [Hachage de Mot de Passe dans ASP.NET Core Identity](https://learn.microsoft.com/fr-fr/aspnet/core/security/authentication/identity-configuration/)
- [Authentification par Cookie dans ASP.NET Core](https://learn.microsoft.com/fr-fr/aspnet/core/security/authentication/cookie/)
- [Two-Factor Authentication dans ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-enable-qrcodes)
- [OWASP Bonnes Pratiques d'Authentification](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [OWASP Multi-Factor Authentication](https://cheatsheetseries.owasp.org/cheatsheets/Multifactor_Authentication_Cheat_Sheet.html)
- [Référence Domain-Driven Design](https://www.domainlanguage.com/ddd/reference/)
- [Clean Architecture par Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
