# API-Only Authentication Flow

## 📋 Architecture Simplifiée

### ✅ Changements
- **API JSON uniquement** : Plus de vues Razor
- **Un seul flux** : Register (fusionne Register + Onboarding)
- **`[ApiController]`** : Hérite de `ControllerBase` au lieu de `Controller`
- **Suppression** : Views/Account/, ViewModels HTML, endpoints GET/POST avec View()
- **CustomConfiguration** : Branding et langues gérés dans un agrégat séparé, partageable entre tenants

---

## 🎯 Endpoints API

### **POST /api/auth/login**
Connexion avec email et mot de passe.

**Request:**
```json
POST /api/auth/login?acr_values=tenant:banking
{
  "email": "user@example.com",
  "password": "MyPassword123"
}
```

**Success (200):**
```json
{
  "message": "Login successful",
  "email": "user@example.com"
}
```

**Errors:**
- `401`: Invalid credentials or tenant access denied
- `400`: Invalid request

---

### **POST /api/auth/logout**
Déconnexion.

**Success (200):**
```json
{
  "message": "Logout successful"
}
```

---

### **POST /api/auth/register**
Demande d'inscription (SANS mot de passe).

**Request:**
```json
{
  "email": "newuser@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "tenantId": "banking"
}
```

**Success (202 Accepted):**
```json
{
  "message": "Registration request submitted. Awaiting validation.",
  "requestId": "abc-123",
  "email": "newuser@example.com",
  "tenantId": "banking",
  "status": "pending"
}
```

**Errors:**
- `409`: User already exists
- `400`: Invalid tenant

---

### **POST /api/auth/activate**
Activation avec mot de passe.

**Request:**
```json
{
  "token": "CfDJ8ABC123...",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "newPassword": "MySecurePassword123",
  "confirmPassword": "MySecurePassword123"
}
```

**Success (200):**
```json
{
  "message": "Account activated successfully",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "newuser@example.com",
  "status": "Active"
}
```

**Errors:**
- `400`: Invalid/expired token

---

### **POST /api/auth/forgot-password**
Demande reset mot de passe.

**Request:**
```json
{
  "email": "user@example.com"
}
```

**Success (200 - Dev):**
```json
{
  "message": "Password reset token generated",
  "email": "user@example.com",
  "token": "CfDJ8XYZ789...",
  "resetUrl": "https://localhost:5001/api/auth/reset-password"
}
```

---

### **POST /api/auth/reset-password**
Reset mot de passe.

**Request:**
```json
{
  "email": "user@example.com",
  "token": "CfDJ8XYZ789...",
  "password": "NewPassword123",
  "confirmPassword": "NewPassword123"
}
```

**Success (200):**
```json
{
  "message": "Password reset successful",
  "email": "user@example.com"
}
```

---

## 🔄 Flux Register Complet

```
1. Client → POST /api/auth/register
   ↓ (202 Accepted)
   
2. IDP → NotificationService → HTTP POST vers app tierce
   ↓
   
3. App tierce → Validation métier → Décision
   ↓ (Si approuvé)
   
4. App tierce → POST /api/users/register
   → Création User (Status=PendingActivation)
   → Domain Event: UserPendingActivationEvent
   ↓
   
5. Event Handler → Envoi email avec token
   ↓
   
6. User → POST /api/auth/activate
   → Password défini
   → Status=Active
   → EmailConfirmed=true
   ↓
   
7. User → POST /api/auth/login ✅
```

---

## 📝 Différences

| Avant | Maintenant |
|-------|------------|
| Views Razor + API | **API uniquement** |
| Register + Onboarding | **Register unique** |
| Auto-register | **Login = compte existant** |
| GET/POST HTML | **POST JSON** |
| ViewModels | **DTOs** |
| `Controller` | **`ControllerBase`** |

---

## ✅ Statut

- ✅ AccountController (API-only)
- ✅ Views/Account/ supprimé
- ✅ Build réussi
- ✅ Flux Register unifié
- ⏳ Tests à faire
