# IdentityServer Signing Key Configuration

## Development

En développement, IdentityServer utilise automatiquement `AddDeveloperSigningCredential()` qui génère une clé temporaire. Cette clé est régénérée à chaque redémarrage de l'application.

**Configuration requise :**
- Détection automatique via `IWebHostEnvironment.IsDevelopment()`
- Aucune clé de signature nécessaire

## Production / Staging

En production, vous devez fournir une clé de signature persistante pour assurer que les tokens JWT restent valides même après un redémarrage.

### Choix de la méthode de signature

| Méthode | Avantages | Inconvénients | Usage recommandé |
|---------|-----------|---------------|------------------|
| **Certificat X.509** | ✅ Standard PKI<br>✅ Révocation OCSP/CRL<br>✅ Expiration automatique | ❌ Gestion complexe<br>❌ Renouvellement requis | Grandes entreprises avec PKI existante |
| **JSON Web Key (JWK)** | ✅ Format JSON simple<br>✅ Rotation facile<br>✅ Kubernetes-friendly<br>✅ Vault natif | ❌ Pas de PKI<br>❌ Expiration manuelle | **Recommandé pour startups/scale-ups** |
| **Azure Key Vault HSM** | ✅ Clé jamais exposée<br>✅ Rotation automatique | ❌ Coût Azure<br>❌ Dépendance cloud | Applications critiques sur Azure |

**🔍 Sécurité identique :** JWK avec RSA 2048+ est tout aussi sécurisé qu'un certificat X.509, tant que la clé est stockée chiffrée dans Vault et rotée régulièrement.

### Option A: Certificat X.509 (Méthode actuelle)

#### Générer une clé de signature

```bash
# Option 1: Utiliser dotnet dev-certs (simple, pour dev/staging)
dotnet dev-certs https -ep ./keys/signing-key.pfx -p YourSecurePassword

# Option 2: Générer un certificat auto-signé (plus de contrôle)
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem -days 365 -nodes
openssl pkcs12 -export -out signing-key.pfx -inkey key.pem -in cert.pem -passout pass:YourSecurePassword

# Option 3: Production - Utiliser un certificat d'une CA reconnue
# Acheter un certificat SSL/TLS standard et l'utiliser pour signer les tokens
```

#### Configuration

**appsettings.Production.json :**
```json
{
  "IdentityServer": {
    "SigningMethod": "Certificate",
    "SigningKeyPath": "/app/keys/signing-key.pfx",
    "SigningKeyPassword": "REPLACE_WITH_SECRET"
  }
}
```

**Code (ServiceCollectionExtensions.cs) :**
```csharp
var signingMethod = configuration["IdentityServer:SigningMethod"] ?? "Certificate";

if (signingMethod == "Certificate")
{
    var signingKeyPath = configuration["IdentityServer:SigningKeyPath"];
    
    if (string.IsNullOrEmpty(signingKeyPath) || !File.Exists(signingKeyPath))
    {
        throw new InvalidOperationException(
            "IdentityServer:SigningKeyPath must be configured in production.");
    }
    
    var keyPassword = configuration["IdentityServer:SigningKeyPassword"];
    idServerBuilder.AddSigningCredential(
        new System.Security.Cryptography.X509Certificates.X509Certificate2(
            signingKeyPath, 
            keyPassword));
}
```

### Option B: JSON Web Key (JWK) - Recommandée pour Kubernetes

#### Générer une clé JWK

**Créer un projet générateur :**
```bash
mkdir tools/KeyGenerator
cd tools/KeyGenerator
dotnet new console
dotnet add package Microsoft.IdentityModel.Tokens
```

**Program.cs du générateur :**
```csharp
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

var rsa = RSA.Create(2048); // Ou 4096 pour plus de sécurité
var key = new RsaSecurityKey(rsa)
{
    KeyId = Guid.NewGuid().ToString()
};

var parameters = rsa.ExportParameters(includePrivateParameters: true);
var jwk = new
{
    kty = "RSA",
    kid = key.KeyId,
    use = "sig",
    alg = "RS256",
    n = Base64UrlEncoder.Encode(parameters.Modulus),
    e = Base64UrlEncoder.Encode(parameters.Exponent),
    d = Base64UrlEncoder.Encode(parameters.D),
    p = Base64UrlEncoder.Encode(parameters.P),
    q = Base64UrlEncoder.Encode(parameters.Q),
    dp = Base64UrlEncoder.Encode(parameters.DP),
    dq = Base64UrlEncoder.Encode(parameters.DQ),
    qi = Base64UrlEncoder.Encode(parameters.InverseQ)
};

var jwkJson = JsonSerializer.Serialize(jwk, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText("signing-key.jwk", jwkJson);
Console.WriteLine($"✅ JWK generated: signing-key.jwk (kid: {key.KeyId})");
```

**Exécuter :**
```bash
dotnet run --project tools/KeyGenerator
# Output: signing-key.jwk
```

#### Configuration

**appsettings.Production.json :**
```json
{
  "IdentityServer": {
    "SigningMethod": "JWK",
    "SigningKeyPath": "/app/keys/signing-key.jwk"
  }
}
```

**Ajouter helper dans Infrastructure (SigningKeyHelper.cs) :**
```csharp
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

namespace Johodp.Infrastructure.IdentityServer;

public static class SigningKeyHelper
{
    public static RsaSecurityKey LoadJwkFromFile(string path)
    {
        var jwkJson = File.ReadAllText(path);
        var jwk = JsonSerializer.Deserialize<JsonElement>(jwkJson);
        
        var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("n").GetString()!),
            Exponent = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("e").GetString()!),
            D = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("d").GetString()!),
            P = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("p").GetString()!),
            Q = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("q").GetString()!),
            DP = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("dp").GetString()!),
            DQ = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("dq").GetString()!),
            InverseQ = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("qi").GetString()!)
        });
        
        return new RsaSecurityKey(rsa)
        {
            KeyId = jwk.GetProperty("kid").GetString()
        };
    }
    
    public static RsaSecurityKey LoadJwkFromVault(string jwkJson)
    {
        var jwk = JsonSerializer.Deserialize<JsonElement>(jwkJson);
        
        var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("n").GetString()!),
            Exponent = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("e").GetString()!),
            D = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("d").GetString()!),
            P = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("p").GetString()!),
            Q = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("q").GetString()!),
            DP = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("dp").GetString()!),
            DQ = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("dq").GetString()!),
            InverseQ = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("qi").GetString()!)
        });
        
        return new RsaSecurityKey(rsa)
        {
            KeyId = jwk.GetProperty("kid").GetString()
        };
    }
}
```

**Code (ServiceCollectionExtensions.cs) :**
```csharp
var signingMethod = configuration["IdentityServer:SigningMethod"] ?? "Certificate";

if (signingMethod == "JWK")
{
    var signingKeyPath = configuration["IdentityServer:SigningKeyPath"];
    
    if (string.IsNullOrEmpty(signingKeyPath) || !File.Exists(signingKeyPath))
    {
        throw new InvalidOperationException(
            "IdentityServer:SigningKeyPath must be configured in production.");
    }
    
    var jwkKey = SigningKeyHelper.LoadJwkFromFile(signingKeyPath);
    idServerBuilder.AddSigningCredential(jwkKey, SecurityAlgorithms.RsaSha256);
    
    // Support de la rotation : charger aussi l'ancienne clé si elle existe
    var previousKeyPath = configuration["IdentityServer:PreviousSigningKeyPath"];
    if (!string.IsNullOrEmpty(previousKeyPath) && File.Exists(previousKeyPath))
    {
        var previousKey = SigningKeyHelper.LoadJwkFromFile(previousKeyPath);
        idServerBuilder.AddValidationKey(previousKey);
    }
}
```

### Sécurité des secrets

**NE JAMAIS** committer les secrets dans Git. Utilisez une des méthodes suivantes :

#### Option 1: Variables d'environnement (Docker, Kubernetes)
```bash
export IdentityServer__SigningKeyPassword="YourSecurePassword"
```

#### Option 2: HashiCorp Vault (Recommandé pour JWK)
**Package:** `dotnet add package VaultSharp`

**Stocker la JWK dans Vault :**
```bash
# Générer la clé
dotnet run --project tools/KeyGenerator

# Stocker dans Vault (format JSON brut)
vault kv put secret/johodp/identityserver/current @signing-key.jwk

# Vérifier
vault kv get -format=json secret/johodp/identityserver/current
```

**Code (Program.cs) :**
```csharp
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.AuthMethods;

// Configuration Vault
var vaultUri = builder.Configuration["Vault:Uri"];
var vaultToken = builder.Configuration["Vault:Token"];

IAuthMethodInfo authMethod = new TokenAuthMethodInfo(vaultToken);
var vaultClientSettings = new VaultClientSettings(vaultUri, authMethod);
var vaultClient = new VaultClient(vaultClientSettings);

// Charger la clé ACTUELLE
var currentSecret = await vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
    path: "johodp/identityserver/current",
    mountPoint: "secret");

var currentJwkJson = currentSecret.Data.Data["data"].ToString();
builder.Configuration["IdentityServer:CurrentKeyJson"] = currentJwkJson;

// Charger la clé PRÉCÉDENTE (pour rotation)
try
{
    var previousSecret = await vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
        path: "johodp/identityserver/previous",
        mountPoint: "secret");
    
    var previousJwkJson = previousSecret.Data.Data["data"].ToString();
    builder.Configuration["IdentityServer:PreviousKeyJson"] = previousJwkJson;
}
catch
{
    // Pas de clé précédente (rotation pas encore effectuée)
}
```

**Code (ServiceCollectionExtensions.cs) :**
```csharp
if (signingMethod == "JWK")
{
    // Charger depuis Vault
    var currentKeyJson = configuration["IdentityServer:CurrentKeyJson"];
    if (!string.IsNullOrEmpty(currentKeyJson))
    {
        var currentKey = SigningKeyHelper.LoadJwkFromVault(currentKeyJson);
        idServerBuilder.AddSigningCredential(currentKey, SecurityAlgorithms.RsaSha256);
        
        // Support rotation
        var previousKeyJson = configuration["IdentityServer:PreviousKeyJson"];
        if (!string.IsNullOrEmpty(previousKeyJson))
        {
            var previousKey = SigningKeyHelper.LoadJwkFromVault(previousKeyJson);
            idServerBuilder.AddValidationKey(previousKey);
        }
    }
    else
    {
        // Fallback : charger depuis fichier
        var signingKeyPath = configuration["IdentityServer:SigningKeyPath"];
        var jwkKey = SigningKeyHelper.LoadJwkFromFile(signingKeyPath);
        idServerBuilder.AddSigningCredential(jwkKey, SecurityAlgorithms.RsaSha256);
    }
}
```

**appsettings.Production.json :**
```json
{
  "IdentityServer": {
    "SigningMethod": "JWK"
  },
  "Vault": {
    "Uri": "https://vault.yourcompany.com:8200",
    "Token": "VAULT_TOKEN_FROM_ENV"
  }
}
```

**Architecture multi-pods (Kubernetes) :**
```
┌─────────────────────────────────────────────────┐
│  HashiCorp Vault                                │
│  secret/johodp/identityserver/                  │
│    ├─ current  (key-2024-11.jwk) ← Signe        │
│    └─ previous (key-2024-08.jwk) ← Valide       │
└─────────────────────────────────────────────────┘
                     ▲
                     │ Read at startup
                     │
         ┌───────────┴────────────┬────────────┐
         │                        │            │
    ┌────▼────┐             ┌────▼────┐  ┌────▼────┐
    │ Pod 1   │             │ Pod 2   │  │ Pod 3   │
    │ KeyB +  │             │ KeyB +  │  │ KeyB +  │
    │ KeyA    │             │ KeyA    │  │ KeyA    │
    └─────────┘             └─────────┘  └─────────┘
    
✅ Tous les pods ont les MÊMES clés (pas de génération par pod)
✅ Tokens signés par Pod 1 valides sur Pod 2 et Pod 3
✅ Rotation centralisée dans Vault (un seul endroit)
```

#### Option 3: Azure Key Vault (Azure)
**Package:** `dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets`

```csharp
// Program.cs
using Azure.Identity;

builder.Configuration.AddAzureKeyVault(
    new Uri("https://your-keyvault.vault.azure.net/"),
    new DefaultAzureCredential());
```

**Stocker le secret :**
```bash
az keyvault secret set --vault-name your-keyvault \
    --name IdentityServer--SigningKeyPassword \
    --value "YourSecurePassword"
```

#### Option 4: AWS Secrets Manager (AWS)
**Package:** `dotnet add package AWSSDK.SecretsManager`

```csharp
// Program.cs
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

var client = new AmazonSecretsManagerClient();
var request = new GetSecretValueRequest
{
    SecretId = "johodp/identityserver/signing-key-password"
};
var response = await client.GetSecretValueAsync(request);
builder.Configuration["IdentityServer:SigningKeyPassword"] = response.SecretString;
```

**Stocker le secret :**
```bash
aws secretsmanager create-secret \
    --name johodp/identityserver/signing-key-password \
    --secret-string "YourPassword"
```

#### Option 5: User Secrets (développement local uniquement)
```bash
cd src/Johodp.Api
dotnet user-secrets init
dotnet user-secrets set "IdentityServer:SigningKeyPassword" "YourSecurePassword"
```

### Structure des fichiers

```
src/Johodp.Api/
  keys/
    signing-key.pfx          # Clé de production (NE PAS COMMITTER)
    .gitignore               # Ignore signing-key.pfx
  appsettings.json           # Configuration de base (IsDevelopment: false)
  appsettings.Development.json   # IsDevelopment: true
  appsettings.Production.json    # Chemin vers la clé
```

### Rotation des clés

Pour une sécurité maximale, les clés de signature doivent être changées périodiquement.

#### Pourquoi faire une rotation ?

- 🔐 **Principe de moindre privilège** : Limiter la fenêtre d'exposition en cas de compromission
- 🛡️ **Conformité** : PCI-DSS, SOC 2, ISO 27001 exigent une rotation régulière
- 🕒 **Réduction du risque** : Si une clé est volée, elle devient inutile après rotation

**Fréquence recommandée :**
- **Production critique** : Tous les 90 jours
- **Production standard** : Tous les 6 mois
- **Développement** : Pas nécessaire (clé temporaire à chaque restart)

#### Impacts lors d'une rotation

**⚠️ Problème sans stratégie :**
```
10:00 - API utilise KeyA pour signer les tokens
10:05 - User obtient un access_token signé avec KeyA (expire dans 1h)
10:10 - ROTATION : API passe à KeyB (KeyA supprimée)
10:15 - User fait un appel API avec son token (signé avec KeyA)
        ❌ ERREUR : API ne peut plus vérifier le token
```

**✅ Solution : Grace Period (période de transition)**

Duende IdentityServer supporte plusieurs clés simultanément :
- **Clé ACTUELLE** : Signe les NOUVEAUX tokens via `AddSigningCredential()`
- **Clé PRÉCÉDENTE** : Valide les ANCIENS tokens via `AddValidationKey()`

#### Processus de rotation sans interruption

##### Avec Certificat X.509

```bash
# JOUR 0 : Configuration initiale
# - cert-a.pfx : ACTIVE (signe + valide)

# JOUR 90 : Rotation
# 1. Générer nouveau certificat
dotnet dev-certs https -ep ./keys/cert-b.pfx -p NewPassword

# 2. Mettre à jour appsettings.Production.json
{
  "IdentityServer": {
    "SigningKeyPath": "/app/keys/cert-b.pfx",
    "SigningKeyPassword": "NewPassword",
    "PreviousSigningKeyPath": "/app/keys/cert-a.pfx",
    "PreviousSigningKeyPassword": "OldPassword"
  }
}

# 3. Redémarrer l'application (rolling restart si Kubernetes)
kubectl rollout restart deployment/johodp-api

# État après rotation :
# - cert-b.pfx : Signe les NOUVEAUX tokens
# - cert-a.pfx : Valide encore les ANCIENS tokens (jusqu'à expiration)

# JOUR 90 + Token Lifetime (ex: +1 jour) :
# 4. Retirer l'ancien certificat
{
  "IdentityServer": {
    "SigningKeyPath": "/app/keys/cert-b.pfx",
    "SigningKeyPassword": "NewPassword"
    // PreviousSigningKeyPath retiré
  }
}
```

##### Avec JWK (recommandé)

```bash
# JOUR 0 : Configuration initiale
# - key-2024-08.jwk : ACTIVE (signe + valide)

# JOUR 90 : Rotation
# 1. Générer nouvelle clé
dotnet run --project tools/KeyGenerator
mv signing-key.jwk key-2024-11.jwk

# 2. Stocker dans Vault
vault kv put secret/johodp/identityserver/current @key-2024-11.jwk
vault kv put secret/johodp/identityserver/previous @key-2024-08.jwk

# 3. Redémarrer les pods (rolling restart - zéro downtime)
kubectl rollout restart deployment/johodp-api

# État après rotation :
# - key-2024-11.jwk : Signe les NOUVEAUX tokens
# - key-2024-08.jwk : Valide encore les ANCIENS tokens

# JOUR 90 + Token Lifetime (ex: +1 jour) :
# 4. Supprimer l'ancienne clé
vault kv delete secret/johodp/identityserver/previous
```

#### Script de rotation automatique (CronJob Kubernetes)

**rotate-signing-key.sh :**
```bash
#!/bin/bash
set -e

echo "🔄 Starting IdentityServer signing key rotation..."

# 1. Backup de la clé actuelle
echo "📦 Backing up current key..."
vault kv get -format=json secret/johodp/identityserver/current > /tmp/current-backup.json

# 2. Déplacer current → previous
echo "📝 Moving current key to previous..."
vault kv put secret/johodp/identityserver/previous @/tmp/current-backup.json

# 3. Générer nouvelle clé
echo "🔑 Generating new signing key..."
dotnet run --project /tools/KeyGenerator -- --output /tmp/new-key.jwk

# 4. Uploader nouvelle clé
echo "☁️ Uploading new key to Vault..."
vault kv put secret/johodp/identityserver/current @/tmp/new-key.jwk

# 5. Rolling restart (zéro downtime)
echo "🔄 Restarting pods..."
kubectl rollout restart deployment/johodp-api -n production
kubectl rollout status deployment/johodp-api -n production --timeout=5m

# 6. Attendre expiration des tokens (24h par défaut)
echo "⏳ Waiting 24h for old tokens to expire..."
sleep 86400

# 7. Supprimer l'ancienne clé
echo "🗑️ Removing old key..."
vault kv delete secret/johodp/identityserver/previous

# 8. Cleanup
rm -f /tmp/current-backup.json /tmp/new-key.jwk

echo "✅ Key rotation completed successfully!"
```

**CronJob Kubernetes (tous les 90 jours) :**
```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: rotate-identityserver-key
  namespace: production
spec:
  schedule: "0 2 1 */3 *"  # 02:00 le 1er jour tous les 3 mois
  jobTemplate:
    spec:
      template:
        spec:
          serviceAccountName: vault-auth
          containers:
          - name: key-rotator
            image: johodp-tools:latest
            command: ["/scripts/rotate-signing-key.sh"]
            env:
            - name: VAULT_ADDR
              value: "https://vault.yourcompany.com:8200"
            - name: VAULT_TOKEN
              valueFrom:
                secretKeyRef:
                  name: vault-token
                  key: token
          restartPolicy: OnFailure
```

## Comparaison sécurité : Certificat vs JWK

| Critère | Certificat X.509 | JWK brute | Verdict |
|---------|------------------|-----------|---------|
| **Algorithme cryptographique** | RSA 2048+ ou ECC | RSA 2048+ ou ECC | ⚖️ **IDENTIQUE** |
| **Longueur de clé** | 2048-4096 bits | 2048-4096 bits | ⚖️ **IDENTIQUE** |
| **Format de stockage** | Binaire (.pfx) + Password | JSON chiffré dans Vault | ⚖️ **IDENTIQUE** si chiffré |
| **Chaîne de confiance PKI** | ✅ Certificat CA | ❌ Pas de CA | 🔵 Certificat + |
| **Révocation (OCSP/CRL)** | ✅ Standard PKI | ❌ Manuel | 🔵 Certificat + |
| **Expiration automatique** | ✅ NotAfter dans cert | ❌ Logique applicative | 🔵 Certificat + |
| **Facilité de rotation** | ❌ Complexe (renouvellement) | ✅ Simple (JSON) | 🟢 JWK + |
| **Stockage dans Vault** | ⚠️ Possible mais lourd | ✅ Natif (JSON) | 🟢 JWK + |
| **Compatibilité JWKS** | ⚠️ Conversion requise | ✅ Format natif RFC 7517 | 🟢 JWK + |
| **Multi-pods Kubernetes** | ⚠️ Montage volume | ✅ Read from Vault | 🟢 JWK + |

### Verdict final

**JWK est tout aussi sécurisé SI :**
- ✅ Clé générée avec `RSA.Create(2048)` minimum (ou 4096)
- ✅ Clé stockée chiffrée dans Vault (jamais en clair dans Git)
- ✅ Rotation régulière implémentée (90 jours)
- ✅ Permissions strictes sur Vault (least privilege)
- ✅ Audit des accès activé (Vault logs)

**Certificat X.509 apporte un PLUS si :**
- Vous avez déjà une PKI d'entreprise (Active Directory CS)
- Conformité réglementaire exige certificats CA (banques, santé)
- Besoin de révocation automatique OCSP/CRL

**Recommandation pour startups/scale-ups modernes :**
```
✅ JWK + HashiCorp Vault + Rotation automatisée (CronJob)
```

## Vérification

Pour vérifier que votre configuration fonctionne :

```bash
# Développement
curl http://localhost:5000/.well-known/openid-configuration/jwks
# Devrait retourner une clé temporaire

# Production
curl https://your-domain.com/.well-known/openid-configuration/jwks
# Devrait retourner la clé du certificat configuré
```

Les tokens JWT incluent le `kid` (Key ID) qui correspond à la clé utilisée pour les signer.
