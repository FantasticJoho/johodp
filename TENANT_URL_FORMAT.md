# Format URL Tenant - acr_values

## Vue d'ensemble

Le paramètre OIDC standard `acr_values` dans Johodp utilise l'**URL complète du tenant** (sans caractères spéciaux) pour identifier le tenant de manière unique et sécurisée.

## Format

### Syntaxe

```
acr_values=tenant:<URL_NETTOYEE>
```

Où `<URL_NETTOYEE>` est dérivé de l'URL complète du tenant en appliquant les transformations suivantes :

### Règles de transformation

| Étape | Transformation | Exemple |
|-------|---------------|---------|
| 1. URL originale | URL complète du tenant | `https://acme-corp.example.com` |
| 2. Supprimer protocole | Enlever `http://` ou `https://` | `acme-corp.example.com` |
| 3. Normaliser caractères | Translittérer accents, supprimer non-ASCII | `acme-corp.example.com` |
| 4. Remplacer séparateurs | Remplacer `/`, `.`, `:` par `-` | `acme-corp-example-com` |
| 5. Lowercase | Convertir en minuscules | `acme-corp-example-com` |
| 6. Résultat final | Identifiant tenant nettoyé (ASCII uniquement) | `acme-corp-example-com` |

### Exemples de transformation

| URL Tenant | acr_values |
|-----------|-----------|
| `https://acme-corp.example.com` | `acr_values=tenant:acme-corp-example-com` |
| `https://client.subdomain.app.io` | `acr_values=tenant:client-subdomain-app-io` |
| `https://app.company.fr` | `acr_values=tenant:app-company-fr` |
| `http://localhost:8080` | `acr_values=tenant:localhost-8080` |
| `https://api-v2.service.cloud:443` | `acr_values=tenant:api-v2-service-cloud-443` |
| `https://café-société.fr` | `acr_values=tenant:cafe-societe-fr` |
| `https://tëst-ñoño.com` | `acr_values=tenant:test-nono-com` |
| `https://société.例え.com` | `acr_values=tenant:societe-com` |

## Utilisation dans les endpoints

### 1. Authorization Code Flow (OIDC)

```http
GET /connect/authorize?
  response_type=code&
  client_id=johodp-spa&
  redirect_uri=http://localhost:4200/callback&
  scope=openid profile email&
  code_challenge=xyz&
  code_challenge_method=S256&
  acr_values=tenant:acme-corp-example-com

# Où acme-corp-example-com provient de https://acme-corp.example.com
```

### 2. Login API

```http
POST /api/auth/login?acr_values=tenant:acme-corp-example-com
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "P@ssw0rd!"
}
```

### 3. Onboarding

```http
GET /account/onboarding?acr_values=tenant:acme-corp-example-com
```

### 4. Activation

```http
POST /api/account/activate
Content-Type: application/json

{
  "token": "ABC123",
  "userId": "guid",
  "tenantId": "acme-corp-example-com",
  "newPassword": "P@ssw0rd!",
  "confirmPassword": "P@ssw0rd!"
}
```

## Justification du format URL

### Pourquoi utiliser l'URL complète ?

1. **Unicité garantie** : Les URLs sont uniques par nature (DNS)
2. **Traçabilité** : L'URL identifie clairement l'origine de la requête
3. **Sécurité** : Correspond au domaine réel du tenant
4. **Flexibilité** : Support de subdomains, ports, chemins
5. **Standard web** : Aligné avec les conventions DNS/HTTP

### Pourquoi nettoyer les caractères spéciaux ?

1. **Compatibilité** : Les query params OIDC ne supportent pas tous les caractères spéciaux
2. **Simplicité** : Facilite le parsing et la validation
3. **Lisibilité** : Format plus lisible dans les logs et URLs
4. **Sécurité** : Évite les attaques d'injection via caractères spéciaux

### Gestion des caractères non-ASCII

Seuls les **caractères ASCII** (a-z, 0-9, -) sont autorisés dans `acr_values`.

#### Règles de normalisation

| Caractère | Transformation | Exemple |
|-----------|---------------|----------|
| Accents (é, è, ê, à, ù, etc.) | Translittération | `café` → `cafe` |
| Tilde (ñ, õ) | Translittération | `niño` → `nino` |
| Umlaut (ü, ö, ä) | Translittération | `zürich` → `zurich` |
| Cédille (ç) | Translittération | `français` → `francais` |
| Caractères non-latins (漢字, кириллица, etc.) | Suppression | `test.例え.com` → `test-com` |
| Symboles spéciaux (€, £, ©, etc.) | Suppression | `app©.com` → `app-com` |

#### Table de translittération

```
é, è, ê, ë, ē → e
à, â, ä, ā → a
î, ï, ī → i
ô, ö, ō → o
ù, û, ü, ū → u
ç → c
ñ → n
œ → oe
æ → ae
ß → ss
```

## Stockage en base de données

### Table Tenant

```sql
CREATE TABLE "Tenants" (
  "Id" UUID PRIMARY KEY,
  "Name" VARCHAR(255) NOT NULL UNIQUE,  -- URL nettoyée (ex: acme-corp-example-com)
  "TenantUrl" VARCHAR(500) NOT NULL,    -- URL originale (ex: https://acme-corp.example.com)
  "DisplayName" VARCHAR(255) NOT NULL,
  ...
);
```

### Exemple de données

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "acme-corp-example-com",
  "tenantUrl": "https://acme-corp.example.com",
  "displayName": "ACME Corporation",
  "allowedReturnUrls": [
    "https://acme-corp.example.com/callback",
    "http://localhost:4200/callback"
  ],
  "allowedCorsOrigins": [
    "https://acme-corp.example.com",
    "http://localhost:4200"
  ]
}
```

## Claims JWT

Les tokens JWT incluent à la fois l'identifiant nettoyé et l'URL originale :

```json
{
  "sub": "user-guid",
  "email": "user@example.com",
  "tenant_id": "acme-corp-example-com",
  "tenant_url": "https://acme-corp.example.com",
  "role": ["admin"],
  "permission": ["users:read", "users:write"],
  "scope": ["openid", "profile", "email", "johodp.api"]
}
```

### Utilisation des claims

- **`tenant_id`** : Pour la validation et l'isolation (comparaison rapide)
- **`tenant_url`** : Pour l'affichage, les redirections, les webhooks

## Validation et sécurité

### Validation côté serveur

```csharp
using System.Text;
using System.Globalization;

public class TenantValidator
{
    public static string CleanTenantUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Tenant URL cannot be empty");
        
        // Supprimer protocole
        var cleaned = url.Replace("https://", "").Replace("http://", "");
        
        // Normaliser caractères non-ASCII (translittération)
        cleaned = RemoveDiacritics(cleaned);
        
        // Supprimer tous les caractères non-ASCII restants
        cleaned = Regex.Replace(cleaned, @"[^\x00-\x7F]", "");
        
        // Remplacer séparateurs
        cleaned = cleaned.Replace("/", "-")
                         .Replace(".", "-")
                         .Replace(":", "-")
                         .Replace("_", "-");
        
        // Supprimer caractères non autorisés (garder a-z, 0-9, -)
        cleaned = Regex.Replace(cleaned, @"[^a-zA-Z0-9\-]", "");
        
        // Lowercase
        cleaned = cleaned.ToLowerInvariant();
        
        // Supprimer tirets multiples consécutifs
        cleaned = Regex.Replace(cleaned, @"-{2,}", "-");
        
        // Supprimer tirets au début/fin
        cleaned = cleaned.Trim('-');
        
        // Validation finale (ASCII uniquement: a-z, 0-9, -)
        if (!Regex.IsMatch(cleaned, @"^[a-z0-9\-]+$"))
            throw new ArgumentException("Invalid tenant URL format (must be ASCII only)");
        
        if (cleaned.Length < 3 || cleaned.Length > 255)
            throw new ArgumentException("Tenant ID must be between 3 and 255 characters");
        
        return cleaned;
    }
    
    private static string RemoveDiacritics(string text)
    {
        // Normaliser en forme décomposée (NFD)
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();
        
        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            // Garder tout sauf les marques non espacées (accents)
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }
        
        // Recomposer en forme NFC
        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
    
    public static bool ValidateTenantUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Scheme == "http" || uri.Scheme == "https";
        }
        catch
        {
            return false;
        }
    }
}
```

### Vérification d'accès tenant

```csharp
public bool UserHasAccessToTenant(User user, string requestedTenantId)
{
    // Wildcard = accès à tous les tenants
    if (user.TenantIds.Contains("*"))
        return true;
    
    // Vérification explicite
    return user.TenantIds.Contains(requestedTenantId);
}
```

## Webhooks et notifications

Lors de l'envoi de webhooks vers l'application tierce, inclure les deux formats :

```http
POST https://api.acme.com/webhooks/johodp/user-registered
Authorization: Bearer {external_idp_token}
Content-Type: application/json

{
  "eventType": "user.registered",
  "eventId": "uuid",
  "timestamp": "2025-11-25T10:30:00Z",
  "data": {
    "userId": "guid",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "tenantId": "acme-corp-example-com",
    "tenantUrl": "https://acme-corp.example.com"
  }
}
```

L'application tierce peut utiliser :
- `tenantId` pour recherche rapide en base
- `tenantUrl` pour validation d'origine et redirections

## Migration depuis format simple

Si vous aviez auparavant un format simple (ex: `acme-corp`), voici comment migrer :

### Étape 1 : Ajouter colonne TenantUrl

```sql
ALTER TABLE "Tenants" ADD COLUMN "TenantUrl" VARCHAR(500);
```

### Étape 2 : Migrer les données

```sql
-- Pour les tenants existants, reconstruire l'URL
UPDATE "Tenants"
SET "TenantUrl" = 'https://' || REPLACE("Name", '-', '.') || '.example.com'
WHERE "TenantUrl" IS NULL;
```

### Étape 3 : Mettre à jour Name avec URL nettoyée

```sql
-- Si nécessaire, régénérer Name depuis TenantUrl
UPDATE "Tenants"
SET "Name" = LOWER(
  REPLACE(
    REPLACE(
      REPLACE(REPLACE("TenantUrl", 'https://', ''), 'http://', ''),
      '.', '-'
    ),
    ':', '-'
  )
);
```

### Étape 4 : Rendre TenantUrl obligatoire

```sql
ALTER TABLE "Tenants" ALTER COLUMN "TenantUrl" SET NOT NULL;
```

## Logging et debugging

Les logs Serilog incluent le tenant nettoyé via l'enricher `TenantClientEnricher` :

```
[15:42:31 INF] acme-corp-example-com johodp-spa User user@example.com authenticated successfully
[15:42:32 INF] client-subdomain-app-io johodp-api Provisioning MFA for user 1bb71afc-e622-42f4-b3fd-df4956ebb3eb
```

### Recherche dans les logs

```bash
# Rechercher tous les événements d'un tenant
grep "acme-corp-example-com" logs/johodp-*.log

# Rechercher échecs d'authentification
grep "acme-corp-example-com.*authentication failed" logs/johodp-*.log
```

## Bonnes pratiques

### ✅ À faire

- Utiliser des URLs HTTPS en production (`https://tenant.example.com`)
- Valider l'URL avant de créer le tenant
- Stocker à la fois `Name` (nettoyé ASCII) et `TenantUrl` (original UTF-8)
- Inclure les deux dans les claims JWT
- Logger avec `tenant_id` nettoyé pour lisibilité
- Privilégier des domaines ASCII natifs (éviter les IDN si possible)
- Tester la translittération avant de créer le tenant

### ❌ À éviter

- Ne pas utiliser d'URL avec chemins (`https://example.com/tenant/acme`)
- Ne pas inclure de query params (`https://example.com?tenant=acme`)
- Ne pas utiliser d'URL avec fragments (`https://example.com#acme`)
- Ne pas exposer `TenantUrl` dans les URLs publiques (utiliser `Name`)
- Ne pas supposer que la translittération est réversible (é → e est irréversible)
- Ne pas utiliser des domaines avec caractères non-ASCII si l'unicité est critique

## Domaines internationalisés (IDN)

### Conversion Punycode

Les domaines internationalisés (IDN) utilisent le format Punycode pour l'encodage ASCII :

```
Original:  https://café.fr
Punycode:  https://xn--caf-dma.fr
Tenant ID: xn--caf-dma-fr
```

### Recommandation

**Pour les URLs avec caractères non-ASCII, utiliser le domaine Punycode d'origine :**

```csharp
using System;

public static string ConvertToPunycode(string url)
{
    var uri = new Uri(url);
    var idn = new IdnMapping();
    var punycodeHost = idn.GetAscii(uri.Host);
    
    return $"{uri.Scheme}://{punycodeHost}{uri.PathAndQuery}";
}

// Exemple
var original = "https://café-société.fr";
var punycode = ConvertToPunycode(original); 
// Résultat: "https://xn--caf-socit-i1a6e.fr"

var tenantId = CleanTenantUrl(punycode);
// Résultat: "xn--caf-socit-i1a6e-fr"
```

### Comparaison des approches

| Approche | Avantages | Inconvénients |
|----------|-----------|---------------|
| **Translittération** (é→e) | Simple, lisible | Perte d'information, collisions possibles |
| **Punycode** (xn--...) | Réversible, unique | Moins lisible, plus long |
| **ASCII natif** | Idéal | Nécessite des domaines ASCII |

### Recommandation finale

1. **Préférer des domaines ASCII natifs** (`acme-corp.com`)
2. **Si IDN obligatoire**, utiliser Punycode pour garantir l'unicité
3. **En dernier recours**, translittérer mais documenter les risques de collision

## Références

- **USE_CASES.md** : UC-02 (Création tenant), UC-04 (Onboarding), UC-06 (Authentification)
- **USER_STORIES.md** : US-2.1 (Créer tenant), US-3.3/3.4 (Gestion accès multi-tenant)
- **LOGGING_ENRICHERS.md** : Extraction de `tenant_id` depuis `acr_values`
- **API_ENDPOINTS.md** : Endpoints avec paramètre tenant
- **RFC 3492** : Punycode standard pour IDN
- **RFC 5891** : Internationalized Domain Names in Applications (IDNA)

---

**Documentation mise à jour : 2025-11-25**
