# 🔐 MFA par Client - Guide Complet

## Vue d'ensemble

Cette implémentation permet d'activer le MFA (Multi-Factor Authentication) uniquement pour certains clients OAuth2/OIDC spécifiques, en utilisant Microsoft Authenticator ou d'autres méthodes d'authentification forte.

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                    Flux d'authentification                    │
└─────────────────────────┬────────────────────────────────────┘
                          │
                          ▼
          1. User entre credentials (email/password)
                          │
                          ▼
               2. Valider credentials
                          │
                          ▼
          3. Extraire client_id de returnUrl
                          │
                          ▼
       4. Vérifier si Client.RequireMfa = true
                          │
          ┌───────────────┴───────────────┐
          │ NO                           │ YES
          ▼                              ▼
    Connecter user              Initier demande MFA
    directement                 (Microsoft Authenticator)
          │                              │
          │                              ▼
          │                    Envoyer push notification
          │                              │
          │                              ▼
          │                    Attendre validation user
          │                              │
          │                ┌─────────────┴──────────────┐
          │                │ Approuvé              │ Rejeté
          │                ▼                       ▼
          │          Connecter user          Refuser connexion
          │                │                       │
          └────────────────┴───────────────────────┘
                          │
                          ▼
              Rediriger vers returnUrl
```

## Configuration

### 1. Base de données

La colonne `RequireMfa` a été ajoutée à la table `clients` :

```sql
ALTER TABLE clients 
ADD COLUMN "RequireMfa" boolean NOT NULL DEFAULT false;
```

**Migration:** `20251124131813_AddRequireMfaToClient`

### 2. Domaine - Client Aggregate

```csharp
// Domain/Clients/Aggregates/Client.cs
public class Client : AggregateRoot
{
    public bool RequireMfa { get; private set; }

    public static Client Create(
        string clientName,
        string[] allowedScopes,
        bool requireConsent = true,
        bool requireMfa = false)
    {
        // ... création client avec RequireMfa
    }

    public void EnableMfa()
    {
        RequireMfa = true;
    }

    public void DisableMfa()
    {
        RequireMfa = false;
    }
}
```

### 3. Application - DTOs et Commands

```csharp
// CreateClientDto
public class CreateClientDto
{
    public bool RequireMfa { get; set; } = false;
}

// UpdateClientDto
public class UpdateClientDto
{
    public bool? RequireMfa { get; set; }
}
```

### 4. Service MFA

```csharp
// IMfaAuthenticationService
public interface IMfaAuthenticationService
{
    Task<MfaPendingRequest> InitiateMfaAsync(
        Guid userId, 
        string email, 
        string clientId, 
        CancellationToken cancellationToken = default);

    Task<bool> ValidateMfaAsync(
        Guid requestId, 
        string? verificationCode = null, 
        CancellationToken cancellationToken = default);

    Task ApproveMfaAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task RejectMfaAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<MfaPendingRequest?> GetMfaRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
}
```

## Utilisation

### Créer un client avec MFA

```http
POST /api/clients
Content-Type: application/json

{
  "clientName": "secure-banking-app",
  "allowedScopes": [
    "openid",
    "profile",
    "email",
    "johodp.api"
  ],
  "requireConsent": true,
  "requireMfa": true  ← Active le MFA pour ce client
}
```

### Activer/Désactiver le MFA pour un client existant

```http
PUT /api/clients/{clientId}
Content-Type: application/json

{
  "requireMfa": true
}
```

## Flux d'authentification avec MFA

### 1. Login classique (sans MFA)

```http
POST /Account/Login
Content-Type: application/x-www-form-urlencoded

email=user@example.com
&password=P@ssw0rd123!
&returnUrl=/connect/authorize?client_id=my-app&...

→ Retour 302 Redirect vers returnUrl
```

### 2. Login avec MFA requis

#### Étape 1: Initier la connexion

```http
POST /Account/Login
Content-Type: application/x-www-form-urlencoded

email=user@example.com
&password=P@ssw0rd123!
&returnUrl=/connect/authorize?client_id=secure-banking-app&...

→ Client requiert MFA
→ Génération d'une demande MFA
→ Retour 200 OK avec view MFA pending
```

#### Étape 2: Push notification Microsoft Authenticator

```
┌──────────────────────────────────────┐
│   Microsoft Authenticator App        │
├──────────────────────────────────────┤
│  🔔 Nouvelle demande de connexion    │
│                                      │
│  Application: Secure Banking App     │
│  Email: user@example.com             │
│  Code: 123456                        │
│                                      │
│  [Approuver]     [Refuser]          │
└──────────────────────────────────────┘
```

#### Étape 3: Vérifier le statut MFA

```http
GET /api/mfa/status/{requestId}

Response:
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Approved",  // ou "Pending", "Rejected", "Expired"
  "expiresAt": "2025-11-24T13:25:00Z"
}
```

#### Étape 4: Finaliser la connexion

```http
POST /Account/MfaValidate
Content-Type: application/x-www-form-urlencoded

requestId=550e8400-e29b-41d4-a716-446655440000
&returnUrl=/connect/authorize?client_id=secure-banking-app&...

→ Validation réussie
→ Connexion user
→ Retour 302 Redirect vers returnUrl
```

## Intégration Microsoft Authenticator

### Configuration (TODO - Implémentation future)

```json
{
  "MicrosoftAuthenticator": {
    "TenantId": "your-azure-ad-tenant-id",
    "ClientId": "your-app-registration-client-id",
    "ClientSecret": "your-client-secret",
    "NotificationUrl": "https://graph.microsoft.com/v1.0/...",
    "Enabled": true
  }
}
```

### Alternatives supportées

1. **Microsoft Authenticator** (Push notifications) - Recommandé
   - Configuration via Azure AD
   - Push notifications en temps réel
   - Biométrie sur mobile

2. **TOTP (Time-Based One-Time Password)**
   - Google Authenticator
   - Authy
   - Codes à 6 chiffres

3. **SMS** (Moins sécurisé)
   - Via Twilio ou similaire
   - Code envoyé par SMS

4. **Email** (Moins sécurisé)
   - Code envoyé par email
   - Backup method

## Endpoints API

### GET /api/mfa/status/{requestId}
Vérifie le statut d'une demande MFA

**Response:**
```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Pending",
  "expiresAt": "2025-11-24T13:25:00Z"
}
```

### POST /api/mfa/approve (Dev/Testing uniquement)
Simule l'approbation Microsoft Authenticator

```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000"
}
```

### POST /api/mfa/reject (Dev/Testing uniquement)
Simule le rejet Microsoft Authenticator

```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000"
}
```

## Tests

### Test 1: Client sans MFA

```bash
# 1. Créer client sans MFA
curl -X POST http://localhost:5000/api/clients \
  -H "Content-Type: application/json" \
  -d '{
    "clientName": "simple-app",
    "allowedScopes": ["openid", "profile"],
    "requireMfa": false
  }'

# 2. Login
curl -X POST http://localhost:5000/Account/Login \
  -d "email=test@example.com&password=P@ssw0rd123!&returnUrl=/connect/authorize?client_id=simple-app"

# ✅ Résultat: Connexion immédiate, pas de MFA
```

### Test 2: Client avec MFA

```bash
# 1. Créer client avec MFA
curl -X POST http://localhost:5000/api/clients \
  -H "Content-Type: application/json" \
  -d '{
    "clientName": "secure-app",
    "allowedScopes": ["openid", "profile"],
    "requireMfa": true
  }'

# 2. Login
curl -X POST http://localhost:5000/Account/Login \
  -d "email=test@example.com&password=P@ssw0rd123!&returnUrl=/connect/authorize?client_id=secure-app"

# ✅ Résultat: Demande MFA générée, en attente d'approbation

# 3. Simuler approbation Microsoft Authenticator (DEV)
curl -X POST http://localhost:5000/api/mfa/approve \
  -H "Content-Type: application/json" \
  -d '{"requestId": "REQUEST_ID_FROM_STEP_2"}'

# 4. Valider MFA
curl -X POST http://localhost:5000/Account/MfaValidate \
  -d "requestId=REQUEST_ID_FROM_STEP_2&returnUrl=/connect/authorize?client_id=secure-app"

# ✅ Résultat: Connexion complète, redirect vers returnUrl
```

## Scénarios d'utilisation

### Scénario 1: Application bancaire (MFA requis)

```
Client: "mobile-banking"
RequireMfa: true

→ Tous les utilisateurs DOIVENT valider MFA
→ Microsoft Authenticator recommandé
→ Protection forte des transactions financières
```

### Scénario 2: Application grand public (pas de MFA)

```
Client: "social-media-app"
RequireMfa: false

→ Connexion simple et rapide
→ Pas de friction utilisateur
→ UX optimisée
```

### Scénario 3: Dashboard administrateur (MFA requis)

```
Client: "admin-dashboard"
RequireMfa: true

→ Protection accès admin
→ MFA obligatoire pour tous les admins
→ Audit trail complet
```

## Sécurité

### ⚠️ Points d'attention

1. **Expiration des demandes MFA**
   - Timeout: 5 minutes
   - Auto-nettoyage après expiration

2. **Protection contre le brute force**
   - Rate limiting sur /Account/Login
   - Verrouillage après X tentatives échouées

3. **Stockage des demandes**
   - En mémoire (ConcurrentDictionary)
   - ⚠️ Pertes lors du redémarrage app
   - TODO: Persister dans Redis/PostgreSQL pour production

4. **HTTPS obligatoire**
   - Certificat SSL requis en production
   - Cookies SecureOnly

### ✅ Bonnes pratiques

- Utiliser MFA pour applications sensibles (banque, santé, admin)
- Ne PAS utiliser MFA pour applications grand public (friction UX)
- Permettre aux users de choisir leur méthode MFA
- Fournir des codes de backup
- Logger tous les événements MFA

## Monitoring

### Métriques à surveiller

1. **Taux de succès MFA**
   - Approuvé vs Rejeté vs Expiré
   - Temps moyen de validation

2. **Tentatives suspectes**
   - Multiples rejets
   - Connexions depuis nouvelles localisations

3. **Performance**
   - Latence des push notifications
   - Timeout rate

### Logs

```
[INFO] MFA request initiated for user {UserId} with request ID {RequestId} for client {ClientId}
[INFO] MFA request {RequestId} approved for user {UserId}
[WARN] MFA validation failed: Request {RequestId} has expired
[WARN] MFA validation failed: Invalid verification code for request {RequestId}
```

## Roadmap

- [x] Ajout RequireMfa au domaine Client
- [x] Migration base de données
- [x] Service MFA avec gestion en mémoire
- [x] API endpoints pour tests
- [ ] Intégration Microsoft Authenticator (Azure AD)
- [ ] Support TOTP (Google Authenticator)
- [ ] Persistance Redis pour requêtes MFA
- [ ] UI pour configuration MFA par client
- [ ] Codes de backup
- [ ] Méthodes MFA multiples par user
- [ ] Audit complet événements MFA
- [ ] Reporting et analytics

## FAQ

**Q: Le MFA est-il obligatoire pour tous les clients ?**  
R: Non, c'est opt-in. Seuls les clients avec `RequireMfa = true` le nécessitent.

**Q: Peut-on activer le MFA pour un client existant ?**  
R: Oui, via `PUT /api/clients/{id}` avec `{ "requireMfa": true }`.

**Q: Que se passe-t-il si l'utilisateur rejette la demande MFA ?**  
R: La connexion est refusée, l'utilisateur doit réessayer.

**Q: Le MFA est-il basé sur l'utilisateur ou le client ?**  
R: Sur le CLIENT. Un même utilisateur peut se connecter sans MFA sur app A et avec MFA sur app B.

**Q: Combien de temps une demande MFA est-elle valide ?**  
R: 5 minutes. Après cela, l'utilisateur doit se reconnecter.

**Q: Les demandes MFA survivent-elles au redémarrage de l'application ?**  
R: Non (stockage en mémoire). TODO: Utiliser Redis pour la production.

**Q: Peut-on forcer le MFA pour certains rôles uniquement ?**  
R: Pas encore implémenté. Voir `ROLES_PERMISSIONS_MFA.md` pour le MFA par rôle.

---

**Dernière mise à jour:** 24 novembre 2025  
**Version:** 1.0 (MFA par Client - MVP)
