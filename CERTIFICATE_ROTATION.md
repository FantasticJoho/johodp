# Guide de Rotation des Certificats Sans Interruption

## Principe de la Rotation Sans Coupure

La rotation sans coupure repose sur le **grace period** : avoir **deux certificats actifs simultanément** pendant une période de transition.

```
Timeline de rotation:

T0 (Avant rotation):
├─ cert-a.pfx : SIGNE + VALIDE les tokens
└─ Durée de vie token : 1h

T1 (Rotation - Jour 90):
├─ cert-b.pfx : SIGNE les nouveaux tokens ← AddSigningCredential()
└─ cert-a.pfx : VALIDE les anciens tokens  ← AddValidationKey()
                 (tokens créés avant T1)

T2 (T1 + 1h = fin du grace period):
└─ cert-b.pfx : SIGNE + VALIDE
    (tous les tokens signés avec cert-a ont expiré)
```

## Configuration

### appsettings.Production.json

```json
{
  "IdentityServer": {
    "SigningMethod": "Certificate",
    "SigningKeyPath": "/app/keys/signing-key-current.pfx",
    "SigningKeyPassword": "FROM_VAULT",
    "PreviousSigningKeyPath": "/app/keys/signing-key-previous.pfx",
    "PreviousSigningKeyPassword": "FROM_VAULT"
  }
}
```

**Pendant le grace period :**
- `SigningKeyPath` → Nouveau certificat (signe les nouveaux tokens)
- `PreviousSigningKeyPath` → Ancien certificat (valide les anciens tokens)

**Après le grace period :**
- `PreviousSigningKeyPath` → Retirer (ou laisser vide)

## Méthode 1: Rotation Manuelle (Pas-à-pas)

### Étape 1: Générer nouveau certificat

```bash
# Avec dotnet dev-certs
dotnet dev-certs https -ep /app/keys/signing-key-new.pfx -p NewPassword123

# Ou avec OpenSSL
openssl req -x509 -newkey rsa:4096 \
    -keyout /tmp/key.pem \
    -out /tmp/cert.pem \
    -days 365 \
    -nodes \
    -subj "/CN=Johodp IdentityServer/O=Johodp/C=FR"

openssl pkcs12 -export \
    -out /app/keys/signing-key-new.pfx \
    -inkey /tmp/key.pem \
    -in /tmp/cert.pem \
    -passout pass:NewPassword123

rm /tmp/key.pem /tmp/cert.pem
```

### Étape 2: Stocker dans Vault

```bash
# Stocker le nouveau certificat
vault kv put secret/johodp/identityserver/new \
    cert-path="/app/keys/signing-key-new.pfx" \
    cert-password="NewPassword123"

# Vérifier
vault kv get secret/johodp/identityserver/new
```

### Étape 3: Configurer grace period

**Mettre à jour appsettings.Production.json :**

```json
{
  "IdentityServer": {
    "SigningMethod": "Certificate",
    "SigningKeyPath": "/app/keys/signing-key-new.pfx",
    "SigningKeyPassword": "NewPassword123",
    "PreviousSigningKeyPath": "/app/keys/signing-key-old.pfx",
    "PreviousSigningKeyPassword": "OldPassword123"
  }
}
```

**Ou via Vault (recommandé) :**

```bash
# Charger depuis Vault dans Program.cs
var currentCert = vault.Get("secret/johodp/identityserver/new");
var previousCert = vault.Get("secret/johodp/identityserver/current");

builder.Configuration["IdentityServer:SigningKeyPath"] = currentCert.CertPath;
builder.Configuration["IdentityServer:SigningKeyPassword"] = currentCert.Password;
builder.Configuration["IdentityServer:PreviousSigningKeyPath"] = previousCert.CertPath;
builder.Configuration["IdentityServer:PreviousSigningKeyPassword"] = previousCert.Password;
```

### Étape 4: Redémarrer l'application (Rolling Restart)

**Kubernetes (zéro downtime) :**

```bash
kubectl rollout restart deployment/johodp-api -n production
kubectl rollout status deployment/johodp-api -n production --timeout=5m
```

**Docker Compose :**

```bash
docker-compose up -d --no-deps --build johodp-api
```

**Systemd :**

```bash
systemctl reload johodp-api
```

### Étape 5: Vérifier que les deux certificats sont actifs

```bash
# Vérifier les clés exposées dans JWKS
curl http://your-api.com/.well-known/openid-configuration/jwks

# Devrait retourner 2 clés avec des "kid" différents
{
  "keys": [
    {
      "kty": "RSA",
      "use": "sig",
      "kid": "new-key-id-2024-11-25",
      "n": "...",
      "e": "AQAB"
    },
    {
      "kty": "RSA",
      "use": "sig",
      "kid": "old-key-id-2024-08-25",
      "n": "...",
      "e": "AQAB"
    }
  ]
}
```

### Étape 6: Attendre le grace period

**Calculer la durée :**

```
Grace Period = Token Lifetime + Marge de sécurité
             = 1 heure + 1 heure
             = 2 heures minimum
```

**Attendre automatiquement :**

```bash
# Attendre 2 heures
sleep 7200

# Ou planifier via cron/scheduled task
```

### Étape 7: Retirer l'ancien certificat

**Mettre à jour appsettings.Production.json :**

```json
{
  "IdentityServer": {
    "SigningMethod": "Certificate",
    "SigningKeyPath": "/app/keys/signing-key-new.pfx",
    "SigningKeyPassword": "NewPassword123"
    // PreviousSigningKeyPath retiré
  }
}
```

**Ou via Vault :**

```bash
# Promouvoir "new" → "current"
vault kv put secret/johodp/identityserver/current @new-cert.json

# Supprimer "previous"
vault kv delete secret/johodp/identityserver/previous
```

### Étape 8: Redémarrer à nouveau

```bash
kubectl rollout restart deployment/johodp-api -n production
```

### Étape 9: Vérifier JWKS (1 seule clé)

```bash
curl http://your-api.com/.well-known/openid-configuration/jwks

# Devrait retourner 1 clé uniquement
{
  "keys": [
    {
      "kty": "RSA",
      "use": "sig",
      "kid": "new-key-id-2024-11-25",
      "n": "...",
      "e": "AQAB"
    }
  ]
}
```

## Méthode 2: Rotation Automatique (Scripts)

### Windows (PowerShell)

```powershell
# Exécution manuelle
.\tools\rotate-certificate.ps1

# Ou avec Task Scheduler (tous les 90 jours)
$action = New-ScheduledTaskAction -Execute "PowerShell.exe" `
    -Argument "-File C:\app\tools\rotate-certificate.ps1"

$trigger = New-ScheduledTaskTrigger -Daily -DaysInterval 90 -At 2am

Register-ScheduledTask -TaskName "IdentityServerCertRotation" `
    -Action $action `
    -Trigger $trigger `
    -User "SYSTEM"
```

### Linux (Bash + Cron)

```bash
# Exécution manuelle
./tools/rotate-certificate.sh

# Ou avec crontab (tous les 90 jours le 1er du trimestre à 2h du matin)
crontab -e

# Ajouter:
0 2 1 */3 * /app/tools/rotate-certificate.sh >> /var/log/cert-rotation.log 2>&1
```

### Kubernetes (CronJob)

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: rotate-identityserver-cert
  namespace: production
spec:
  schedule: "0 2 1 */3 *"  # 2h du matin, 1er jour, tous les 3 mois
  jobTemplate:
    spec:
      template:
        spec:
          serviceAccountName: vault-auth
          containers:
          - name: cert-rotator
            image: johodp-tools:latest
            command: ["/bin/bash", "/scripts/rotate-certificate.sh"]
            env:
            - name: VAULT_ADDR
              value: "https://vault.company.com:8200"
            - name: VAULT_TOKEN
              valueFrom:
                secretKeyRef:
                  name: vault-token
                  key: token
            - name: TOKEN_LIFETIME_HOURS
              value: "1"
            volumeMounts:
            - name: keys
              mountPath: /app/keys
          volumes:
          - name: keys
            persistentVolumeClaim:
              claimName: identityserver-keys
          restartPolicy: OnFailure
```

## Vérification Post-Rotation

### Test 1: Vérifier qu'aucun token n'est invalidé

```bash
# Obtenir un token AVANT rotation
TOKEN_OLD=$(curl -X POST http://your-api.com/connect/token \
    -d "grant_type=password&username=test&password=test" | jq -r .access_token)

# Effectuer la rotation
./tools/rotate-certificate.sh

# Tester que le token OLD fonctionne toujours
curl -H "Authorization: Bearer $TOKEN_OLD" http://your-api.com/api/users

# ✅ Devrait retourner 200 OK (token validé avec l'ancien certificat)
```

### Test 2: Vérifier que les nouveaux tokens sont signés avec le nouveau certificat

```bash
# Obtenir un token APRÈS rotation
TOKEN_NEW=$(curl -X POST http://your-api.com/connect/token \
    -d "grant_type=password&username=test&password=test" | jq -r .access_token)

# Décoder le header JWT
echo $TOKEN_NEW | cut -d. -f1 | base64 -d | jq

# Devrait afficher le nouveau "kid"
{
  "alg": "RS256",
  "kid": "new-key-id-2024-11-25",
  "typ": "JWT"
}
```

### Test 3: Monitoring des erreurs

```bash
# Surveiller les logs pour les erreurs de validation
kubectl logs -f deployment/johodp-api -n production | grep -i "signature"

# Aucune erreur ne devrait apparaître pendant le grace period
```

## Troubleshooting

### Problème: Tokens invalidés immédiatement après rotation

**Cause:** `PreviousSigningKeyPath` non configuré

**Solution:**
```json
{
  "IdentityServer": {
    "SigningKeyPath": "/new-cert.pfx",
    "SigningKeyPassword": "...",
    "PreviousSigningKeyPath": "/old-cert.pfx",  // ← Ajouter
    "PreviousSigningKeyPassword": "..."           // ← Ajouter
  }
}
```

### Problème: JWKS expose toujours 2 clés après le grace period

**Cause:** `PreviousSigningKeyPath` non retiré de la config

**Solution:**
```bash
# Mettre à jour config pour retirer PreviousSigningKeyPath
# Puis redémarrer
kubectl rollout restart deployment/johodp-api
```

### Problème: Erreur "Unable to obtain configuration from..."

**Cause:** Certificat corrompu ou mot de passe incorrect

**Solution:**
```bash
# Vérifier le certificat
openssl pkcs12 -info -in /app/keys/signing-key.pfx -passin pass:YourPassword

# Régénérer si corrompu
dotnet dev-certs https -ep /app/keys/signing-key-new.pfx -p NewPassword --trust
```

## Checklist de Rotation

- [ ] Générer nouveau certificat (RSA 4096 bits, valide 365 jours)
- [ ] Stocker dans Vault sécurisé
- [ ] Configurer `PreviousSigningKeyPath` (grace period)
- [ ] Rolling restart de l'application
- [ ] Vérifier JWKS expose 2 clés
- [ ] Tester qu'anciens tokens fonctionnent
- [ ] Tester que nouveaux tokens utilisent nouvelle clé
- [ ] Attendre grace period (Token Lifetime + marge)
- [ ] Retirer `PreviousSigningKeyPath`
- [ ] Rolling restart final
- [ ] Vérifier JWKS expose 1 clé
- [ ] Supprimer ancien certificat du filesystem
- [ ] Logger la date de rotation (next rotation = +90 jours)

## Fréquence Recommandée

- **Production critique (finance, santé)**: 90 jours
- **Production standard**: 180 jours (6 mois)
- **Staging**: 1 an
- **Développement**: Clé temporaire (pas de rotation)

## Références

- RFC 7517: JSON Web Key (JWK)
- RFC 7519: JSON Web Token (JWT)
- Duende IdentityServer Documentation: Key Management
- NIST SP 800-57: Key Management Recommendations
