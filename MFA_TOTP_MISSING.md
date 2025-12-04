# MFA/TOTP - Éléments Manquants

## 🎯 **Récapitulatif des 3 Parcours à Implémenter**

### **Parcours 1: Onboarding MFA (First-time setup)**
```
Condition: Client.RequireMfa = true && User.MFAEnabled = false

Flux:
1. User se connecte (email + password)
2. ✅ Credentials valides
3. 🔴 MFA requis mais pas configuré
4. Redirection forcée → POST /mfa/enroll
5. Afficher QR code
6. User scanne avec nouvel authenticator
7. POST /mfa/verify-enrollment (entrer 6 chiffres)
8. ✅ MFA activé + 10 recovery codes générés
9. ✅ User connecté
```

**Endpoints requis:**
- ✅ `POST /api/auth/mfa/enroll` (déjà existant)
- ✅ `POST /api/auth/mfa/verify-enrollment` (déjà existant)

---

### **Parcours 2: Login avec TOTP (Existing users with MFA)**
```
Condition: Client.RequireMfa = true && User.MFAEnabled = true

Flux:
1. User tape email + password
2. ✅ Credentials valides
3. 🔴 MFA activé → Créer cookie "pending_mfa"
4. Redirection → /mfa-verification (form TOTP)
5. User entre 6 chiffres du TOTP
6. ✅ Code valide → Générer JWT + supprimer cookie
7. ✅ User connecté

Fallback si perte device:
- Cliquer "J'ai perdu mon authenticator"
- Parcours 3 (Lost Device Recovery)
```

**Endpoints requis:**
- ✅ `POST /api/auth/login` (modifier pour ajouter MFA logic)
- ✅ `POST /api/auth/mfa-verify` (créer nouveau endpoint)

---

### **Parcours 3: Lost Device Recovery (Device perdu)**
```
Condition: User a perdu son téléphone/authenticator

Flux:
1. Login échoue (pas de code TOTP)
2. Cliquer "J'ai perdu mon authenticator"
3. POST /auth/mfa/lost-device (email)
4. 📧 Email avec lien de vérification (1h)
5. Cliquer lien → GET /auth/mfa/verify-identity
6. Optionnel: Questions de sécurité (date naissance, etc)
7. ✅ Identité vérifiée
8. MFA désactivé automatiquement
9. User reçoit 📧 "MFA réinitialisé, reconnectez-vous"
10. POST /api/auth/login (sans TOTP)
11. ✅ Connecté + redirection forcée vers Parcours 1
12. ✅ User réactive MFA avec nouvel authenticator
```

**Endpoints requis:**
- ❌ `POST /api/auth/mfa/lost-device` (NOUVEAU)
- ❌ `POST /api/auth/mfa/verify-identity` (NOUVEAU)
- ❌ `POST /api/auth/mfa/reset-enrollment` (NOUVEAU)

---

## 🌐 **Workflow Global MFA - Vue d'Ensemble Complète**

### **Diagramme Unifié des 3 Parcours**

```
                    ┌─────────────────────────┐
                    │  User arrive sur /login │
                    │  (email + password)     │
                    └──────────┬──────────────┘
                               │
                    ┌──────────▼──────────┐
                    │ Credentials valides?│
                    └──────┬───────┬──────┘
                      NON  │       │ OUI
                           │       │
                   ┌───────▼──┐    │
                   │ 401 Error│    │
                   └──────────┘    │
                                   │
                        ┌──────────▼──────────────┐
                        │ Client.RequireMfa?      │
                        └──────┬──────────┬───────┘
                          NON  │          │ OUI
                               │          │
                    ┌──────────▼──┐       │
                    │ ✅ JWT token │       │
                    │ ✅ Connecté  │       │
                    └──────────────┘       │
                                           │
                                ┌──────────▼──────────┐
                                │ User.MFAEnabled?    │
                                └──────┬──────┬───────┘
                                  NON  │      │ OUI
                                       │      │
                    ┌──────────────────▼──┐   │
                    │ 🟡 PARCOURS 1       │   │
                    │ Onboarding MFA      │   │
                    │                     │   │
                    │ POST /mfa/enroll    │   │
                    │ ↓                   │   │
                    │ [QR Code]           │   │
                    │ ↓                   │   │
                    │ User scan           │   │
                    │ ↓                   │   │
                    │ POST /mfa/verify-   │   │
                    │ enrollment (6 dig)  │   │
                    │ ↓                   │   │
                    │ ✅ MFA activé       │   │
                    │ ✅ 10 recovery codes│   │
                    │ ✅ JWT + connecté   │   │
                    └─────────────────────┘   │
                                              │
                                   ┌──────────▼──────────────┐
                                   │ 🔵 PARCOURS 2           │
                                   │ Login avec TOTP         │
                                   │                         │
                                   │ Cookie "pending_mfa"    │
                                   │ ↓                       │
                                   │ Redirect /mfa-verify    │
                                   │ ↓                       │
                                   │ User entre TOTP (6 dig) │
                                   │ ↓                       │
                                   │ Code valide?            │
                                   └──────┬──────────┬───────┘
                                     NON  │          │ OUI
                                          │          │
                                    ┌─────▼─────┐    │
                                    │ ❌ Retry  │    │
                                    │ ou        │    │
                                    │ Lost Dev? │    │
                                    └─────┬─────┘    │
                                          │          │
                                          │   ┌──────▼────────┐
                                          │   │ ✅ JWT token  │
                                          │   │ ✅ Connecté   │
                                          │   └───────────────┘
                                          │
                                          │
                            ┌─────────────▼─────────────────┐
                            │ 🔴 PARCOURS 3                 │
                            │ Lost Device Recovery          │
                            │                               │
                            │ POST /mfa/lost-device (email) │
                            │ ↓                             │
                            │ 📧 Email avec lien (1h)       │
                            │ ↓                             │
                            │ User clique lien              │
                            │ ↓                             │
                            │ POST /mfa/verify-identity     │
                            │ (token + questions sécu opt)  │
                            │ ↓                             │
                            │ ✅ Identité vérifiée          │
                            │ ↓                             │
                            │ POST /mfa/reset-enrollment    │
                            │ (token vérifié)               │
                            │ ↓                             │
                            │ 🔄 MFA désactivé              │
                            │ ↓                             │
                            │ 📧 Confirmation email         │
                            │ ↓                             │
                            │ Retour POST /login            │
                            │ ↓                             │
                            │ ✅ Connecté (sans TOTP)       │
                            │ ↓                             │
                            │ 🔁 Redirect → PARCOURS 1      │
                            │ (re-enroll obligatoire)       │
                            └───────────────────────────────┘
```

---

### **Tableau Récapitulatif des Parcours**

| Parcours | Condition Déclencheur | Endpoints Utilisés | Résultat Final |
|----------|----------------------|-------------------|----------------|
| **🟡 Parcours 1<br>Onboarding** | `Client.RequireMfa = true`<br>`User.MFAEnabled = false` | 1. `/mfa/enroll`<br>2. `/mfa/verify-enrollment` | ✅ MFA activé<br>✅ 10 recovery codes<br>✅ User connecté |
| **🔵 Parcours 2<br>Login TOTP** | `Client.RequireMfa = true`<br>`User.MFAEnabled = true` | 1. `/login` (cookie)<br>2. `/mfa-verify` | ✅ JWT token<br>✅ User connecté |
| **🔴 Parcours 3<br>Lost Device** | User perd authenticator<br>TOTP code inaccessible | 1. `/mfa/lost-device`<br>2. `/mfa/verify-identity`<br>3. `/mfa/reset-enrollment`<br>4. Retour → Parcours 1 | 🔄 MFA réinitialisé<br>✅ Re-enroll nouveau TOTP<br>✅ User connecté |

---

### **Scénarios Réels d'Utilisation**

#### **Scénario A: Nouvel utilisateur avec MFA obligatoire**
```
1. Admin crée compte → User reçoit email invitation
2. User clique lien → POST /login (email + password)
3. ✅ Credentials OK → Client.RequireMfa = true → User.MFAEnabled = false
4. 🟡 PARCOURS 1 déclenché automatiquement
5. User scanne QR code → Active MFA → 10 recovery codes générés
6. ✅ User connecté avec MFA actif
```

#### **Scénario B: Utilisateur existant se reconnecte**
```
1. User → POST /login (email + password)
2. ✅ Credentials OK → Client.RequireMfa = true → User.MFAEnabled = true
3. 🔵 PARCOURS 2 déclenché
4. Cookie "pending_mfa" créé → Redirect /mfa-verify
5. User entre code TOTP de son app (6 chiffres)
6. ✅ Code valide → JWT généré
7. ✅ User connecté
```

#### **Scénario C: Utilisateur perd son téléphone**
```
1. User → POST /login (email + password)
2. ✅ Credentials OK → Redirect /mfa-verify
3. ❌ User n'a pas accès au code TOTP
4. Clique "J'ai perdu mon authenticator"
5. 🔴 PARCOURS 3 déclenché
6. POST /mfa/lost-device → 📧 Email reçu avec lien
7. User clique lien → POST /mfa/verify-identity
8. Répond questions de sécurité → ✅ Identité vérifiée
9. POST /mfa/reset-enrollment → MFA désactivé
10. User se reconnecte → 🟡 PARCOURS 1 forcé (re-enroll)
11. User scanne nouveau QR code avec nouveau téléphone
12. ✅ User connecté avec nouveau MFA
```

#### **Scénario D: Client désactive MFA obligatoire**
```
1. Admin change Client.RequireMfa = false
2. User → POST /login (email + password)
3. ✅ Credentials OK → Client.RequireMfa = false
4. ✅ JWT généré directement (pas de MFA check)
5. ✅ User connecté
6. Optionnel: User peut désactiver son MFA dans settings
```

---

### **Points de Décision Clés**

```
Décision 1: MFA requis?
├─ NON → Login direct (JWT)
└─ OUI → Décision 2

Décision 2: MFA déjà configuré?
├─ NON → PARCOURS 1 (Onboarding)
└─ OUI → PARCOURS 2 (Login TOTP)

Décision 3: Code TOTP valide?
├─ OUI → JWT + connecté
└─ NON → Retry ou PARCOURS 3 (Lost Device)

Décision 4: Lost Device - Identité vérifiée?
├─ OUI → Reset MFA → PARCOURS 1 (Re-enroll)
└─ NON → Bloquer accès
```

---

## 📊 **Matrice de Décision Login**

```
┌─────────────────────────────────────────────────────────────┐
│ POST /login (email, password)                               │
│ ✅ Credentials valides?                                     │
│ NON → 401 Unauthorized                                      │
│ OUI → continuer                                             │
└────────────────────┬────────────────────────────────────────┘
                     │
        ┌────────────▼─────────────┐
        │ Client.RequireMfa?       │
        └────┬──────────────┬──────┘
        NON  │              │  OUI
             │              │
        ┌────▼──┐      ┌────▼────────────────┐
        │ JWT ✅│      │ User.MFAEnabled?    │
        │signin │      └────┬──────────┬─────┘
        └───────┘      NON  │          │ OUI
                    ┌───────▼──┐  ┌────▼────────┐
                    │ Onboarding   │ Login TOTP  │
                    │ Parcours 1 │  │ Parcours 2 │
                    └───────────┘  └─────────────┘
```

---

## 🔍 **Clarification: Lost Device vs Reset Enrollment vs Verify Identity**

### **Différence entre les 3 endpoints du Parcours 3**

| Endpoint | But | Input | Output |
|----------|-----|-------|--------|
| **POST /mfa/lost-device** | Initier récupération | Email | Lien de vérification par email |
| **POST /mfa/verify-identity** | Prouver que c'est toi | Token + Questions de sécurité (opt) | Token "verified_identity" |
| **POST /mfa/reset-enrollment** | Désactiver MFA | Token vérifié | MFA disabled, user peut se reconnecter |

### **Flux Séquentiel**

```
┌─────────────────────────────────────┐
│ 1. User perd son authenticator      │
│    Clique "Lost Device"             │
└────────────┬────────────────────────┘
             │
             ↓ POST /mfa/lost-device (email)
             │
    ┌────────────────────────────┐
    │ Backend:                   │
    │ - Cherche l'user par email │
    │ - Génère token (1h exp)    │
    │ - Envoie email avec lien   │
    │ Response: "Check email"    │
    └────────────┬───────────────┘
                 │
    📧 Email reçu avec lien:
    "https://app/verify?token=xxx"
                 │
                 ↓ Cliquer lien
                 │
    ┌──────────────────────────────────┐
    │ 2. POST /mfa/verify-identity     │
    │    (token du lien)               │
    │                                  │
    │ Backend:                         │
    │ - Valide le token               │
    │ - Optionnel: Questions sécu     │
    │ - Génère token "verified"       │
    │ Response: "verified_token"      │
    └────────┬─────────────────────────┘
             │
             ↓ POST /mfa/reset-enrollment (verified_token)
             │
    ┌──────────────────────────────────┐
    │ 3. Backend:                      │
    │ - Valide "verified_token"        │
    │ - user.DisableMFA()              │
    │ - Invalide recovery codes        │
    │ - 📧 Email confirmation          │
    │ Response: "MFA disabled"         │
    └────────┬─────────────────────────┘
             │
             ↓ User peut se reconnecter
             │
    ┌──────────────────────────────────┐
    │ 4. POST /login (email + password)│
    │    (SANS code TOTP)              │
    │                                  │
    │ ✅ Connecté                      │
    │ Redirection vers Parcours 1:     │
    │ Re-enrollment MFA obligatoire    │
    └──────────────────────────────────┘
```

### **Points Clés**

**Lost Device:**
- ✅ PUBLIC (AllowAnonymous)
- ✅ Sécurité: Pas révéler si email existe
- ✅ Envoie email avec lien

**Verify Identity:**
- ✅ PUBLIC (AllowAnonymous)
- ✅ Valide le token du lien
- ✅ Questions de sécurité OPTIONNELLES
- ✅ Retourne token "verified_identity"

**Reset Enrollment:**
- ✅ PUBLIC (AllowAnonymous)
- ✅ Prend le token "verified_identity"
- ✅ **DÉSACTIVE LE MFA**
- ✅ Invalide tous les recovery codes
- ✅ Envoie email de confirmation

### **Validations Essentielles**

```csharp
// Lost Device - Valider email format seulement
if (!IsValidEmail(request.Email))
    return BadRequest("Invalid email format");

// Verify Identity - Valider token et réponses sécu
if (!ValidateToken(request.Token))
    return Unauthorized("Invalid token");

if (request.SecurityAnswers != null)
{
    if (!ValidateSecurityAnswers(user, request.SecurityAnswers))
        return Unauthorized("Wrong answers");
}

// Reset Enrollment - Valider token VERIFIÉd
if (!IsTokenVerified(request.VerificationToken))
    return Unauthorized("Token not verified");
```

---

## 📋 Analyse de l'Implémentation Actuelle

### ✅ **Ce qui EXISTE déjà**

1. **Domain Layer**
   - ✅ `User.EnableMFA()` / `User.DisableMFA()`
   - ✅ `User.MFAEnabled` property
   - ✅ Client.RequireMfa configuration

2. **Application Layer**
   - ✅ `IMfaService` - logique métier
   - ✅ `MfaService.IsMfaRequiredForUserAsync()`
   - ✅ `MfaService.GenerateQrCodeUri()`
   - ✅ `MfaService.FormatKey()`

3. **API Layer (AccountController)**
   - ✅ `POST /api/auth/mfa/enroll` - Génère QR code
   - ✅ `POST /api/auth/mfa/verify-enrollment` - Active MFA après scan
   - ✅ `POST /api/auth/login-with-totp` - Login avec TOTP

4. **Contracts**
   - ✅ `TotpEnrollmentResponse`
   - ✅ `VerifyTotpRequest`
   - ✅ `LoginWithTotpRequest`
   - ✅ `MfaRequiredResponse`

---

## ❌ **Ce qui MANQUE**

### **1. Endpoints API Critiques**

#### **a) Désactiver MFA**
```csharp
/// <summary>
/// Disable MFA for the current user (requires password confirmation)
/// </summary>
[HttpPost("mfa/disable")]
[Authorize]
public async Task<IActionResult> DisableMfa([FromBody] DisableMfaRequest request)
{
    // Vérifier password avant de désactiver
    // Désactiver 2FA dans Identity
    // Appeler domainUser.DisableMFA()
}
```

**Contrat manquant:**
```csharp
public class DisableMfaRequest
{
    public string Password { get; set; } = null!; // Confirmation
}
```

---

#### **b) Statut MFA**
```csharp
/// <summary>
/// Get MFA status for current user
/// </summary>
[HttpGet("mfa/status")]
[Authorize]
public async Task<IActionResult> GetMfaStatus()
{
    // Retourne: MFA requis? MFA activé? Recovery codes restants?
}
```

**Contrat manquant:**
```csharp
public class MfaStatusResponse
{
    public bool MfaRequired { get; set; }
    public bool MfaEnabled { get; set; }
    public int RecoveryCodesRemaining { get; set; }
    public DateTime? MfaEnabledAt { get; set; }
}
```

---

#### **c) Régénérer Recovery Codes**
```csharp
/// <summary>
/// Regenerate recovery codes (requires password)
/// </summary>
[HttpPost("mfa/regenerate-recovery-codes")]
[Authorize]
public async Task<IActionResult> RegenerateRecoveryCodes([FromBody] ConfirmPasswordRequest request)
{
    // Vérifier password
    // Générer nouveaux codes
    // Retourner codes (à sauvegarder côté client)
}
```

**Contrat manquant:**
```csharp
public class ConfirmPasswordRequest
{
    public string Password { get; set; } = null!;
}

public class RecoveryCodesResponse
{
    public string[] RecoveryCodes { get; set; } = Array.Empty<string>();
}
```

---

### **1.1 Exemples de Payloads** 📦

#### **a) POST /api/auth/mfa/enroll**

**Request:**
```json
{} // Pas de body (utilisateur authentifié)
```

**Response (200 OK):**
```json
{
  "qrCodeUri": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAHQAAAB0CAMAAABb...",
  "manualEntryKey": "JBSWY3DPEHPK3PXP",
  "secret": "JBSWY3DPEHPK3PXP"
}
```

---

#### **b) POST /api/auth/mfa/verify-enrollment**

**Request:**
```json
{
  "totpCode": "123456"
}
```

**Response (200 OK):**
```json
{
  "mfaEnabled": true,
  "recoveryCodes": [
    "ABC123-DEF456",
    "GHI789-JKL012",
    "MNO345-PQR678",
    "STU901-VWX234",
    "YZA567-BCD890",
    "EFG123-HIJ456",
    "KLM789-NOP012",
    "QRS345-TUV678",
    "WXY901-ZAB234",
    "CDE567-FGH890"
  ],
  "message": "MFA has been successfully enabled. Please save your recovery codes in a secure location."
}
```

**Response (400 Bad Request):**
```json
{
  "error": "Invalid TOTP code",
  "errorCode": "INVALID_TOTP"
}
```

---

#### **c) POST /api/auth/login-with-totp**

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "totpCode": "123456"
}
```

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "refresh_token_value_here",
  "expiresIn": 3600,
  "tokenType": "Bearer",
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "mfaEnabled": true
  }
}
```

**Response (401 Unauthorized):**
```json
{
  "error": "Invalid TOTP code or credentials",
  "errorCode": "INVALID_MFA_CREDENTIALS",
  "attemptsRemaining": 2
}
```

---

#### **d) POST /api/auth/mfa/disable**

**Request:**
```json
{
  "password": "SecurePassword123!"
}
```

**Response (200 OK):**
```json
{
  "mfaEnabled": false,
  "message": "MFA has been successfully disabled"
}
```

**Response (400 Bad Request - mauvais password):**
```json
{
  "error": "Invalid password",
  "errorCode": "INVALID_PASSWORD"
}
```

---

#### **e) GET /api/auth/mfa/status**

**Request:**
```http
GET /api/auth/mfa/status
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response (200 OK):**
```json
{
  "mfaRequired": true,
  "mfaEnabled": true,
  "recoveryCodesRemaining": 8,
  "mfaEnabledAt": "2025-12-01T14:30:00Z",
  "backupCodesNeverUsed": true
}
```

**Response (200 OK - MFA pas enabled mais requis):**
```json
{
  "mfaRequired": true,
  "mfaEnabled": false,
  "recoveryCodesRemaining": 0,
  "mfaEnabledAt": null,
  "backupCodesNeverUsed": false
}
```

---

#### **f) POST /api/auth/mfa/regenerate-recovery-codes**

**Request:**
```json
{
  "password": "SecurePassword123!"
}
```

**Response (200 OK):**
```json
{
  "recoveryCodes": [
    "ABC123-DEF456",
    "GHI789-JKL012",
    "MNO345-PQR678",
    "STU901-VWX234",
    "YZA567-BCD890",
    "EFG123-HIJ456",
    "KLM789-NOP012",
    "QRS345-TUV678",
    "WXY901-ZAB234",
    "CDE567-FGH890"
  ],
  "message": "Recovery codes have been regenerated. Previous codes are now invalid."
}
```

**Response (400 Bad Request):**
```json
{
  "error": "Invalid password",
  "errorCode": "INVALID_PASSWORD"
}
```

---

### **2. Domain Events Manquants** 📢

```csharp
// À ajouter dans Domain/Users/Events/

public class MfaEnabledEvent : DomainEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
    public DateTime EnabledAt { get; set; }
}

public class MfaDisabledEvent : DomainEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
    public string Reason { get; set; } = null!; // "user_request", "admin_action", etc.
}

public class MfaRecoveryCodeUsedEvent : DomainEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
    public DateTime UsedAt { get; set; }
}
```

**Utilisation:**
```csharp
// Dans User.EnableMFA()
public void EnableMFA()
{
    MFAEnabled = true;
    UpdatedAt = DateTime.UtcNow;
    
    // ✅ Émettre événement
    RaiseDomainEvent(new MfaEnabledEvent
    {
        UserId = Id.Value,
        Email = Email.Value,
        EnabledAt = DateTime.UtcNow
    });
}
```

---

### **2.1 Exemples de Payloads - Événements Domaine** 📦

#### **MfaEnabledEvent**
```json
{
  "eventType": "MfaEnabledEvent",
  "eventId": "550e8400-e29b-41d4-a716-446655440001",
  "aggregateId": "550e8400-e29b-41d4-a716-446655440000",
  "aggregateType": "User",
  "timestamp": "2025-12-04T14:30:00Z",
  "version": 5,
  "data": {
    "userId": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "enabledAt": "2025-12-04T14:30:00Z"
  }
}
```

#### **MfaDisabledEvent**
```json
{
  "eventType": "MfaDisabledEvent",
  "eventId": "550e8400-e29b-41d4-a716-446655440002",
  "aggregateId": "550e8400-e29b-41d4-a716-446655440000",
  "aggregateType": "User",
  "timestamp": "2025-12-04T15:45:00Z",
  "version": 6,
  "data": {
    "userId": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "reason": "user_request",
    "ipAddress": "192.168.1.100"
  }
}
```

#### **MfaRecoveryCodeUsedEvent**
```json
{
  "eventType": "MfaRecoveryCodeUsedEvent",
  "eventId": "550e8400-e29b-41d4-a716-446655440003",
  "aggregateId": "550e8400-e29b-41d4-a716-446655440000",
  "aggregateType": "User",
  "timestamp": "2025-12-04T16:20:00Z",
  "version": 7,
  "data": {
    "userId": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "usedAt": "2025-12-04T16:20:00Z",
    "recoveryCodesRemaining": 9,
    "ipAddress": "192.168.1.100"
  }
}
```

---

### **2.2 Exemples de Payloads - Audit Logs** 📝

#### **Audit Log - MFA Enrollment**
```json
{
  "auditId": "audit-550e8400-e29b-41d4-a716-001",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "action": "MFA_ENROLLMENT_STARTED",
  "resource": "mfa",
  "resourceId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "success",
  "ipAddress": "192.168.1.100",
  "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
  "timestamp": "2025-12-04T14:25:00Z",
  "details": {
    "method": "totp",
    "deviceType": "authenticator_app"
  }
}
```

#### **Audit Log - MFA Verification**
```json
{
  "auditId": "audit-550e8400-e29b-41d4-a716-002",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "action": "MFA_ENROLLMENT_VERIFIED",
  "resource": "mfa",
  "resourceId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "success",
  "ipAddress": "192.168.1.100",
  "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
  "timestamp": "2025-12-04T14:30:00Z",
  "details": {
    "method": "totp",
    "recoveryCodesGenerated": 10
  }
}
```

#### **Audit Log - Failed MFA Attempt**
```json
{
  "auditId": "audit-550e8400-e29b-41d4-a716-003",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "action": "MFA_LOGIN_ATTEMPT",
  "resource": "mfa",
  "resourceId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "failure",
  "ipAddress": "192.168.1.100",
  "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
  "timestamp": "2025-12-04T15:00:00Z",
  "details": {
    "method": "totp",
    "reason": "invalid_code",
    "attemptsRemaining": 2
  }
}
```

#### **Audit Log - MFA Disabled**
```json
{
  "auditId": "audit-550e8400-e29b-41d4-a716-004",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "action": "MFA_DISABLED",
  "resource": "mfa",
  "resourceId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "success",
  "ipAddress": "192.168.1.100",
  "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
  "timestamp": "2025-12-04T15:45:00Z",
  "details": {
    "reason": "user_request",
    "passwordVerified": true
  }
}
```

#### **Audit Log - Recovery Code Used**
```json
{
  "auditId": "audit-550e8400-e29b-41d4-a716-005",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "action": "RECOVERY_CODE_USED",
  "resource": "mfa",
  "resourceId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "success",
  "ipAddress": "192.168.1.100",
  "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
  "timestamp": "2025-12-04T16:20:00Z",
  "details": {
    "recoveryCodesRemaining": 9,
    "isLastCode": false
  }
}
```

---

### **3. Event Handlers Manquants** 🎯

```csharp
// Application/Users/EventHandlers/MfaEnabledEventHandler.cs

public class MfaEnabledEventHandler : IEventHandler<MfaEnabledEvent>
{
    private readonly IEmailService _emailService;

    public async Task HandleAsync(MfaEnabledEvent @event, CancellationToken ct)
    {
        // Envoyer email de confirmation
        await _emailService.SendMfaEnabledNotificationAsync(
            @event.Email,
            @event.EnabledAt);
    }
}
```

```csharp
// Application/Users/EventHandlers/MfaDisabledEventHandler.cs

public class MfaDisabledEventHandler : IEventHandler<MfaDisabledEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<MfaDisabledEventHandler> _logger;

    public async Task HandleAsync(MfaDisabledEvent @event, CancellationToken ct)
    {
        // Log sécurité critique
        _logger.LogWarning(
            "MFA disabled for user {UserId} - Reason: {Reason}",
            @event.UserId, @event.Reason);
        
        // Envoyer email d'alerte sécurité
        await _emailService.SendMfaDisabledSecurityAlertAsync(
            @event.Email,
            @event.Reason);
    }
}
```

---

### **4. Validations Manquantes** ✅

```csharp
// Application/Users/Validators/DisableMfaRequestValidator.cs

public class DisableMfaRequestValidator : IValidator<DisableMfaRequest>
{
    public Task<IDictionary<string, string[]>> ValidateAsync(DisableMfaRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["Password"] = new[] { "Password is required to disable MFA" };
        }

        return Task.FromResult<IDictionary<string, string[]>>(errors);
    }
}
```

---

### **5. Templates Email Manquants** 📧

```csharp
// Infrastructure/Services/EmailTemplates/

public interface IEmailService
{
    // ❌ Manquants:
    Task SendMfaEnabledNotificationAsync(string email, DateTime enabledAt);
    Task SendMfaDisabledSecurityAlertAsync(string email, string reason);
    Task SendRecoveryCodeUsedAlertAsync(string email, int codesRemaining);
}
```

---

### **5.1 Exemples de Payloads - Emails** 📧

#### **Email - MFA Enabled Notification**
```json
{
  "to": "user@example.com",
  "subject": "Two-Factor Authentication (MFA) Successfully Enabled",
  "templateName": "mfa_enabled",
  "variables": {
    "firstName": "John",
    "enabledAt": "2025-12-04T14:30:00Z",
    "deviceInfo": "Google Authenticator",
    "recoveryCodesCount": 10,
    "disableUrl": "https://johodp.example.com/settings/mfa/disable"
  },
  "htmlContent": "<h2>Two-Factor Authentication Enabled</h2><p>Hello John,</p><p>Your account is now secured with two-factor authentication (MFA).</p><p><strong>When:</strong> 2025-12-04 at 14:30 UTC</p><p><strong>Method:</strong> TOTP (Google Authenticator)</p><p><strong>Important:</strong> You have 10 recovery codes saved. Keep them in a safe place.</p><p>If you did not enable MFA, please <a href='https://johodp.example.com/settings/mfa/disable'>disable it immediately</a>.</p>"
}
```

#### **Email - MFA Disabled Security Alert**
```json
{
  "to": "user@example.com",
  "subject": "⚠️ SECURITY ALERT: Two-Factor Authentication (MFA) Disabled",
  "templateName": "mfa_disabled_alert",
  "priority": "high",
  "variables": {
    "firstName": "John",
    "disabledAt": "2025-12-04T15:45:00Z",
    "ipAddress": "192.168.1.100",
    "userAgent": "Chrome on Windows",
    "reason": "User requested",
    "reEnableUrl": "https://johodp.example.com/settings/mfa/enable"
  },
  "htmlContent": "<h2 style='color: #d32f2f;'>Security Alert: MFA Disabled</h2><p>Hello John,</p><p>Your two-factor authentication has been disabled.</p><p><strong>Details:</strong></p><ul><li><strong>When:</strong> 2025-12-04 at 15:45 UTC</li><li><strong>IP Address:</strong> 192.168.1.100</li><li><strong>Device:</strong> Chrome on Windows</li><li><strong>Reason:</strong> User requested</li></ul><p><strong>⚠️ Action Required:</strong> If you did not disable MFA, your account may be compromised.</p><p><a href='https://johodp.example.com/settings/mfa/enable' style='background-color: #d32f2f; color: white; padding: 10px 20px; border-radius: 5px; text-decoration: none;'>Re-enable MFA Now</a></p>"
}
```

#### **Email - Recovery Code Used Alert**
```json
{
  "to": "user@example.com",
  "subject": "⚠️ SECURITY NOTICE: Recovery Code Used",
  "templateName": "recovery_code_used",
  "priority": "high",
  "variables": {
    "firstName": "John",
    "usedAt": "2025-12-04T16:20:00Z",
    "ipAddress": "192.168.1.100",
    "recoveryCodesRemaining": 9,
    "regenerateUrl": "https://johodp.example.com/settings/mfa/recovery-codes"
  },
  "htmlContent": "<h2>Recovery Code Used</h2><p>Hello John,</p><p>A recovery code from your account was used to log in.</p><p><strong>Details:</strong></p><ul><li><strong>When:</strong> 2025-12-04 at 16:20 UTC</li><li><strong>IP Address:</strong> 192.168.1.100</li><li><strong>Codes Remaining:</strong> 9</li></ul><p>Recovery codes are one-time use backup codes. You should generate new recovery codes soon.</p><p><a href='https://johodp.example.com/settings/mfa/recovery-codes'>Regenerate Recovery Codes</a></p>"
}
```

#### **Email - MFA Enrollment Reminder**
```json
{
  "to": "user@example.com",
  "subject": "Action Required: Complete Your Two-Factor Authentication Setup",
  "templateName": "mfa_enrollment_reminder",
  "priority": "high",
  "variables": {
    "firstName": "John",
    "enrollUrl": "https://johodp.example.com/account/mfa/enroll",
    "deadline": "2025-12-11T00:00:00Z",
    "daysRemaining": 7
  },
  "htmlContent": "<h2>Complete Your MFA Setup</h2><p>Hello John,</p><p>Two-factor authentication is now required for your account.</p><p><strong>You have 7 days to complete the setup.</strong></p><p>Benefits of MFA:</p><ul><li>🔒 Enhanced security</li><li>🛡️ Protection against compromised passwords</li><li>✅ Peace of mind</li></ul><p><a href='https://johodp.example.com/account/mfa/enroll' style='background-color: #4CAF50; color: white; padding: 12px 24px; border-radius: 5px; text-decoration: none; font-weight: bold;'>Start MFA Setup Now</a></p>"
}
```

---

### **6. Logs & Audit Manquants** 📝

```csharp
// Infrastructure/Auditing/MfaAuditService.cs

public interface IMfaAuditService
{
    Task LogMfaEnrollmentAsync(Guid userId, string ipAddress);
    Task LogMfaDisabledAsync(Guid userId, string reason, string ipAddress);
    Task LogFailedTotpAttemptAsync(Guid userId, string ipAddress);
    Task LogRecoveryCodeUsedAsync(Guid userId, string ipAddress);
}
```

**Utilisation:**
```csharp
[HttpPost("mfa/verify-enrollment")]
public async Task<IActionResult> VerifyTotpEnrollment(...)
{
    // ...
    await _mfaAuditService.LogMfaEnrollmentAsync(
        user.Id, 
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
}
```

---

### **7. Middleware de Vérification MFA** 🔒

```csharp
// Api/Middleware/RequireMfaMiddleware.cs

public class RequireMfaMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Si utilisateur authentifié ET MFA requis ET pas encore vérifié
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var mfaVerified = context.User.FindFirstValue("mfa_verified");
            
            if (mfaVerified != "true")
            {
                // Vérifier si MFA requis pour cet utilisateur
                var user = await _userRepository.GetByIdAsync(Guid.Parse(userId));
                var mfaRequired = await _mfaService.IsMfaRequiredForUserAsync(user);
                
                if (mfaRequired)
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "MFA verification required",
                        mfaRequired = true
                    });
                    return;
                }
            }
        }
        
        await _next(context);
    }
}
```

---

### **7.1 Exemples de Payloads - Middleware** 🔒

#### **Middleware Response - MFA Required**
```json
{
  "error": "MFA verification required",
  "mfaRequired": true,
  "message": "Please complete MFA verification to access this resource",
  "nextStep": "/api/auth/login-with-totp"
}
```

#### **Middleware Response - MFA Enrollment Pending**
```json
{
  "error": "MFA enrollment required",
  "mfaRequired": true,
  "userStatus": "PendingMfaEnrollment",
  "message": "Your account requires MFA setup before you can proceed",
  "nextStep": "/api/auth/mfa/enroll",
  "deadline": "2025-12-11T00:00:00Z"
}
```

---

### **7.2 Exemples de Payloads - Claims MFA** 🎫

#### **JWT Claims - Après MFA Vérification**
```json
{
  "sub": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "email_verified": true,
  "given_name": "John",
  "family_name": "Doe",
  "tenant_id": "550e8400-e29b-41d4-a716-446655440099",
  "mfa_enabled": true,
  "mfa_verified": "true",
  "mfa_verified_at": "2025-12-04T16:20:00Z",
  "mfa_method": "totp",
  "iat": 1733343600,
  "exp": 1733347200,
  "iss": "https://johodp.example.com",
  "aud": "johodp-api"
}
```

#### **JWT Claims - Avant MFA Vérification (Temporary Token)**
```json
{
  "sub": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "given_name": "John",
  "family_name": "Doe",
  "mfa_enabled": true,
  "mfa_verified": "false",
  "mfa_token_type": "temporary_mfa_token",
  "iat": 1733343600,
  "exp": 1733343900,
  "iss": "https://johodp.example.com",
  "aud": "johodp-mfa-endpoint"
}
```

#### **JWT Claims - Sans MFA Requis**
```json
{
  "sub": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "email_verified": true,
  "given_name": "John",
  "family_name": "Doe",
  "tenant_id": "550e8400-e29b-41d4-a716-446655440099",
  "mfa_enabled": false,
  "mfa_required": false,
  "iat": 1733343600,
  "exp": 1733347200,
  "iss": "https://johodp.example.com",
  "aud": "johodp-api"
}
```

---

### **8. Claims MFA Manquants** 🎫

```csharp
// Après validation TOTP réussie, ajouter claim:

var claims = new List<Claim>
{
    new Claim("mfa_verified", "true"),
    new Claim("mfa_verified_at", DateTime.UtcNow.ToString("o"))
};

await _userManager.AddClaimsAsync(user, claims);
```

---

### **8.1 Exemples de Payloads - Validation Errors** ❌

#### **Validation Error - Missing TOTP Code**
```json
{
  "statusCode": 400,
  "error": "Validation error",
  "details": {
    "totpCode": [
      "TOTP code is required",
      "TOTP code must be 6 digits"
    ]
  }
}
```

#### **Validation Error - Invalid Password**
```json
{
  "statusCode": 400,
  "error": "Validation error",
  "details": {
    "password": [
      "Password is required",
      "Password must contain at least 8 characters",
      "Password must contain uppercase, lowercase, digit and special character"
    ]
  }
}
```

#### **Validation Error - Invalid Recovery Code Format**
```json
{
  "statusCode": 400,
  "error": "Validation error",
  "details": {
    "recoveryCode": [
      "Recovery code is invalid",
      "Recovery code must be in format XXXXXX-XXXXXX"
    ]
  }
}
```

#### **Validation Error - Multiple Fields Invalid**
```json
{
  "statusCode": 400,
  "error": "Validation error",
  "details": {
    "email": ["Email is required"],
    "password": ["Password is required"],
    "totpCode": ["TOTP code is required"]
  }
}
```

---

### **9. Tests Manquants** 🧪

```csharp
// Tests/MfaTests/TotpWorkflowTests.cs

[Fact]
public async Task EnrollMfa_ValidCode_ShouldEnableMfa()
{
    // Arrange: User sans MFA
    // Act: Enroll + verify code
    // Assert: MFA enabled, recovery codes générés
}

[Fact]
public async Task LoginWithTotp_InvalidCode_ShouldFail()
{
    // Arrange: User avec MFA activé
    // Act: Login avec code invalide
    // Assert: Unauthorized
}

[Fact]
public async Task DisableMfa_WithoutPassword_ShouldFail()
{
    // Arrange: User avec MFA
    // Act: Désactiver sans password
    // Assert: BadRequest
}

[Fact]
public async Task LoginWithRecoveryCode_ShouldInvalidateCode()
{
    // Arrange: User avec MFA et recovery codes
    // Act: Login avec recovery code
    // Assert: Code invalidé, codes restants -1
}
```

---

### **9.1 Exemples de Payloads - Scénarios de Test** 🧪

#### **Test Scenario - MFA Enrollment Success**
```json
{
  "testName": "EnrollMfa_ValidCode_ShouldEnableMfa",
  "scenario": "Complete MFA enrollment with valid TOTP code",
  "steps": [
    {
      "step": 1,
      "action": "POST /api/auth/mfa/enroll",
      "request": {},
      "expectedResponse": {
        "qrCodeUri": "data:image/png;base64,...",
        "manualEntryKey": "JBSWY3DPEHPK3PXP"
      }
    },
    {
      "step": 2,
      "action": "POST /api/auth/mfa/verify-enrollment",
      "request": {
        "totpCode": "123456"
      },
      "expectedResponse": {
        "mfaEnabled": true,
        "recoveryCodes": ["ABC123-DEF456", "..."]
      }
    },
    {
      "step": 3,
      "action": "GET /api/auth/mfa/status",
      "request": {},
      "expectedResponse": {
        "mfaRequired": true,
        "mfaEnabled": true,
        "recoveryCodesRemaining": 10
      }
    }
  ],
  "assertions": [
    "User.MFAEnabled == true",
    "User.Status == Active",
    "RecoveryCodes.Count == 10",
    "RecoveryCodes.All(c => !c.Used)"
  ]
}
```

#### **Test Scenario - Login with TOTP Success**
```json
{
  "testName": "LoginWithTotp_ValidCode_ShouldReturnToken",
  "scenario": "Successful login with valid TOTP code",
  "preconditions": [
    "User exists and MFA is enabled",
    "Client.RequireMfa == true",
    "User has valid TOTP secret"
  ],
  "steps": [
    {
      "step": 1,
      "action": "POST /api/auth/login-with-totp",
      "request": {
        "email": "user@example.com",
        "password": "ValidPassword123!",
        "totpCode": "123456"
      },
      "expectedStatusCode": 200,
      "expectedResponse": {
        "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        "tokenType": "Bearer",
        "expiresIn": 3600
      }
    },
    {
      "step": 2,
      "action": "GET /api/auth/me",
      "headers": {
        "Authorization": "Bearer {accessToken}"
      },
      "expectedStatusCode": 200,
      "expectedResponse": {
        "mfaEnabled": true
      }
    }
  ],
  "assertions": [
    "JWT.mfa_verified == 'true'",
    "JWT.mfa_method == 'totp'",
    "User can access protected resources"
  ]
}
```

#### **Test Scenario - Login with Recovery Code**
```json
{
  "testName": "LoginWithRecoveryCode_ValidCode_ShouldInvalidateCode",
  "scenario": "Login using recovery code and invalidate it",
  "preconditions": [
    "User has MFA enabled",
    "User has unused recovery codes"
  ],
  "steps": [
    {
      "step": 1,
      "action": "POST /api/auth/login-with-recovery-code",
      "request": {
        "email": "user@example.com",
        "password": "ValidPassword123!",
        "recoveryCode": "ABC123-DEF456"
      },
      "expectedStatusCode": 200,
      "expectedResponse": {
        "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        "warning": "Recovery code used. You have 9 recovery codes remaining."
      }
    },
    {
      "step": 2,
      "action": "POST /api/auth/login-with-recovery-code",
      "request": {
        "email": "user@example.com",
        "password": "ValidPassword123!",
        "recoveryCode": "ABC123-DEF456"
      },
      "expectedStatusCode": 401,
      "expectedResponse": {
        "error": "Invalid recovery code or credentials"
      }
    }
  ],
  "assertions": [
    "RecoveryCode.Used == true",
    "RecoveryCode.UsedAt != null",
    "Cannot reuse same recovery code",
    "Audit log records recovery code usage"
  ]
}
```

#### **Test Scenario - Disable MFA**
```json
{
  "testName": "DisableMfa_WithValidPassword_ShouldDisable",
  "scenario": "Disable MFA with password confirmation",
  "preconditions": [
    "User has MFA enabled"
  ],
  "steps": [
    {
      "step": 1,
      "action": "POST /api/auth/mfa/disable",
      "request": {
        "password": "ValidPassword123!"
      },
      "expectedStatusCode": 200,
      "expectedResponse": {
        "mfaEnabled": false,
        "message": "MFA has been successfully disabled"
      }
    },
    {
      "step": 2,
      "action": "GET /api/auth/mfa/status",
      "expectedResponse": {
        "mfaEnabled": false,
        "recoveryCodesRemaining": 0
      }
    }
  ],
  "assertions": [
    "User.MFAEnabled == false",
    "RecoveryCodes are invalidated",
    "MfaDisabledEvent is raised",
    "Security email is sent"
  ]
}
```

#### **Test Scenario - MFA Required But Not Enrolled**
```json
{
  "testName": "LoginWithoutMfa_WhenMfaRequired_ShouldFail",
  "scenario": "User cannot login without MFA when it's required",
  "preconditions": [
    "Client.RequireMfa == true",
    "User.MFAEnabled == false",
    "User.Status == PendingMfaEnrollment"
  ],
  "steps": [
    {
      "step": 1,
      "action": "POST /api/auth/login",
      "request": {
        "email": "user@example.com",
        "password": "ValidPassword123!"
      },
      "expectedStatusCode": 403,
      "expectedResponse": {
        "error": "MFA enrollment required",
        "userStatus": "PendingMfaEnrollment",
        "nextStep": "/api/auth/mfa/enroll"
      }
    }
  ],
  "assertions": [
    "User cannot login",
    "User is redirected to MFA enrollment",
    "LoginAttempt is logged"
  ]
}
```

---

### **10. Documentation API Manquante** 📖

```csharp
// Ajouter dans Swagger:
[SwaggerOperation(
    Summary = "Enroll TOTP authenticator",
    Description = @"
        Generates a QR code for TOTP enrollment.
        
        **Flow:**
        1. User calls this endpoint (authenticated)
        2. Backend generates shared secret
        3. Frontend displays QR code
        4. User scans with authenticator app
        5. User calls /mfa/verify-enrollment with TOTP code
        
        **Requirements:**
        - User must be authenticated
        - MFA must be required for user's tenant
    ",
    Tags = new[] { "MFA / TOTP" }
)]
```

---

---

## 🔄 **Flux Login Simplifié avec Strategy Pattern + Cookie MFA**

### **Architecture du Flux**

```
┌─────────────────────────────────────────────────────────────────┐
│ POST /login (email, password, tenant)                           │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ↓
        ┌────────────────────────┐
        │ Valider credentials    │
        │ (email + password)     │
        └────────┬───────────────┘
                 │
        ┌────────▼─────────────────────┐
        │ Credentials valides?         │
        │ NON → 401 Unauthorized       │
        │ OUI → continuer              │
        └────────┬─────────────────────┘
                 │
        ┌────────▼──────────────────────┐
        │ MFA requis pour le client?    │
        │ (Client.RequireMfa)           │
        └────┬───────────────────────┬──┘
        NON  │                       │  OUI
             │                       │
        ┌────▼──────────────┐   ┌───▼──────────────────────────┐
        │ Générer JWT token │   │ Créer cookie "pending_mfa"   │
        │ Sign in           │   │ Rediriger vers /mfa-verify   │
        │ Retourner token   │   └────┬───────────────────────┬──┘
        └─────────────────┬─┘        │                       │
                          │      ┌───▼──────────────────────────┐
                          │      │ POST /mfa-verify             │
                          │      │ (totpCode + cookie)          │
                          │      └────┬───────────────────────┬──┘
                          │           │                       │
                          │      ┌────▼──────────────┐   ┌───▼──────┐
                          │      │ TOTP valide?      │   │ NON      │
                          │      │ OUI → continuer   │   │ Erreur   │
                          │      └────┬──────────────┘   └──────────┘
                          │           │
                          │      ┌────▼──────────────────────┐
                          │      │ Générer JWT token         │
                          │      │ Supprimer cookie pending  │
                          │      │ Sign in                   │
                          │      │ Retourner token           │
                          │      └────┬───────────────────────┘
                          │           │
                          └───────────▼──────────────┘
                                      │
                          ┌───────────▼─────────────┐
                          │ 200 OK + JWT Token      │
                          │ User authenticated      │
                          └─────────────────────────┘
```

### **Implémentation avec Strategy Pattern**

```csharp
// Strategy Interface
public interface ILoginStrategy
{
    Task<LoginResult> HandleLoginAsync(User user, Client client);
}

// Result object
public enum LoginResultType
{
    Success,           // JWT généré, user connecté
    MfaPending,        // Cookie créé, redirection vers /mfa-verify
    InvalidCredentials // Credentials invalides
}

public class LoginResult
{
    public LoginResultType Type { get; set; }
    public string? Token { get; set; }
    public string? ErrorMessage { get; set; }
}

// Strategy 1: Sans MFA
public class NonMfaLoginStrategy : ILoginStrategy
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<NonMfaLoginStrategy> _logger;

    public async Task<LoginResult> HandleLoginAsync(User user, Client client)
    {
        var token = await _tokenService.GenerateTokenAsync(user, client);
        
        _logger.LogInformation(
            "User {UserId} logged in without MFA",
            user.Id);

        return new LoginResult
        {
            Type = LoginResultType.Success,
            Token = token
        };
    }
}

// Strategy 2: Avec MFA
public class MfaLoginStrategy : ILoginStrategy
{
    private readonly IMfaService _mfaService;
    private readonly ILogger<MfaLoginStrategy> _logger;

    public async Task<LoginResult> HandleLoginAsync(User user, Client client)
    {
        // Vérifier que l'utilisateur a MFA activé
        if (!user.MFAEnabled)
        {
            _logger.LogWarning(
                "User {UserId} MFA required but not enabled",
                user.Id);

            return new LoginResult
            {
                Type = LoginResultType.InvalidCredentials,
                ErrorMessage = "MFA is required but not configured"
            };
        }

        // Cookie sera créé par le contrôleur
        _logger.LogInformation(
            "User {UserId} requires MFA verification",
            user.Id);

        return new LoginResult
        {
            Type = LoginResultType.MfaPending
        };
    }
}

// Contrôleur Login Unifié
[ApiController]
[Route("api/auth")]
public class AccountController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IClientService _clientService;
    private readonly ITokenService _tokenService;
    private readonly IMfaService _mfaService;
    private readonly ILogger<AccountController> _logger;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // 1. Valider credentials
        var user = await _userService.ValidateCredentialsAsync(
            request.Email,
            request.Password);

        if (user == null)
        {
            _logger.LogWarning("Failed login attempt for {Email}", request.Email);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        // 2. Récupérer le client
        var client = await _clientService.GetClientAsync(request.TenantId);
        if (client == null)
        {
            return NotFound(new { error = "Client not found" });
        }

        // 3. Sélectionner la stratégie
        ILoginStrategy strategy = client.RequireMfa
            ? new MfaLoginStrategy(_mfaService, _logger)
            : new NonMfaLoginStrategy(_tokenService, _logger);

        // 4. Exécuter la stratégie
        var result = await strategy.HandleLoginAsync(user, client);

        return result.Type switch
        {
            LoginResultType.Success =>
                Ok(new { accessToken = result.Token, tokenType = "Bearer" }),

            LoginResultType.MfaPending =>
            {
                // Créer cookie de session pour /mfa-verify
                Response.Cookies.Append(
                    "pending_mfa",
                    $"{user.Id}:{client.Id}",
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTime.UtcNow.AddMinutes(5)
                    });

                return Redirect("/mfa-verification");
            },

            _ => Unauthorized(new { error = result.ErrorMessage })
        };
    }

    [HttpPost("mfa-verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyRequest request)
    {
        // 1. Récupérer le cookie
        if (!Request.Cookies.TryGetValue("pending_mfa", out var pendingMfaCookie))
        {
            return Unauthorized(new { error = "No pending MFA session" });
        }

        var parts = pendingMfaCookie.Split(":");
        var userId = Guid.Parse(parts[0]);
        var clientId = Guid.Parse(parts[1]);

        // 2. Récupérer l'utilisateur
        var user = await _userService.GetUserByIdAsync(userId);
        var client = await _clientService.GetClientAsync(clientId);

        // 3. Valider le code TOTP
        var isValid = await _mfaService.ValidateTotpCodeAsync(
            user,
            request.TotpCode);

        if (!isValid)
        {
            _logger.LogWarning(
                "Invalid TOTP attempt for user {UserId}",
                userId);

            return BadRequest(new
            {
                error = "Invalid TOTP code",
                message = "Please try again"
            });
        }

        // 4. Générer le token
        var token = await _tokenService.GenerateTokenAsync(user, client);

        // 5. Supprimer le cookie
        Response.Cookies.Delete("pending_mfa");

        _logger.LogInformation(
            "User {UserId} successfully verified MFA",
            userId);

        return Ok(new
        {
            accessToken = token,
            tokenType = "Bearer"
        });
    }
}
```

---

## 🎯 **Checklist de Complétion**

### **Endpoints API Critiques**
- [ ] ✅ `POST /login` - Login unifié (existant, ajouter MFA logic)
- [ ] ✅ `POST /mfa-verify` - Vérifier TOTP après login (NOUVEAU)
- [ ] `POST /api/auth/mfa/disable` - Désactiver MFA
- [ ] `GET /api/auth/mfa/status` - Statut MFA
- [ ] `POST /api/auth/mfa/lost-device` - Initier recovery
- [ ] `POST /api/auth/mfa/verify-identity` - Vérifier identité
- [ ] `POST /api/auth/mfa/reset-enrollment` - Réinitialiser MFA

### **Strategy Pattern**
- [ ] `ILoginStrategy` interface
- [ ] `NonMfaLoginStrategy` implementation
- [ ] `MfaLoginStrategy` implementation
- [ ] `LoginResult` class
- [ ] Modifier `AccountController.Login` pour utiliser les stratégies

### **Cookie Management**
- [ ] Cookie "pending_mfa" avec UserId + ClientId
- [ ] Expiration 5 minutes
- [ ] HttpOnly + Secure flags

### **Domain Events**
- [ ] `MfaEnabledEvent`
- [ ] `MfaDisabledEvent`
- [ ] `MfaRecoveryCodeUsedEvent`

### **Services & Infrastructure**
- [ ] `IMfaAuditService` - Audit trail MFA
- [ ] Email templates (MFA enabled, disabled, recovery used)

### **Validateurs**
- [ ] `DisableMfaRequestValidator`
- [ ] `RegenerateRecoveryCodesRequestValidator`
- [ ] `MfaVerifyRequestValidator`

### **Tests**
- [ ] Tests login sans MFA
- [ ] Tests login avec MFA (success)
- [ ] Tests login avec MFA (invalid code + retry)
- [ ] Tests recovery codes
- [ ] Tests désactivation MFA

### **Documentation**
- [ ] Swagger documentation endpoints
- [ ] Diagramme flux login

---

## 🚀 **Ordre d'Implémentation Recommandé**

**Phase 1 - Strategy Pattern (Core Login)**
- [ ] Créer `ILoginStrategy` et implementations
- [ ] Modifier `AccountController.Login` 
- [ ] Ajouter cookie "pending_mfa"
- [ ] Tests login flow

**Phase 2 - Endpoints Manquants**
- [ ] `/mfa/disable`
- [ ] `/mfa/status`
- [ ] `/mfa/regenerate-recovery-codes`
- [ ] `/login-with-recovery-code`

**Phase 3 - Events & Notifications**
- [ ] Domain events
- [ ] Event handlers
- [ ] Email templates
- [ ] Audit service

**Phase 4 - Polish**
- [ ] Tests complets
- [ ] Documentation Swagger
- [ ] Validation edge cases

---

## 📊 **Index des Payloads Documentés**

| Catégorie | Type | Description | Section |
|-----------|------|-------------|---------|
| **API Requests** | POST | `/mfa/enroll` - Démarrer enrollment | 1.1a |
| **API Requests** | POST | `/mfa/verify-enrollment` - Vérifier code TOTP | 1.1b |
| **API Requests** | POST | `/login-with-totp` - Login avec TOTP | 1.1c |
| **API Requests** | POST | `/mfa/disable` - Désactiver MFA | 1.1d |
| **API Requests** | GET | `/mfa/status` - Statut MFA | 1.1e |
| **API Requests** | POST | `/mfa/regenerate-recovery-codes` - Régénérer codes | 1.1f |
| **API Requests** | POST | `/login-with-recovery-code` - Login backup | 1.1g |
| **Domain Events** | JSON | `MfaEnabledEvent` - MFA activé | 2.1 |
| **Domain Events** | JSON | `MfaDisabledEvent` - MFA désactivé | 2.1 |
| **Domain Events** | JSON | `MfaRecoveryCodeUsedEvent` - Code de recovery utilisé | 2.1 |
| **Audit Logs** | JSON | MFA Enrollment Started | 2.2 |
| **Audit Logs** | JSON | MFA Enrollment Verified | 2.2 |
| **Audit Logs** | JSON | Failed MFA Attempt | 2.2 |
| **Audit Logs** | JSON | MFA Disabled | 2.2 |
| **Audit Logs** | JSON | Recovery Code Used | 2.2 |
| **Emails** | JSON | MFA Enabled Notification | 5.1 |
| **Emails** | JSON | MFA Disabled Security Alert | 5.1 |
| **Emails** | JSON | Recovery Code Used Alert | 5.1 |
| **Emails** | JSON | MFA Enrollment Reminder | 5.1 |
| **Middleware** | JSON | MFA Required Response | 7.1 |
| **Middleware** | JSON | MFA Enrollment Pending Response | 7.1 |
| **JWT Claims** | JSON | Claims après MFA Vérification | 7.2 |
| **JWT Claims** | JSON | Temporary Token Claims | 7.2 |
| **JWT Claims** | JSON | Claims sans MFA | 7.2 |
| **Validation Errors** | JSON | Missing TOTP Code | 8.1 |
| **Validation Errors** | JSON | Invalid Password | 8.1 |
| **Validation Errors** | JSON | Invalid Recovery Code | 8.1 |
| **Validation Errors** | JSON | Multiple Fields Invalid | 8.1 |
| **Test Scenarios** | JSON | MFA Enrollment Success Flow | 9.1 |
| **Test Scenarios** | JSON | Login with TOTP Success | 9.1 |
| **Test Scenarios** | JSON | Login with Recovery Code | 9.1 |
| **Test Scenarios** | JSON | Disable MFA | 9.1 |
| **Test Scenarios** | JSON | MFA Required But Not Enrolled | 9.1 |

**Total: 33 exemples de payloads couvrant tous les cas d'usage MFA/TOTP**

