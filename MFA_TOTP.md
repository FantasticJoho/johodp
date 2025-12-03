# MFA TOTP - Double Authentification

## Vue d'ensemble

Le système MFA (Multi-Factor Authentication) avec TOTP (Time-based One-Time Password) permet une authentification à deux facteurs lors de l'activation et de la connexion.

**Condition** : Le MFA est requis si le `Client` attaché au `Tenant` a `RequireMfa = true`.

## Architecture

```
User
  └── TenantId
        └── Tenant
              └── ClientId
                    └── Client
                          └── RequireMfa (true/false)
```

Si `Client.RequireMfa = true`, alors :
- L'utilisateur **doit** configurer TOTP lors de l'activation
- L'utilisateur **doit** fournir le code TOTP à chaque connexion

## Flow d'activation avec MFA

### 1. Activation du compte

```http
POST /api/auth/activate
Content-Type: application/json

{
  "token": "activation-token-from-email",
  "userId": "user-guid",
  "newPassword": "SecureP@ssw0rd",
  "confirmPassword": "SecureP@ssw0rd"
}
```

**Réponse si MFA requis** :
```json
{
  "message": "Account activated successfully",
  "userId": "guid",
  "email": "user@example.com",
  "status": "Active",
  "mfaRequired": true,
  "mfaSetupUrl": "/api/auth/mfa/enroll"
}
```

### 2. Enrollment TOTP (Authentificateur)

```http
POST /api/auth/mfa/enroll
Authorization: Bearer <access_token>
```

**Réponse** :
```json
{
  "sharedKey": "JBSWY3DPEHPK3PXP",
  "qrCodeUri": "otpauth://totp/Johodp:user@example.com?secret=JBSWY3DPEHPK3PXP&issuer=Johodp&digits=6",
  "manualEntryKey": "jbsw y3dp ehpk 3pxp"
}
```

**Actions utilisateur** :
1. Scanner le `qrCodeUri` avec une app d'authentification (Google Authenticator, Microsoft Authenticator, Authy, etc.)
2. OU saisir manuellement `manualEntryKey` dans l'app

### 3. Vérification du code TOTP

```http
POST /api/auth/mfa/verify-enrollment
Authorization: Bearer <access_token>
Content-Type: application/json

{
  "code": "123456"
}
```

**Réponse** :
```json
{
  "message": "Two-factor authentication enabled successfully",
  "recoveryCodes": [
    "ab12cd34ef56",
    "gh78ij90kl12",
    "mn34op56qr78",
    "st90uv12wx34",
    "yz56ab78cd90",
    "ef12gh34ij56",
    "kl78mn90op12",
    "qr34st56uv78",
    "wx90yz12ab34",
    "cd56ef78gh90"
  ]
}
```

⚠️ **Important** : Sauvegarder les `recoveryCodes` ! Ils permettent de se connecter si l'appareil TOTP est perdu.

## Flow de connexion avec MFA

### 1. Première tentative de connexion

```http
POST /api/auth/login-with-totp
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecureP@ssw0rd"
}
```

**Réponse si MFA requis** :
```json
{
  "mfaRequired": true,
  "mfaMethod": "totp",
  "message": "Two-factor authentication code required"
}
```

### 2. Connexion avec code TOTP

```http
POST /api/auth/login-with-totp
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecureP@ssw0rd",
  "totpCode": "123456"
}
```

**Réponse succès** :
```json
{
  "message": "Login successful",
  "userId": "guid",
  "email": "user@example.com",
  "mfaVerified": true
}
```

## Configuration d'un Client avec MFA obligatoire

### Via API

```http
PUT /api/clients/{clientId}
Content-Type: application/json

{
  "requireMfa": true
}
```

### Via domaine (code)

```csharp
var client = await _clientRepository.GetByIdAsync(clientId);
client.EnableMfa();
await _unitOfWork.SaveChangesAsync();
```

## Vérification si MFA est requis

La logique de vérification se fait automatiquement :

```csharp
// Dans AccountController
private async Task<bool> IsMfaRequiredForTenant(TenantId tenantId)
{
    if (tenantId == null)
        return false;

    // 1. Récupérer le tenant
    var tenant = await _tenantRepository.GetByIdAsync(tenantId);
    if (tenant == null || string.IsNullOrEmpty(tenant.ClientId))
        return false;

    // 2. Récupérer le client associé
    var client = await _clientRepository.GetByClientNameAsync(tenant.ClientId);
    
    // 3. Vérifier si MFA est requis
    return client?.RequireMfa ?? false;
}
```

**Cascade** : `User` → `TenantId` → `Tenant.ClientId` → `Client.RequireMfa`

## Endpoints disponibles

| Endpoint | Méthode | Auth | Description |
|----------|---------|------|-------------|
| `/api/auth/activate` | POST | ❌ | Active le compte et indique si MFA requis |
| `/api/auth/mfa/enroll` | POST | ✅ | Génère QR code pour enrollment TOTP |
| `/api/auth/mfa/verify-enrollment` | POST | ✅ | Vérifie code TOTP et active 2FA |
| `/api/auth/login-with-totp` | POST | ❌ | Connexion avec mot de passe + TOTP |

## Modèle de données

### ASP.NET Identity (Infrastructure)

- `User.TwoFactorEnabled` : `true` si TOTP activé
- `UserTokens` : Stocke le `SharedKey` TOTP (chiffré)
- `RecoveryCodes` : Codes de récupération (hachés)

### Domaine

- `User.MFAEnabled` : Flag domain indiquant si MFA est actif
- `Client.RequireMfa` : Règle métier indiquant si MFA est obligatoire

## Codes de récupération

Si l'utilisateur perd son appareil TOTP, il peut utiliser un code de récupération (usage unique) :

```http
POST /api/auth/login-with-recovery-code
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecureP@ssw0rd",
  "recoveryCode": "ab12cd34ef56"
}
```

## Sécurité

✅ **Bonnes pratiques implémentées** :
- TOTP basé sur RFC 6238 (standard OATH)
- SharedKey généré aléatoirement par ASP.NET Identity
- Codes TOTP valides 30 secondes
- Window de validation : ±1 intervalle (90 secondes total)
- Recovery codes hachés (non réversibles)
- 10 recovery codes générés, usage unique

## Tests

### Test d'enrollment TOTP

```bash
# 1. Activer un compte
POST /api/auth/activate
{
  "token": "...",
  "userId": "...",
  "newPassword": "Test123!",
  "confirmPassword": "Test123!"
}

# 2. Se connecter pour obtenir un token
POST /api/auth/login
{
  "email": "test@example.com",
  "password": "Test123!"
}

# 3. Enrollment TOTP
POST /api/auth/mfa/enroll
Authorization: Bearer <token>

# 4. Utiliser une app TOTP pour générer un code

# 5. Vérifier le code
POST /api/auth/mfa/verify-enrollment
Authorization: Bearer <token>
{
  "code": "123456"
}
```

### Test de connexion avec TOTP

```bash
# 1. Connexion sans code (doit retourner mfaRequired: true)
POST /api/auth/login-with-totp
{
  "email": "test@example.com",
  "password": "Test123!"
}

# 2. Connexion avec code TOTP
POST /api/auth/login-with-totp
{
  "email": "test@example.com",
  "password": "Test123!",
  "totpCode": "123456"
}
```

## Troubleshooting

### Erreur : "MFA is not required for your account"

**Cause** : Le client associé au tenant n'a pas `RequireMfa = true`

**Solution** :
```bash
PUT /api/clients/{clientId}
{
  "requireMfa": true
}
```

### Erreur : "Invalid verification code"

**Causes possibles** :
- Code expiré (30 secondes de validité)
- Horloge système désynchronisée
- Mauvaise saisie du code

**Solutions** :
- Vérifier l'heure système (NTP)
- Réessayer avec un nouveau code
- Utiliser un recovery code si disponible

### Erreur : "Two-factor authentication is required but not enrolled"

**Cause** : L'utilisateur doit configurer TOTP avant de pouvoir se connecter

**Solution** : Suivre le flow d'enrollment TOTP

## Références

- **RFC 6238** : TOTP - Time-Based One-Time Password Algorithm
- **ASP.NET Core Identity** : Two-Factor Authentication
- **Google Authenticator** : Compatible TOTP app
- **Microsoft Authenticator** : Compatible TOTP app
