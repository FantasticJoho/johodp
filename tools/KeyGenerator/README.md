# JWK Signing Key Generator

Tool pour générer des clés de signature JWK (JSON Web Key) pour IdentityServer.

## Usage

```bash
# Générer une clé RSA 2048 bits (par défaut)
dotnet run --project tools/KeyGenerator

# Générer une clé RSA 4096 bits
dotnet run --project tools/KeyGenerator -- --keysize 4096

# Spécifier le fichier de sortie
dotnet run --project tools/KeyGenerator -- --output my-signing-key.jwk
```

## Stocker dans HashiCorp Vault

```bash
# 1. Générer la clé
dotnet run --project tools/KeyGenerator

# 2. Stocker dans Vault
vault kv put secret/johodp/identityserver/current @signing-key.jwk

# 3. Vérifier
vault kv get -format=json secret/johodp/identityserver/current

# 4. Supprimer le fichier local (sécurité)
rm signing-key.jwk
```

## Utilisation avec Kubernetes

### Secret Kubernetes (simple)

```bash
# Créer un secret depuis le fichier
kubectl create secret generic identityserver-signing-key \
  --from-file=signing-key.jwk=./signing-key.jwk \
  --namespace=production

# Vérifier
kubectl get secret identityserver-signing-key -n production
```

### HashiCorp Vault (recommandé)

Voir `IDENTITY_SERVER_KEYS.md` pour l'intégration complète avec Vault.

## Format de sortie

Le générateur produit un fichier JWK conforme à la RFC 7517 :

```json
{
  "kty": "RSA",
  "kid": "e3b0c442-98fc-1c14-9afb-4c8996fb9248",
  "use": "sig",
  "alg": "RS256",
  "n": "xGOr-H3P_tTLKnv...",
  "e": "AQAB",
  "d": "Kn3pHGsY7q...",
  "p": "7Hv2Bg...",
  "q": "0xPQ3...",
  "dp": "...",
  "dq": "...",
  "qi": "..."
}
```

⚠️ **Ce fichier contient des données PRIVÉES (d, p, q, etc.). Ne jamais le committer dans Git !**

## Rotation des clés

Voir `IDENTITY_SERVER_KEYS.md` pour le processus complet de rotation avec grace period.

## Sécurité

- ✅ Génère des clés RSA 2048+ bits (standard industry)
- ✅ Format JSON compatible Vault/Kubernetes
- ✅ Key ID unique (kid) pour faciliter la rotation
- ✅ Compatible avec IdentityServer via `SigningKeyHelper`

