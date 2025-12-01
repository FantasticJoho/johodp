# Flow de Création de Compte avec Validation Externe

Ce document décrit le flow complet de création de compte utilisateur avec validation par une application tierce et activation par email.

## Table des Matières
- [Vue d'Ensemble](#vue-densemble)
- [Diagrammes de Flux](#diagrammes-de-flux)
- [Flow Détaillé](#flow-détaillé)
- [Endpoints Implémentés](#endpoints-implémentés)
- [Architecture](#architecture)
- [Plan d'Implémentation](#plan-dimplémentation)

---

## Vue d'Ensemble

Le flow d'onboarding se déroule en **2 phases distinctes** :

### Phase 1 : Demande de Création (Synchrone)
1. **Utilisateur** remplit le formulaire d'onboarding
2. **IDP** envoie notification à l'app tierce (fire-and-forget, timeout 5s)
3. **IDP** affiche page "En attente de validation"

### Phase 2 : Création et Activation (Asynchrone)
4. **App tierce** valide la demande (hors IDP)
5. **App tierce** appelle l'API IDP pour créer le compte
6. **IDP** crée le compte en statut `PendingActivation`
7. **IDP** envoie email d'activation
8. **Utilisateur** clique sur le lien et définit son mot de passe
9. **Compte** devient `Active`

---

## Diagrammes de Flux

### Diagramme de Séquence Complet

```mermaid
sequenceDiagram
    participant U as 👤 Utilisateur
    participant Browser as 🌐 Navigateur
    participant IDP as 🔐 IDP<br/>(Johodp)
    participant DB as 💾 Database
    participant APP as 🏢 App Tierce
    participant Email as 📧 Email Service

    Note over U,Email: PHASE 1 : DEMANDE DE CRÉATION

    U->>Browser: Accès formulaire inscription
    Browser->>IDP: GET /account/onboarding<br/>?acr_values=tenant:acme<br/>&return_url=...
    IDP->>DB: Récupérer Tenant "acme" et sa CustomConfiguration
    DB-->>IDP: Tenant { CustomConfigurationId, NotificationUrl }
    IDP->>DB: Récupérer CustomConfiguration
    DB-->>IDP: CustomConfiguration { branding, langues }
    IDP-->>Browser: Formulaire avec branding de CustomConfiguration
    Browser-->>U: Affichage formulaire personnalisé

    U->>Browser: Remplit et soumet formulaire
    Browser->>IDP: POST /account/onboarding<br/>{ email, firstName, lastName }
    
    IDP->>DB: Vérifier si email existe
    DB-->>IDP: Email disponible
    
    IDP->>APP: POST {NotificationUrl}<br/>{ eventType, email, firstName, lastName, requestId }
    Note over IDP,APP: Fire-and-forget<br/>Timeout: 5 secondes<br/>Ne bloque pas le flow
    
    IDP-->>Browser: View "OnboardingPending"
    Browser-->>U: "Votre demande est en attente..."

    Note over U,Email: PHASE 2 : VALIDATION ET ACTIVATION (ASYNCHRONE)

    APP->>APP: Validation métier interne<br/>(peut prendre plusieurs minutes/heures)
    
    alt ✅ Compte Approuvé
        APP->>IDP: POST /api/users/register<br/>{ email, firstName, lastName, tenantId }
        Note over APP,IDP: Anonymous (pour l'instant)<br/>TODO: Sécuriser avec API Key
        
        IDP->>DB: User.Create(..., createAsPending=true)
        DB-->>IDP: User { Status=PendingActivation }
        
        IDP->>IDP: Émettre UserPendingActivationEvent
        IDP->>Email: SendActivationEmail<br/>{ activationUrl, tenant branding }
        
        Email->>U: 📧 Email "Activez votre compte"<br/>Lien: /account/activate?token=...&userId=...
        
        IDP-->>APP: 201 Created { userId, status }
        
        U->>Browser: Clique sur lien dans email
        Browser->>IDP: GET /account/activate<br/>?token=...&userId=...&tenant=acme
        
        IDP->>DB: Récupérer User + Tenant + CustomConfiguration
        DB-->>IDP: User, Tenant, CustomConfiguration branding
        IDP-->>Browser: Formulaire mot de passe<br/>avec branding CustomConfiguration
        Browser-->>U: Formulaire activation
        
        U->>Browser: Définit mot de passe
        Browser->>IDP: POST /account/activate<br/>{ token, userId, newPassword }
        
        IDP->>IDP: Vérifier token validité
        IDP->>DB: user.SetPasswordHash(hash)
        IDP->>DB: user.Activate()
        DB-->>IDP: User { Status=Active }
        
        IDP->>IDP: SignInAsync(user)
        IDP-->>Browser: Redirect to returnUrl<br/>+ Set auth cookie
        Browser-->>U: ✅ Connecté automatiquement
        
    else ❌ Compte Refusé
        Note over APP,U: L'application tierce ne fait rien<br/>Pas d'appel à l'IDP<br/>L'utilisateur ne reçoit pas d'email
    end
```

### Diagramme de Flux (Flowchart)

```mermaid
flowchart TD
    Start([👤 Utilisateur demande<br/>création compte]) --> GetOnboarding[GET /account/onboarding<br/>?acr_values=tenant:acme]
    
    GetOnboarding --> LoadTenant{Tenant existe<br/>et actif ?}
    LoadTenant -->|Non| Error1[❌ Erreur: Tenant invalide]
    LoadTenant -->|Oui| ShowForm[📝 Afficher formulaire<br/>avec branding tenant]
    
    ShowForm --> UserFills[Utilisateur remplit:<br/>email, firstName, lastName]
    UserFills --> PostOnboarding[POST /account/onboarding]
    
    PostOnboarding --> ValidateEmail{Email déjà<br/>utilisé ?}
    ValidateEmail -->|Oui| Error2[❌ Erreur: Email existe]
    ValidateEmail -->|Non| NotifyApp
    
    NotifyApp[🔔 POST notification à app tierce<br/>Fire-and-forget - 5s timeout]
    NotifyApp --> ShowPending[📄 Afficher OnboardingPending<br/>Demande en attente de validation]
    
    ShowPending --> WaitApp[⏳ Attente validation<br/>par app tierce]
    
    WaitApp --> AppDecision{App tierce<br/>valide ?}
    
    AppDecision -->|❌ Refusé| NoAction[Aucune action<br/>Utilisateur ne reçoit rien]
    
    AppDecision -->|✅ Approuvé| ApiRegister[POST /api/users/register<br/>Anonymous]
    
    ApiRegister --> CreateUser[💾 Créer User<br/>Status=PendingActivation<br/>TenantId=acme]
    CreateUser --> EmitEvent[📣 UserPendingActivationEvent]
    EmitEvent --> SendEmail[📧 Envoyer email activation<br/>avec branding tenant]
    
    SendEmail --> UserReceives[👤 Utilisateur reçoit email]
    UserReceives --> ClickLink[Clique sur lien activation]
    
    ClickLink --> GetActivate[GET /account/activate<br/>?token=...&userId=...&tenant=acme]
    
    GetActivate --> ValidateToken{Token<br/>valide ?}
    ValidateToken -->|Non| Error3[❌ Lien expiré/invalide]
    ValidateToken -->|Oui| CheckStatus{User status =<br/>PendingActivation ?}
    
    CheckStatus -->|Non| Error4[❌ Compte déjà activé<br/>ou invalide]
    CheckStatus -->|Oui| ShowActivateForm[📝 Formulaire mot de passe<br/>avec branding tenant]
    
    ShowActivateForm --> UserPassword[Utilisateur définit<br/>mot de passe]
    UserPassword --> PostActivate[POST /account/activate]
    
    PostActivate --> SetPassword[💾 SetPasswordHash]
    SetPassword --> ActivateUser[💾 user.Activate<br/>Status → Active<br/>EmailConfirmed=true]
    ActivateUser --> AutoLogin[🔐 SignInAsync<br/>Connexion automatique]
    AutoLogin --> Success([✅ Compte actif<br/>Utilisateur connecté])
    
    style Start fill:#e1f5ff
    style Success fill:#d4edda
    style Error1 fill:#f8d7da
    style Error2 fill:#f8d7da
    style Error3 fill:#f8d7da
    style Error4 fill:#f8d7da
    style NoAction fill:#fff3cd
    style NotifyApp fill:#cfe2ff
    style ApiRegister fill:#cfe2ff
    style SendEmail fill:#d1ecf1
```

### Vue des États du Compte

```mermaid
stateDiagram-v2
    [*] --> Onboarding: Utilisateur remplit formulaire
    
    Onboarding --> AwaitingValidation: IDP notifie app tierce
    
    AwaitingValidation --> Rejected: App tierce refuse
    AwaitingValidation --> PendingActivation: App tierce valide<br/>POST /api/users/register
    
    Rejected --> [*]: Pas de compte créé
    
    PendingActivation --> EmailSent: IDP envoie email activation
    EmailSent --> PendingActivation: Email en transit
    
    PendingActivation --> Active: Utilisateur définit mot de passe<br/>POST /account/activate
    
    Active --> Suspended: Admin suspend
    Suspended --> Active: Admin réactive
    
    Active --> Deleted: Soft delete
    Deleted --> [*]
    
    note right of PendingActivation
        Status = PendingActivation
        PasswordHash = null
        EmailConfirmed = false
        IsActive = false
    end note
    
    note right of Active
        Status = Active
        PasswordHash != null
        EmailConfirmed = true
        IsActive = true
        ActivatedAt = DateTime.UtcNow
    end note
```

---

## Endpoints Implémentés

### 1. GET /account/onboarding

**Description :** Affiche le formulaire d'onboarding avec le branding du tenant

**Paramètres Query :**
- `acr_values` (string) : Format `tenant:{tenantId}` - Identifiant du tenant
- `return_url` (string, optionnel) : URL de retour après activation

**Réponse :** Vue Razor avec formulaire personnalisé

**Exemple :**
```
GET /account/onboarding?acr_values=tenant:acme&return_url=https://app.acme.com/dashboard
```

**Actions :**
1. Parse `acr_values` pour extraire `tenantId`
2. Récupère `Tenant` depuis la DB (branding, logo, couleurs)
3. Crée `OnboardingViewModel` avec le branding
4. Retourne la vue `Onboarding.cshtml`

---

### 2. POST /account/onboarding

**Description :** Traite la demande d'onboarding et notifie l'app tierce

**Body (Form) :**
```json
{
  "tenantId": "acme",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "returnUrl": "https://app.acme.com/dashboard"
}
```

**Réponse :** Vue `OnboardingPending.cshtml`

**Actions :**
1. Valide le formulaire (email format, champs requis)
2. Vérifie que le tenant existe et est actif
3. Vérifie que l'email n'est pas déjà utilisé
4. **Appelle `NotificationService.NotifyAccountRequestAsync()`** (fire-and-forget)
5. Retourne la vue "En attente de validation"

**Notification envoyée à l'app tierce :**
```http
POST {tenant.NotificationUrl}
Content-Type: application/json
Authorization: Bearer {tenant.ApiKey}

{
  "eventType": "AccountCreationRequested",
  "tenantId": "acme",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "requestedAt": "2025-11-20T10:30:00Z",
  "requestId": "a7b3c8d9-e4f5-6789-a0b1-c2d3e4f5g6h7"
}
```

---

### 3. POST /api/users/register

**Description :** Crée un compte en statut PendingActivation (appelé par l'app tierce)

**Authentification :** `[AllowAnonymous]` (TODO: Sécuriser avec API Key)

**Body (JSON) :**
```json
{
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "tenantId": "acme",
  "createAsPending": true
}
```

**Réponse Success (201 Created) :**
```json
{
  "userId": "b8c4d9e5-f6a7-8901-b2c3-d4e5f6g7h8i9",
  "email": "john.doe@example.com",
  "status": "PendingActivation",
  "message": "User created successfully. Activation email will be sent."
}
```

**Réponse Error (409 Conflict) :**
```json
{
  "error": "User with email john.doe@example.com already exists"
}
```

**Actions :**
1. Force `createAsPending = true`
2. Appelle `RegisterUserCommandHandler`
3. Crée `User` avec `User.Create(..., createAsPending: true)`
4. Émet `UserPendingActivationEvent`
5. Event handler envoie email d'activation (TODO: implémenter)
6. Retourne 201 avec les infos du user

---

### 4. GET /account/activate

**Description :** Affiche le formulaire d'activation avec validation du token

**Paramètres Query :**
- `token` (string, requis) : Token d'activation généré par Identity
- `userId` (string, requis) : GUID de l'utilisateur
- `tenant` (string, requis) : Identifiant du tenant

**Réponse :** Vue Razor avec formulaire mot de passe

**Exemple :**
```
GET /account/activate?token=CfDJ8N...&userId=b8c4d9e5-...&tenant=acme
```

**Actions :**
1. Vérifie que le token et userId sont fournis
2. Récupère le `User` depuis la DB
3. Vérifie que `User.Status == PendingActivation`
4. Récupère le `Tenant` pour le branding
5. Crée `ActivateViewModel` avec email masqué
6. Retourne la vue `Activate.cshtml`

---

### 5. POST /account/activate

**Description :** Active le compte en définissant le mot de passe

**Body (Form) :**
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

**Réponse :** Vue `ActivateSuccess.cshtml` + Cookie d'authentification

**Actions :**
1. Valide le formulaire (mots de passe correspondent)
2. Vérifie le token avec `UserManager.VerifyUserTokenAsync()`
3. Hash le mot de passe avec `UserManager.PasswordHasher`
4. Appelle `user.SetPasswordHash(passwordHash)`
5. Confirme l'email avec `UserManager.ConfirmEmailAsync()`
6. **Appelle `domainUser.Activate()`** (domain logic)
7. Sauvegarde avec `UnitOfWork.SaveChangesAsync()`
8. Connecte automatiquement avec `SignInManager.SignInAsync()`
9. Retourne la vue de succès

**Modifications Domain :**
```csharp
// user.Activate() fait :
Status = UserStatus.Active
IsActive = true
EmailConfirmed = true
ActivatedAt = DateTime.UtcNow
// + Émet UserActivatedEvent
```

---

## Architecture

### Composants Implémentés

```mermaid
graph TB
    subgraph "API Layer"
        AC[AccountController]
        UC[UsersController]
    end
    
    subgraph "Application Layer"
        RUC[RegisterUserCommand]
        RUCH[RegisterUserCommandHandler]
        NS[INotificationService]
    end
    
    subgraph "Domain Layer"
        User[User Aggregate]
        UserStatus[UserStatus Enum]
        Tenant[Tenant Aggregate]
        UPE[UserPendingActivationEvent]
        UAE[UserActivatedEvent]
    end
    
    subgraph "Infrastructure Layer"
        NSImpl[NotificationService]
        UR[UserRepository]
        TR[TenantRepository]
        UOW[UnitOfWork]
        HTTPC[HttpClient]
    end
    
    AC -->|POST onboarding| NS
    AC -->|GET/POST activate| User
    AC -->|Branding| TR
    
    UC -->|POST register| RUC
    RUC -->|Handler| RUCH
    RUCH -->|Create| User
    RUCH -->|Emit| UPE
    
    NS -->|Implements| NSImpl
    NSImpl -->|HTTP POST| HTTPC
    NSImpl -->|Get config| TR
    
    User -->|Has| UserStatus
    User -->|Emits| UPE
    User -->|Emits| UAE
    User -->|Methods| UserActivate[Activate<br/>Suspend<br/>Deactivate]
    
    Tenant -->|Has| TenantConfig[NotificationUrl<br/>ApiKey<br/>NotifyOnAccountRequest]
    
    style AC fill:#d1ecf1
    style UC fill:#d1ecf1
    style User fill:#fff3cd
    style Tenant fill:#fff3cd
    style NSImpl fill:#d4edda
    style UPE fill:#f8d7da
    style UAE fill:#f8d7da
```

### Structure des Données

**User Aggregate :**
```csharp
public class User : AggregateRoot
{
    public UserId Id { get; private set; }
    public Email Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public UserStatus Status { get; private set; }  // ✅ Nouveau
    public DateTime? ActivatedAt { get; private set; }  // ✅ Nouveau
    public bool EmailConfirmed { get; private set; }
    public bool IsActive { get; private set; }
    public string? PasswordHash { get; private set; }
    public List<string> TenantIds { get; private set; }
    
    // ✅ Nouveaux états
    public enum UserStatus
    {
        PendingActivation = 0,  // En attente activation
        Active = 1,             // Compte actif
        Suspended = 2,          // Compte suspendu
        Deleted = 3             // Compte supprimé (soft delete)
    }
    
    // ✅ Nouvelles méthodes
    public static User Create(email, firstName, lastName, tenantId, createAsPending);
    public void Activate();
    public void Suspend(string reason);
}
```

**Tenant Aggregate :**
```csharp
public class Tenant : AggregateRoot
{
    // Existant
    public string Name { get; private set; }
    public string DisplayName { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? CustomCss { get; private set; }
    
    // ✅ Nouveau - Configuration notification
    public string? NotificationUrl { get; private set; }
    public string? ApiKey { get; private set; }
    public bool NotifyOnAccountRequest { get; private set; }
    
    // ✅ Nouvelles méthodes
    public void ConfigureNotifications(string url, string apiKey);
    public void DisableNotifications();
    public void RegenerateApiKey();
}
```

### ViewModels

**OnboardingViewModel :**
```csharp
public class OnboardingViewModel
{
    public string TenantId { get; set; }
    public string TenantDisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public string? ReturnUrl { get; set; }
    
    [Required, EmailAddress]
    public string Email { get; set; }
    
    [Required, StringLength(100)]
    public string FirstName { get; set; }
    
    [Required, StringLength(100)]
    public string LastName { get; set; }
}
```

**ActivateViewModel :**
```csharp
public class ActivateViewModel
{
    [Required]
    public string Token { get; set; }
    
    [Required]
    public string UserId { get; set; }
    
    [Required]
    public string TenantId { get; set; }
    
    public string MaskedEmail { get; set; }
    public string TenantDisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public string? ReturnUrl { get; set; }
    
    [Required, StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; }
    
    [Required, Compare("NewPassword")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; }
}
```

---

## Flow Détaillé

### Étape 1 : Demande de Création de Compte

**URL d'entrée :**
```
GET /account/onboarding?acr_values=tenant:acme&return_url=https://app.com/callback
```

**Paramètres :**
- `acr_values` : Identifiant du tenant (format `tenant:xxx`)
- `return_url` : URL de retour après activation

**Actions IDP :**
1. Parser `acr_values` pour extraire `tenantId` ("acme")
2. Charger les paramètres du tenant (branding, langue, config webhook)
3. Afficher page d'onboarding avec :
   - Logo du tenant
   - Couleurs primaire/secondaire
   - CSS personnalisé
   - Langue par défaut
   - Formulaire d'inscription

### Étape 2 : Soumission du Formulaire

**Données collectées :**
```json
{
  "tenantId": "acme",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "returnUrl": "https://app.com/callback"
}
```

**Actions IDP :**
1. Valider format email et champs obligatoires
2. Vérifier que le tenant existe et est actif
3. Vérifier que l'email n'est pas déjà utilisé

### Étape 3 : Notification à l'Application Tierce (Fire-and-Forget)

**HTTP POST envoyé par l'IDP (notification simple) :**

```http
POST https://app-tierce.com/api/idp/account-request
Content-Type: application/json
Authorization: Bearer {shared_secret}

{
  "eventType": "AccountCreationRequested",
  "tenantId": "acme",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "requestedAt": "2025-11-20T10:30:00Z",
  "requestId": "abc-123-def-456"
}
```

**Caractéristiques :**
- **Fire-and-forget** : L'IDP n'attend pas de réponse
- **Timeout court** : 5 secondes max
- **Pas de retry** : Si échec, l'app tierce ne reçoit pas la notification (acceptable)
- **Optionnel** : Le tenant peut ne pas avoir d'URL configurée

**Réponse attendue :**
```http
HTTP/1.1 204 No Content
```

L'IDP ignore le contenu de la réponse, seul le status code 2xx est vérifié pour logging.

### Étape 4 : Application Tierce Crée le Compte (Plus Tard)

**L'application tierce appelle l'API publique de l'IDP :**

```http
POST https://idp.example.com/api/users/register
Content-Type: application/json
Authorization: Bearer {api_key_tenant}

{
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "tenantId": "acme",
  "sendActivationEmail": true,
  "metadata": {
    "approvedBy": "admin@company.com",
    "department": "IT",
    "employeeId": "12345"
  }
}
```

**Réponse :**
```http
HTTP/1.1 201 Created
Content-Type: application/json

{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "email": "user@example.com",
  "status": "PendingActivation",
  "activationEmailSent": true
}
```

### Étape 5 : Création du Compte (Status: PendingActivation)

**Flow interne de l'endpoint `/api/users/register` :**

```csharp
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
{
    // 1. Valider que le tenant existe
    var tenant = await _tenantRepository.GetByNameAsync(request.TenantId);
    if (tenant == null)
        return BadRequest("Invalid tenant");
    
    // 2. Valider l'API key (authentification)
    if (!await _authService.ValidateTenantApiKey(tenant, Request.Headers["Authorization"]))
        return Unauthorized();
    
    // 3. Créer le compte en statut PendingActivation
    var command = new RegisterUserCommand
    {
        Email = request.Email,
        FirstName = request.FirstName,
        LastName = request.LastName,
        TenantId = request.TenantId,
        CreateAsPending = true,  // ⚠️ Nouveau paramètre
        SendActivationEmail = request.SendActivationEmail
    };
    
    var userId = await _mediator.Send(command);
    
    return Created($"/api/users/{userId}", new { userId, status = "PendingActivation" });
}
```

**User Aggregate :**
```csharp
public static User CreatePending(
    string email, 
    string firstName, 
    string lastName, 
    string tenantId)
{
    var user = new User
    {
        Id = UserId.CreateUnique(),
        Email = Email.Create(email),
        FirstName = firstName,
        LastName = lastName,
        TenantIds = new List<string> { tenantId },
        Status = UserStatus.PendingActivation,  // ⚠️ Nouveau statut
        IsActive = false,
        EmailConfirmed = false,
        PasswordHash = null  // Pas encore de mot de passe
    };
    
    user.AddDomainEvent(new UserPendingActivationEvent(user.Id, user.Email));
    return user;
}
```

### Étape 6 : Envoi de l'Email d'Activation

**Event Handler (déclenché automatiquement après création du compte) :**
```csharp
public class UserPendingActivationEventHandler : 
    INotificationHandler<UserPendingActivationEvent>
{
    public async Task Handle(UserPendingActivationEvent notification, 
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(notification.UserId);
        var tenant = await _tenantRepository.GetByNameAsync(user.TenantIds.First());
        
        // Générer token d'activation (24h de validité)
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        
        // Construire URL d'activation
        var activationUrl = $"https://idp.example.com/account/activate" +
            $"?token={Uri.EscapeDataString(token)}" +
            $"&userId={user.Id}" +
            $"&tenant={tenant.Name}";
        
        // Envoyer email avec branding du tenant
        await _emailService.SendActivationEmailAsync(
            email: user.Email.Value,
            firstName: user.FirstName,
            activationUrl: activationUrl,
            tenantId: tenant.Name
        );
        
        _logger.LogInformation(
            "Activation email sent to {Email} for user {UserId}", 
            user.Email, user.Id);
    }
}
```

**Email Template (avec branding) :**
```html
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: Arial, sans-serif; }
        .header { 
            background-color: {{PrimaryColor}}; 
            padding: 20px; 
            text-align: center; 
        }
        .logo { max-width: 200px; }
        .content { padding: 30px; }
        .button {
            background-color: {{SecondaryColor}};
            color: white;
            padding: 15px 30px;
            text-decoration: none;
            border-radius: 5px;
            display: inline-block;
        }
    </style>
</head>
<body>
    <div class="header">
        <img src="{{LogoUrl}}" class="logo" alt="{{TenantName}}" />
    </div>
    <div class="content">
        <h2>Bienvenue {{FirstName}} !</h2>
        <p>Votre compte a été créé avec succès.</p>
        <p>Pour l'activer, cliquez sur le bouton ci-dessous et définissez votre mot de passe :</p>
        <p style="text-align: center; margin: 30px 0;">
            <a href="{{ActivationUrl}}" class="button">
                Activer mon compte
            </a>
        </p>
        <p><small>Ce lien expire dans 24 heures.</small></p>
        <p>Si vous n'avez pas demandé la création de ce compte, vous pouvez ignorer cet email.</p>
        <hr/>
        <p><small>L'équipe {{TenantName}}</small></p>
    </div>
</body>
</html>
```

### Étape 7 : Activation du Compte

**Page d'activation :**
```
GET /account/activate?token={token}&userId={userId}&tenant=acme
```

**Affichage :**
- Logo et branding du tenant
- Email de l'utilisateur (masqué : u***@example.com)
- Formulaire de définition du mot de passe :
  - Nouveau mot de passe
  - Confirmation du mot de passe
  - Exigences affichées (8 caractères min, majuscule, chiffre, etc.)

**Soumission :**
```csharp
[HttpPost]
public async Task<IActionResult> Activate(ActivateAccountRequest request)
{
    // 1. Récupérer l'utilisateur
    var user = await _userManager.FindByIdAsync(request.UserId);
    if (user == null || user.Status != UserStatus.PendingActivation)
        return BadRequest("Invalid activation link");
    
    // 2. Valider le token (vérifie expiration et unicité)
    var tokenValid = await _userManager.VerifyUserTokenAsync(
        user, 
        _userManager.Options.Tokens.EmailConfirmationTokenProvider,
        "EmailConfirmation", 
        request.Token);
    
    if (!tokenValid)
        return BadRequest("Invalid or expired token");
    
    // 3. Valider le mot de passe
    var passwordValidator = new PasswordValidator<ApplicationUser>();
    var passwordResult = await passwordValidator.ValidateAsync(
        _userManager, 
        null, 
        request.NewPassword);
    
    if (!passwordResult.Succeeded)
        return BadRequest(passwordResult.Errors);
    
    // 4. Définir le mot de passe
    var passwordHash = _userManager.PasswordHasher.HashPassword(user, request.NewPassword);
    await _userManager.SetPasswordHashAsync(user, passwordHash);
    
    // 5. Confirmer l'email
    var confirmResult = await _userManager.ConfirmEmailAsync(user, request.Token);
    if (!confirmResult.Succeeded)
        return BadRequest(confirmResult.Errors);
    
    // 6. Activer le compte (domain logic)
    var domainUser = await _userRepository.GetByIdAsync(Guid.Parse(user.Id));
    domainUser.Activate(); // ⚠️ Nouvelle méthode domain
    await _unitOfWork.SaveChangesAsync();
    
    // 7. Connecter automatiquement l'utilisateur
    await _signInManager.SignInAsync(user, isPersistent: true);
    
    _logger.LogInformation("User {UserId} activated successfully", user.Id);
    
    // 8. Rediriger vers l'application
    if (!string.IsNullOrEmpty(request.ReturnUrl))
        return Redirect(request.ReturnUrl);
    
    return RedirectToAction("ActivateSuccess");
}
```

**Méthode Domain :**
```csharp
public void Activate()
{
    if (Status != UserStatus.PendingActivation)
        throw new InvalidOperationException(
            $"Cannot activate user with status {Status}");
    
    Status = UserStatus.Active;
    IsActive = true;
    EmailConfirmed = true;
    ActivatedAt = DateTime.UtcNow;
    
    AddDomainEvent(new UserActivatedEvent(Id, Email));
}
```

---

## Analyse de l'Implémentation Actuelle

### ✅ Ce qui Existe Déjà

| Fonctionnalité | Fichier | Status |
|----------------|---------|--------|
| Tenant Aggregate | `Johodp.Domain/Tenants/Aggregates/Tenant.cs` | ✅ Complet |
| Branding Tenant | `Tenant.PrimaryColor`, `Tenant.LogoUrl`, etc. | ✅ Complet |
| User Aggregate | `Johodp.Domain/Users/Aggregates/User.cs` | ✅ Complet |
| Multi-tenant User | `User.TenantIds` (List<string>) | ✅ Complet |
| Registration Endpoint | `UsersController.Register()` | ✅ Existe mais ne correspond pas au flow |
| Login Endpoint | `AccountController.Login()` | ✅ Complet |
| ASP.NET Identity | `UserManager`, `SignInManager` | ✅ Configuré |
| Domain Events | `UserRegisteredEvent`, etc. | ✅ Infrastructure en place |

### ❌ Ce qui Manque

| Fonctionnalité | Priorité | Estimation |
|----------------|----------|------------|
| **UserStatus Enum** | 🔴 Critique | 1 jour |
| **Page Onboarding** | 🔴 Critique | 2 jours |
| **Notification Service** (Fire-and-forget) | 🟠 Important | 1 jour |
| **Email Service** | 🟠 Important | 2 jours |
| **Email Templates** | 🟠 Important | 1 jour |
| **Page Activation** | 🟠 Important | 2 jours |
| **API Key Authentication** (Tenant) | 🟠 Important | 1 jour |
| **Admin Dashboard** | 🟢 Nice to have | 3 jours |

**Total estimé : 8-10 jours de développement** ✅ (Simplifié !)

---

## Gaps à Combler

### 1. UserStatus Enum (Priorité 1)

**Actuellement :**
```csharp
public class User
{
    public bool IsActive { get; private set; }
    public bool EmailConfirmed { get; private set; }
}
```

**Requis :**
```csharp
public enum UserStatus
{
    PendingApproval = 0,      // En attente validation app tierce
    PendingActivation = 1,    // En attente clic email
    Active = 2,               // Compte actif
    Suspended = 3,            // Compte suspendu
    Deleted = 4               // Compte supprimé (soft delete)
}

public class User
{
    public UserStatus Status { get; private set; }
    public DateTime? ActivatedAt { get; private set; }
    
    public static User CreatePending(...)
    {
        // Status = PendingActivation
    }
    
    public void Activate()
    {
        if (Status != UserStatus.PendingActivation)
            throw new InvalidOperationException();
        
        Status = UserStatus.Active;
        ActivatedAt = DateTime.UtcNow;
        AddDomainEvent(new UserActivatedEvent(...));
    }
}
```

### 2. Notification Configuration (Priorité 2)

**Requis (simplifié) :**
```csharp
public class Tenant : AggregateRoot
{
    // Existant
    public string Name { get; private set; }
    public string DisplayName { get; private set; }
    
    // À ajouter
    public string? NotificationUrl { get; private set; }
    public string? ApiKey { get; private set; }  // Pour authentifier les appels de l'app tierce
    public bool NotifyOnAccountRequest { get; private set; } = false;
    
    public void ConfigureNotifications(string url, string apiKey)
    {
        NotificationUrl = url;
        ApiKey = apiKey;
        NotifyOnAccountRequest = true;
    }
    
    public void DisableNotifications()
    {
        NotifyOnAccountRequest = false;
    }
}
```

### 3. Page Onboarding (Priorité 1)

**Fichiers à créer :**
```
src/Johodp.Api/Controllers/Account/AccountController.cs
  → OnboardingGet() 
  → OnboardingPost()

src/Johodp.Api/Views/Account/Onboarding.cshtml
src/Johodp.Api/Views/Account/OnboardingPending.cshtml
src/Johodp.Api/Views/Account/OnboardingSuccess.cshtml
src/Johodp.Api/Views/Account/OnboardingRejected.cshtml

src/Johodp.Api/Models/ViewModels/OnboardingViewModel.cs
src/Johodp.Api/Models/Requests/OnboardingRequest.cs
```

### 4. Notification Service (Priorité 2)

**Interface (simplifié) :**
```csharp
public interface INotificationService
{
    Task NotifyAccountRequestAsync(
        string tenantId,
        string email,
        string firstName,
        string lastName);
}

public class NotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<NotificationService> _logger;
    
    public async Task NotifyAccountRequestAsync(
        string tenantId,
        string email,
        string firstName,
        string lastName)
    {
        var tenant = await _tenantRepository.GetByNameAsync(tenantId);
        
        // Si pas de notification configurée, skip
        if (string.IsNullOrEmpty(tenant.NotificationUrl) || !tenant.NotifyOnAccountRequest)
        {
            _logger.LogInformation("No notification configured for tenant {TenantId}", tenantId);
            return;
        }
        
        var payload = new
        {
            eventType = "AccountCreationRequested",
            tenantId,
            email,
            firstName,
            lastName,
            requestedAt = DateTime.UtcNow,
            requestId = Guid.NewGuid().ToString()
        };
        
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, tenant.NotificationUrl)
            {
                Content = JsonContent.Create(payload)
            };
            
            request.Headers.Add("Authorization", $"Bearer {tenant.ApiKey}");
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.SendAsync(request, cts.Token);
            
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Notification sent to {Url}", tenant.NotificationUrl);
            else
                _logger.LogWarning("Notification failed with status {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            // Fire-and-forget : on log mais on ne throw pas
            _logger.LogError(ex, "Failed to send notification to {Url}", tenant.NotificationUrl);
        }
    }
}
```

### 5. API Key Authentication (Priorité 2)

**Middleware ou Filter pour valider les API keys :**
```csharp
public class TenantApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ITenantRepository _tenantRepository;
    
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            return AuthenticateResult.Fail("Missing Authorization header");
        
        var token = authHeader.ToString().Replace("Bearer ", "");
        
        // Extraire le tenantId depuis le body ou un header custom
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(tenantId))
            return AuthenticateResult.Fail("Missing X-Tenant-Id header");
        
        var tenant = await _tenantRepository.GetByNameAsync(tenantId);
        
        if (tenant == null || tenant.ApiKey != token)
            return AuthenticateResult.Fail("Invalid API key");
        
        var claims = new[]
        {
            new Claim("tenant_id", tenant.Name),
            new Claim("client_type", "external_app")
        };
        
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        
        return AuthenticateResult.Success(ticket);
    }
}

// Dans ServiceCollectionExtensions.cs
services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, TenantApiKeyAuthenticationHandler>(
        "TenantApiKey", options => { });
```

**Utilisation :**
```csharp
[Authorize(AuthenticationSchemes = "TenantApiKey")]
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
{
    // L'authentification a déjà validé l'API key du tenant
    // ...
}
```

### 6. Email Service (Priorité 2)

**Interface :**
```csharp
public interface IEmailService
{
    Task SendActivationEmailAsync(
        string email,
        string firstName,
        string activationUrl,
        string tenantId);
    
    Task SendWelcomeEmailAsync(
        string email,
        string firstName,
        string tenantId);
}
```

**Implémentation (SendGrid) :**
```csharp
public class SendGridEmailService : IEmailService
{
    private readonly SendGridClient _client;
    private readonly IEmailTemplateService _templateService;
    
    public async Task SendActivationEmailAsync(
        string email,
        string firstName,
        string activationUrl,
        string tenantId)
    {
        var template = await _templateService.GetActivationTemplateAsync(tenantId);
        var html = template
            .Replace("{{FirstName}}", firstName)
            .Replace("{{ActivationUrl}}", activationUrl);
        
        var msg = new SendGridMessage
        {
            From = new EmailAddress("noreply@johodp.com", "Johodp Identity"),
            Subject = "Activez votre compte",
            HtmlContent = html
        };
        
        msg.AddTo(new EmailAddress(email, firstName));
        
        var response = await _client.SendEmailAsync(msg);
        
        if (!response.IsSuccessStatusCode)
            throw new EmailException($"Failed to send email: {response.StatusCode}");
    }
}
```

### 7. Page Activation (Priorité 2)

**Fichiers à créer :**
```
src/Johodp.Api/Controllers/Account/AccountController.cs
  → ActivateGet()
  → ActivatePost()

src/Johodp.Api/Views/Account/Activate.cshtml
src/Johodp.Api/Views/Account/ActivateSuccess.cshtml

src/Johodp.Api/Models/ViewModels/ActivateViewModel.cs
src/Johodp.Api/Models/Requests/ActivateAccountRequest.cs
```

---

## Architecture Recommandée

### Diagramme de Composants (Simplifié)

```
┌─────────────────────────────────────────────────────────┐
│                      IDP (Johodp)                       │
│                                                         │
│  ┌────────────────┐         ┌─────────────────┐       │
│  │ Account        │         │ Notification    │       │
│  │ Controller     │◄───────►│ Service         │───┐   │
│  │ /onboarding    │         │ (Fire-and-      │   │   │
│  └────────────────┘         │  forget)        │   │   │
│         │                   └─────────────────┘   │   │
│         │                                          │   │
│         │ Affiche message                          ▼   │
│         │ "Demande en attente"          ┌──────────────┐
│         │                               │ Application  │
│         │                               │   Tierce     │
│         │                               └──────────────┘
│         │                                      │       │
│         │                                      │       │
│         │                      Plus tard...    │       │
│         │                                      ▼       │
│         │                         ┌──────────────────┐ │
│         │                         │ POST /api/users/ │ │
│         │                         │     register     │ │
│         │                         └──────────────────┘ │
│         │                                      │       │
│         │                                      ▼       │
│  ┌─────────────────────────────────────────────────┐  │
│  │        RegisterUserCommand Handler              │  │
│  │  (createAsPending = true)                      │  │
│  └─────────────────────────────────────────────────┘  │
│         │                                              │
│         ▼                                              │
│  ┌────────────────┐                                   │
│  │ User Aggregate │                                   │
│  │ Status:Pending │                                   │
│  └────────────────┘                                   │
│         │                                              │
│         ▼                                              │
│  ┌────────────────┐                                   │
│  │ Domain Event   │                                   │
│  │ UserPending    │                                   │
│  │ ActivationEvent│                                   │
│  └────────────────┘                                   │
│         │                                              │
│         ▼                                              │
│  ┌────────────────┐                                   │
│  │ Event Handler  │                                   │
│  │ SendActivation │                                   │
│  │ Email          │                                   │
│  └────────────────┘                                   │
│         │                                              │
│         ▼                                              │
│  ┌────────────────┐                                   │
│  │ Email Service  │                                   │
│  │ (SendGrid)     │                                   │
│  └────────────────┘                                   │
│                                                        │
└────────────────────────────────────────────────────────┘
```

### Configuration appsettings.json (Simplifié)

```json
{
  "Onboarding": {
    "ShowPendingMessage": true,
    "PendingMessageText": "Votre demande a été enregistrée et sera traitée prochainement. Vous recevrez un email lorsque votre compte sera créé."
  },
  "Notification": {
    "TimeoutSeconds": 5,
    "LogFailures": true
  },
  "Email": {
    "Provider": "SendGrid",
    "ApiKey": "your-sendgrid-api-key",
    "FromEmail": "noreply@johodp.com",
    "FromName": "Johodp Identity",
    "TemplatesPath": "EmailTemplates"
  },
  "Activation": {
    "TokenExpirationHours": 24,
    "PasswordRequirements": {
      "RequireDigit": true,
      "RequireLowercase": true,
      "RequireUppercase": true,
      "RequireNonAlphanumeric": true,
      "RequiredLength": 8
    }
  },
  "ApiKeyAuthentication": {
    "HeaderName": "X-Tenant-Id",
    "Enabled": true
  }
}
```

---

## Plan d'Implémentation

### ✅ Phase 1 : Domain Model (TERMINÉ)

- [x] **UserStatus enum** dans `User.cs` (PendingActivation, Active, Suspended, Deleted)
- [x] **User.Create()** avec paramètre `createAsPending`
- [x] **User.Activate()** méthode (change status, set ActivatedAt, émet event)
- [x] **User.Suspend()** méthode
- [x] **UserPendingActivationEvent** créé
- [x] **UserActivatedEvent** créé
- [x] **Tenant.NotificationUrl, ApiKey, NotifyOnAccountRequest** ajoutés
- [x] **Tenant.ConfigureNotifications()** méthode

### ✅ Phase 2 : Application Layer (TERMINÉ)

- [x] **RegisterUserCommand** avec `CreateAsPending`, `TenantId`, `Password`
- [x] **RegisterUserCommandHandler** adapté pour créer en Pending si demandé
- [x] **INotificationService** interface créée
- [x] **NotificationService** implémenté (fire-and-forget, timeout 5s)
- [x] Enregistrement dans DI avec HttpClient

### ✅ Phase 3 : API Endpoints (TERMINÉ)

- [x] **GET /account/onboarding** - Formulaire avec branding tenant
- [x] **POST /account/onboarding** - Notification à l'app tierce + affichage "pending"
- [x] **POST /api/users/register** - Création compte PendingActivation (Anonymous)
- [x] **GET /account/activate** - Formulaire activation avec token
- [x] **POST /account/activate** - Définir mot de passe et activer
- [x] **ViewModels** créés (OnboardingViewModel, ActivateViewModel, etc.)

### ⏳ Phase 4 : Services Email (À FAIRE)

- [ ] **IEmailService** interface (déjà créée dans IMPLEMENTATION_GAPS.md)
- [ ] **EmailService** implémentation (SendGrid ou SMTP)
- [ ] **EmailTemplateService** pour templating avec branding
- [ ] **UserPendingActivationEventHandler** pour envoyer email activation
- [ ] Configuration SendGrid dans appsettings.json
- [ ] Templates HTML avec branding tenant dynamique

**Estimation :** 2-3 jours

### ✅ Phase 5 : Database Migration (TERMINÉ)

- [x] Migration EF Core pour ajouter:
  - `Users.Status` (int, NOT NULL, default 1)
  - `Users.ActivatedAt` (datetime, nullable)
  - `Tenants.NotificationUrl` (varchar(500), nullable)
  - `Tenants.ApiKey` (varchar(100), nullable)
  - `Tenants.NotifyOnAccountRequest` (bool, NOT NULL, default false)
- [x] Migration créée: `20251120113742_AddOnboardingFlowSupport`
- [x] Migration appliquée à la base de données

**Terminé le :** 20 novembre 2025

### ⏳ Phase 6 : Views Razor (À FAIRE)

- [ ] **Onboarding.cshtml** - Formulaire inscription avec branding
- [ ] **OnboardingPending.cshtml** - Message "en attente"
- [ ] **Activate.cshtml** - Formulaire mot de passe
- [ ] **ActivateSuccess.cshtml** - Confirmation activation
- [ ] CSS/JS pour branding dynamique (couleurs, logo)
- [ ] Validation côté client (jQuery Validation)

**Estimation :** 2 jours

### 🔒 Phase 7 : Sécurisation API (OPTIONNEL - Plus tard)

- [ ] **TenantApiKeyAuthenticationHandler** (déjà créé mais commenté)
- [ ] Décommenter l'enregistrement dans ServiceCollectionExtensions
- [ ] Modifier UsersController.Register avec `[Authorize(AuthenticationSchemes = "TenantApiKey")]`
- [ ] Générer API keys pour chaque tenant
- [ ] Documentation API key pour app tierce

**Estimation :** 1-2 jours

### ⏳ Phase 8 : Tests & Polish (À FAIRE)

- [ ] Tests unitaires domain (User.Activate, etc.)
- [ ] Tests unitaires NotificationService (mock HttpClient)
- [ ] Tests intégration endpoints onboarding/activate
- [ ] Tests E2E complet du flow
- [ ] Logs structurés avec correlation IDs
- [ ] Gestion erreurs (token expiré, email invalide)
- [ ] Documentation API pour app tierce

**Estimation :** 2-3 jours

---

## État d'Avancement

### ✅ Implémenté (70%)

| Composant | Status | Fichier |
|-----------|--------|---------|
| UserStatus enum | ✅ | `Domain/Users/Aggregates/User.cs` |
| User.Activate() | ✅ | `Domain/Users/Aggregates/User.cs` |
| User.Suspend() | ✅ | `Domain/Users/Aggregates/User.cs` |
| UserPendingActivationEvent | ✅ | `Domain/Users/Events/UserPendingActivationEvent.cs` |
| UserActivatedEvent | ✅ | `Domain/Users/Events/UserActivatedEvent.cs` |
| Tenant.NotificationUrl/ApiKey | ✅ | `Domain/Tenants/Aggregates/Tenant.cs` |
| Tenant.ConfigureNotifications() | ✅ | `Domain/Tenants/Aggregates/Tenant.cs` |
| INotificationService | ✅ | `Application/Common/Interfaces/INotificationService.cs` |
| NotificationService | ✅ | `Infrastructure/Services/NotificationService.cs` |
| RegisterUserCommand.CreateAsPending | ✅ | `Application/Users/Commands/RegisterUserCommand.cs` |
| AccountController.Onboarding GET/POST | ✅ | `Api/Controllers/Account/AccountController.cs` |
| AccountController.Activate GET/POST | ✅ | `Api/Controllers/Account/AccountController.cs` |
| UsersController.Register (Anonymous) | ✅ | `Api/Controllers/UsersController.cs` |
| OnboardingViewModel | ✅ | `Api/Models/ViewModels/OnboardingViewModel.cs` |
| ActivateViewModel | ✅ | `Api/Models/ViewModels/ActivateViewModel.cs` |
| TenantApiKeyAuthenticationHandler | ✅ (commenté) | `Infrastructure/Identity/TenantApiKeyAuthenticationHandler.cs` |

### ⏳ À Faire (25%)

| Composant | Status | Priorité |
|-----------|--------|----------|
| Migration EF Core | ✅ | 🔴 Haute |
| IEmailService implémentation | ❌ | 🔴 Haute |
| EmailTemplateService | ❌ | 🔴 Haute |
| UserPendingActivationEventHandler | ❌ | 🔴 Haute |
| Views Razor (Onboarding, Activate) | ❌ | 🟡 Moyenne |
| Tests unitaires | ❌ | 🟡 Moyenne |
| Tests E2E | ❌ | 🟢 Basse |
| API Key authentication (enable) | ❌ | 🟢 Basse |

---

## Checklist de Validation

### ✅ Architecture & Design

- [x] Flow documenté avec diagrammes Mermaid
- [x] Diagramme de séquence complet
- [x] Diagramme de flux (flowchart)
- [x] Diagramme d'états
- [x] Endpoints documentés
- [x] Structure des données définie
- [x] ViewModels créés

### ✅ Domain Layer

- [x] UserStatus enum défini
- [x] User.Create() avec createAsPending
- [x] User.Activate() implémenté
- [x] User.Suspend() implémenté
- [x] UserPendingActivationEvent créé
- [x] UserActivatedEvent créé
- [x] Tenant.NotificationUrl ajouté
- [x] Tenant.ApiKey ajouté
- [x] Tenant.ConfigureNotifications() implémenté

### ✅ Application Layer

- [x] RegisterUserCommand.CreateAsPending ajouté
- [x] RegisterUserCommandHandler adapté
- [x] INotificationService interface créée

### ✅ Infrastructure Layer

- [x] NotificationService implémenté
- [x] Fire-and-forget avec timeout 5s
- [x] TenantApiKeyAuthenticationHandler créé (commenté)

### ✅ API Layer

- [x] GET /account/onboarding implémenté
- [x] POST /account/onboarding implémenté
- [x] GET /account/activate implémenté
- [x] POST /account/activate implémenté
- [x] POST /api/users/register adapté (Anonymous)
- [x] AccountController injecte tous les services
- [x] ViewModels créés

### ⏳ Services Email

- [ ] IEmailService implémentation (SendGrid/SMTP)
- [ ] EmailTemplateService avec branding
- [ ] UserPendingActivationEventHandler
- [ ] Templates HTML email
- [ ] Configuration SendGrid

### ⏳ Views

- [ ] Onboarding.cshtml créée
- [ ] OnboardingPending.cshtml créée
- [ ] Activate.cshtml créée
- [ ] ActivateSuccess.cshtml créée
- [ ] CSS dynamique avec branding
- [ ] Validation client (jQuery)

### ✅ Database

- [x] Migration créée (`20251120113742_AddOnboardingFlowSupport`)
- [x] Migration appliquée
- [x] Colonnes ajoutées:
  - users.Status (integer, default 1)
  - users.ActivatedAt (timestamp with time zone, nullable)
  - tenants.NotificationUrl (varchar 500, nullable)
  - tenants.ApiKey (varchar 100, nullable)
  - tenants.NotifyOnAccountRequest (boolean, default false)
- [ ] Indexes optimisés
- [ ] Contraintes validées

### ⏳ Tests

- [ ] Tests unitaires domain
- [ ] Tests unitaires services
- [ ] Tests intégration API
- [ ] Tests E2E flow complet
- [ ] Tests cas d'erreur

### ⏳ Sécurité (Optionnel)

- [ ] API Key authentication activée
- [ ] Rate limiting onboarding
- [ ] HTTPS forcé production
- [ ] Tokens expiration testée

---

## Prochaines Étapes Recommandées

### ✅ ~~Priorité 1 : Migration Database~~ (TERMINÉ)

~~Créer et appliquer la migration pour ajouter les nouvelles colonnes.~~

Migration `20251120113742_AddOnboardingFlowSupport` créée et appliquée avec succès.

### 🔴 Priorité 1 : Email Service (2-3 jours)

1. Choisir le provider (recommandation: **SendGrid** pour simplicité)
2. Implémenter `IEmailService` et `EmailTemplateService`
3. Créer `UserPendingActivationEventHandler`
4. Tester l'envoi d'email avec branding

### 🟡 Priorité 3 : Views Razor (2 jours)

1. Créer les 4 vues (Onboarding, OnboardingPending, Activate, ActivateSuccess)
2. Ajouter CSS pour branding dynamique
3. Validation client formulaires

### 🟢 Priorité 4 : Tests & Documentation (2-3 jours)

1. Tests unitaires et intégration
2. Tests E2E du flow complet
3. Documentation API pour app tierce
4. Guide configuration tenant

---

## Conclusion

**État actuel :** ~75% implémenté (code backend + database) ✅  
**Effort restant :** 4-6 jours développement  
**Complexité :** Moyenne (architecture simplifiée réussie)  

**Avantages du flow actuel :**
- ✅ Architecture claire et maintenable
- ✅ Fire-and-forget évite les blocages
- ✅ Pas de dépendance message broker
- ✅ L'app tierce garde le contrôle
- ✅ Code backend pratiquement terminé
- ✅ Database schema mis à jour

**Bloqueurs pour mise en production :**
1. ✅ ~~Migration database~~ (TERMINÉ)
2. ❌ Email service non implémenté
3. ❌ Views Razor non créées
4. ❌ Tests manquants

**Recommandation :** Implémenter le **service email** puis créer les **views Razor** pour avoir un flow E2E fonctionnel rapidement.

### Phase 1 : Foundation (Jours 1-2)

#### Jour 1 : Domain Model
- [ ] Ajouter `UserStatus` enum dans `User.cs`
- [ ] Ajouter méthode `User.CreatePending()`
- [ ] Ajouter méthode `User.Activate()`
- [ ] Créer `UserPendingActivationEvent`
- [ ] Créer `UserActivatedEvent`
- [ ] Tests unitaires du domain

#### Jour 2 : Tenant Configuration & Services
- [ ] Ajouter `NotificationUrl`, `ApiKey` dans `Tenant.cs`
- [ ] Ajouter méthode `Tenant.ConfigureNotifications()`
- [ ] Migration EF Core
- [ ] Créer `INotificationService` interface
- [ ] Créer `IEmailService` interface
- [ ] Configurer DI

### Phase 2 : Services (Jours 3-4)

#### Jour 3 : Services de Base
- [ ] Implémenter `NotificationService` (fire-and-forget)
- [ ] Implémenter `SendGridEmailService`
- [ ] Créer templates HTML avec branding
- [ ] Service de rendu des templates
- [ ] Tests unitaires

#### Jour 4 : API Key Auth
- [ ] Implémenter `TenantApiKeyAuthenticationHandler`
- [ ] Configurer dans `Program.cs`
- [ ] Modifier `/api/users/register` pour accepter API key
- [ ] Ajouter paramètre `createAsPending` au RegisterUserCommand
- [ ] Tests d'intégration

### Phase 3 : UI (Jours 5-6)

#### Jour 5 : Page Onboarding
- [ ] `AccountController.OnboardingGet()`
- [ ] `AccountController.OnboardingPost()`
- [ ] Appel `NotificationService` (fire-and-forget)
- [ ] Vue `Onboarding.cshtml` avec branding dynamique
- [ ] Vue `OnboardingPending.cshtml` (message d'attente)
- [ ] Tests UI

#### Jour 6 : Page Activation
- [ ] `AccountController.ActivateGet()`
- [ ] `AccountController.ActivatePost()`
- [ ] Vue `Activate.cshtml`
- [ ] Validation mot de passe (front + back)
- [ ] Auto-login après activation
- [ ] Tests

### Phase 4 : Intégration (Jours 7-8)

#### Jour 7 : Event Handlers & Tests
- [ ] `UserPendingActivationEventHandler`
- [ ] Génération token activation
- [ ] Envoi email
- [ ] Logging
- [ ] Tests

#### Jour 8 : Tests E2E & Documentation
- [ ] Flow complet onboarding → API register → activation
- [ ] Cas d'erreur (token expiré, email invalide, API key invalide)
- [ ] Tests avec notification mock
- [ ] Tests avec email mock
- [ ] README avec exemples d'utilisation
- [ ] Guide pour l'application tierce

### Phase 5 : Polish (Jours 9-10)

#### Jour 9 : Monitoring & Sécurité
- [ ] Logs structurés (Serilog)
- [ ] Métriques (Prometheus)
- [ ] Alertes (échec webhook, email, etc.)
- [ ] Dashboard Grafana

#### Jour 10 : Admin Dashboard (Optionnel)
- [ ] Liste users pending activation
- [ ] Forcer activation manuelle
- [ ] Resend activation email
- [ ] Voir historique inbox messages
- [ ] Statistiques

---

## Checklist de Validation

### ✅ Fonctionnalités Core

- [ ] **Onboarding**
  - [ ] Page affiche branding du tenant
  - [ ] Formulaire valide les inputs
  - [ ] Notification envoyée (fire-and-forget)
  - [ ] Message "Demande en attente" affiché
  - [ ] Logging des notifications

- [ ] **API Register avec API Key**
  - [ ] Endpoint accepte API key du tenant
  - [ ] Validation du tenant via API key
  - [ ] Création en statut PendingActivation
  - [ ] Email activation envoyé automatiquement

- [ ] **Création Compte**
  - [ ] User créé avec status PendingActivation
  - [ ] PasswordHash = null
  - [ ] IsActive = false
  - [ ] Domain event émis

- [ ] **Email Activation**
  - [ ] Email envoyé avec bon template
  - [ ] Branding du tenant appliqué
  - [ ] Token généré et sécurisé
  - [ ] Lien expire après 24h

- [ ] **Activation**
  - [ ] Token validé correctement
  - [ ] Mot de passe vérifié (complexité)
  - [ ] User.Status → Active
  - [ ] User.IsActive → true
  - [ ] User.EmailConfirmed → true
  - [ ] Domain event émis
  - [ ] Auto-login
  - [ ] Redirection vers returnUrl

### ✅ Sécurité

- [ ] API Key authentication sur /api/users/register
- [ ] Tokens activation expirés rejetés
- [ ] Tokens usage unique
- [ ] Rate limiting onboarding
- [ ] Input validation stricte
- [ ] SQL injection impossible (EF Core paramétré)
- [ ] XSS impossible (Razor encode HTML)
- [ ] HTTPS forcé en production

### ✅ Performance

- [ ] Notification timeout court (5s)
- [ ] Fire-and-forget (pas de blocage)
- [ ] Email envoi asynchrone via domain events
- [ ] Pas de N+1 queries
- [ ] Indexes base de données

### ✅ Monitoring

- [ ] Logs structurés (Serilog)
- [ ] Corrélation IDs sur tous les logs
- [ ] Métriques Prometheus
- [ ] Alertes sur échecs critiques
- [ ] Dashboard Grafana

---

## Conclusion

**État actuel :** ~75% implémenté ✅  
**Effort restant :** 4-6 jours développement (Simplifié !)  
**Complexité réduite :** Pas de webhook synchrone, pas d'inbox pattern, pas de message broker  

**Avantages du flow simplifié :**
- ✅ Architecture plus simple et maintenable
- ✅ Pas de dépendance à RabbitMQ/Azure Service Bus
- ✅ Temps de développement réduit de 40%
- ✅ L'application tierce garde le contrôle (elle décide quand créer le compte)
- ✅ Fire-and-forget évite les timeouts et complexités
- ✅ Database schema complet

**Prochaines étapes recommandées :**
1. ✅ ~~Migration database~~ (TERMINÉ)
2. Implémenter EmailService (SendGrid ou SMTP)
3. Créer UserPendingActivationEventHandler
4. Créer les views Razor (Onboarding, Activate)
5. Tests E2E du flow complet
