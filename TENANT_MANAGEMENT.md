# Gestion des Tenants - Implémentation Complète

## Vue d'ensemble

Implémentation complète d'un système de gestion des tenants avec branding, langues, et returnUrls. Quand un tenant est créé ou mis à jour, les clients associés sont automatiquement synchronisés avec les returnUrls du tenant.

## Architecture Implémentée

### 1. Couche Domaine (Domain Layer)

#### Agrégat Tenant (`Tenant.cs`)
```
src/Johodp.Domain/Tenants/Aggregates/Tenant.cs
```

**Propriétés principales :**
- `TenantId` : Identifiant unique (Value Object)
- `Name` : Nom unique du tenant (normalisé en minuscules)
- `DisplayName` : Nom d'affichage
- `IsActive` : Statut actif/inactif
- **Branding :**
  - `PrimaryColor` : Couleur primaire (#hex)
  - `SecondaryColor` : Couleur secondaire (#hex)
  - `LogoUrl` : URL ou base64 du logo
  - `BackgroundImageUrl` : URL ou base64 de l'image de fond
  - `CustomCss` : CSS personnalisé
- **Localisation :**
  - `DefaultLanguage` : Langue par défaut (ex: "fr-FR")
  - `SupportedLanguages` : Liste des langues supportées
  - `Timezone` : Fuseau horaire (ex: "Europe/Paris")
  - `Currency` : Devise (ex: "EUR")
- **OAuth2/OIDC :**
  - `AllowedReturnUrls` : Liste des URLs de redirection autorisées
  - `AssociatedClientIds` : Liste des clients OAuth2 associés

**Méthodes du domaine :**
- `Create()` : Création d'un nouveau tenant
- `UpdateBranding()` : Mise à jour du branding
- `AddSupportedLanguage()` / `RemoveSupportedLanguage()`
- `SetDefaultLanguage()`
- `UpdateLocalization()` : Mise à jour timezone/currency
- `AddAllowedReturnUrl()` / `RemoveAllowedReturnUrl()`
- `AddAssociatedClient()` / `RemoveAssociatedClient()`
- `Activate()` / `Deactivate()`

#### Value Object TenantId
```
src/Johodp.Domain/Tenants/ValueObjects/TenantId.cs
```

### 2. Couche Application (Application Layer)

#### DTOs
```
src/Johodp.Application/Tenants/DTOs/TenantDto.cs
```

- `TenantDto` : Représentation complète d'un tenant
- `CreateTenantDto` : DTO pour la création
- `UpdateTenantDto` : DTO pour la mise à jour (propriétés nullables)

#### Commands

**CreateTenantCommand**
```
src/Johodp.Application/Tenants/Commands/CreateTenantCommand.cs
```

Crée un nouveau tenant et synchronise automatiquement les clients associés avec les returnUrls du tenant.

**Flux d'exécution :**
1. Valide que le nom du tenant n'existe pas déjà
2. Crée l'agrégat Tenant avec toutes les propriétés
3. Sauvegarde le tenant dans la base de données
4. Pour chaque client associé :
   - Récupère le client via `IClientRepository.GetByNameAsync()`
   - Ajoute les returnUrls du tenant aux redirectUris du client
   - Met à jour le client

**UpdateTenantCommand**
```
src/Johodp.Application/Tenants/Commands/UpdateTenantCommand.cs
```

Met à jour un tenant existant et synchronise les clients si les returnUrls ou les associations ont changé.

**Flux d'exécution :**
1. Récupère le tenant par ID
2. Met à jour toutes les propriétés spécifiées (nullables)
3. Si returnUrls ou associatedClientIds ont changé :
   - **Supprime tous les redirectUris** des clients associés
   - **Ajoute les nouveaux returnUrls** du tenant

**⚠️ Comportement important :** La mise à jour est **destructive** pour les clients - elle remplace complètement les redirectUris existants par ceux du tenant.

#### Queries

```
src/Johodp.Application/Tenants/Queries/TenantQueries.cs
```

- `GetTenantByIdQuery` : Récupération par ID (Guid)
- `GetAllTenantsQuery` : Liste de tous les tenants
- `GetTenantByNameQuery` : Récupération par nom (string)

### 3. Couche Infrastructure (Infrastructure Layer)

#### Repository
```
src/Johodp.Infrastructure/Persistence/Repositories/TenantRepository.cs
```

Implémente `ITenantRepository` avec méthodes :
- `GetByIdAsync(TenantId)`
- `GetByNameAsync(string)` - normalise en minuscules
- `GetAllAsync()`
- `GetActiveTenantsAsync()`
- `AddAsync(Tenant)`
- `UpdateAsync(Tenant)`
- `DeleteAsync(TenantId)`
- `ExistsAsync(string)` - vérifie si un nom existe

#### Configuration EF Core
```
src/Johodp.Infrastructure/Persistence/Configurations/TenantConfiguration.cs
```

**Mappages de base de données :**
- Table : `tenants`
- Index unique sur `Name`
- Conversion de `TenantId` (Value Object) en Guid
- Collections stockées en **JSONB** PostgreSQL :
  - `SupportedLanguages`
  - `AllowedReturnUrls`
  - `AssociatedClientIds`

**Schéma de table :**
```sql
CREATE TABLE tenants (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    display_name VARCHAR(200) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP,
    primary_color VARCHAR(50),
    secondary_color VARCHAR(50),
    logo_url VARCHAR(500),
    background_image_url VARCHAR(500),
    custom_css TEXT,
    default_language VARCHAR(10) NOT NULL DEFAULT 'fr-FR',
    timezone VARCHAR(50) NOT NULL DEFAULT 'Europe/Paris',
    currency VARCHAR(10) NOT NULL DEFAULT 'EUR',
    supported_languages JSONB NOT NULL,
    allowed_return_urls JSONB NOT NULL,
    associated_client_ids JSONB NOT NULL
);
```

#### DbContext
```
src/Johodp.Infrastructure/Persistence/DbContext/JohodpDbContext.cs
```

Ajout de `DbSet<Tenant> Tenants` et application de la configuration.

### 4. Couche API (API Layer)

#### TenantController
```
src/Johodp.Api/Controllers/TenantController.cs
```

**Endpoints CRUD :**

| Méthode | Route | Description | Auth |
|---------|-------|-------------|------|
| GET | `/api/tenant` | Liste tous les tenants | Oui |
| GET | `/api/tenant/{id}` | Récupère un tenant par ID | Oui |
| GET | `/api/tenant/by-name/{name}` | Récupère un tenant par nom | Oui |
| POST | `/api/tenant` | Crée un nouveau tenant | Oui |
| PUT | `/api/tenant/{id}` | Met à jour un tenant | Oui |

**Endpoints legacy (compatibilité) :**

| Méthode | Route | Description | Auth |
|---------|-------|-------------|------|
| GET | `/api/tenant/{tenantId}/branding.css` | CSS de branding | Non |
| GET | `/api/tenant/{tenantId}/language` | Paramètres de langue | Non |

**Logging complet :**
Tous les endpoints incluent du logging structuré conforme à `JOURNALISATION.md` :
- LogInformation pour les opérations réussies
- LogWarning pour les erreurs métier (tenant non trouvé, nom existant)
- LogError pour les exceptions techniques

#### Enregistrement des services
```
src/Johodp.Api/Extensions/ServiceCollectionExtensions.cs
```

Ajout de :
- `ITenantRepository` → `TenantRepository`
- `CreateTenantCommandHandler`
- `UpdateTenantCommandHandler`
- `GetTenantByIdQueryHandler`
- `GetAllTenantsQueryHandler`
- `GetTenantByNameQueryHandler`

## Migration de Base de Données

### Migration créée
```
src/Johodp.Infrastructure/Migrations/[timestamp]_AddTenantEntity.cs
```

**Pour appliquer la migration :**

1. **Démarrer PostgreSQL** (via Docker ou local)
   ```bash
   docker-compose up -d postgres
   ```

2. **Appliquer la migration**
   ```bash
   cd src/Johodp.Infrastructure
   dotnet ef database update --startup-project ../Johodp.Api
   ```

## Utilisation

### Créer un tenant

**POST** `/api/tenant`

```json
{
  "name": "acme",
  "displayName": "ACME Corporation",
  "defaultLanguage": "fr-FR",
  "supportedLanguages": ["fr-FR", "en-US"],
  "primaryColor": "#0078d4",
  "secondaryColor": "#106ebe",
  "logoUrl": "https://example.com/logo.png",
  "customCss": "body { font-family: Arial; }",
  "timezone": "Europe/Paris",
  "currency": "EUR",
  "allowedReturnUrls": [
    "https://acme.com/callback",
    "https://app.acme.com/signin-oidc"
  ],
  "associatedClientIds": [
    "acme-spa",
    "acme-mobile"
  ]
}
```

**Effet :**
1. Crée le tenant "acme"
2. Pour les clients "acme-spa" et "acme-mobile" :
   - Ajoute les deux returnUrls à leurs `AllowedRedirectUris`

### Mettre à jour un tenant

**PUT** `/api/tenant/{id}`

```json
{
  "displayName": "ACME Corp",
  "primaryColor": "#ff6b6b",
  "allowedReturnUrls": [
    "https://new.acme.com/callback"
  ]
}
```

**Effet :**
1. Met à jour le branding et les returnUrls
2. Pour tous les clients associés :
   - **Supprime tous les redirectUris existants**
   - **Ajoute uniquement** `https://new.acme.com/callback`

⚠️ **Attention :** C'est un remplacement total, pas une fusion.

### Récupérer le branding

**GET** `/api/tenant/{tenantId}/branding.css` (sans authentification)

Retourne du CSS avec variables CSS :
```css
:root {
    --primary-color: #0078d4;
    --secondary-color: #106ebe;
    --logo-base64: url('...');
    --image-base64: url('...');
}
/* + customCss du tenant */
```

### Récupérer les paramètres de langue

**GET** `/api/tenant/{tenantId}/language` (sans authentification)

```json
{
  "tenantId": "acme",
  "defaultLanguage": "fr-FR",
  "supportedLanguages": ["fr-FR", "en-US"],
  "dateFormat": "dd/MM/yyyy",
  "timeFormat": "HH:mm",
  "timezone": "Europe/Paris",
  "currency": "EUR"
}
```

## Relation User-Tenant avec Role et Scope

### Architecture UserTenant

Chaque utilisateur peut être associé à plusieurs tenants via l'entité `UserTenant` qui contient:
- **UserId** (Guid) - Référence vers l'utilisateur
- **TenantId** (Guid) - Référence vers le tenant
- **Role** (string, max 100 chars) - Rôle fourni par l'application tierce (ex: "admin", "user", "manager")
- **Scope** (string, max 200 chars) - Périmètre fourni par l'application tierce (ex: "full_access", "read_only", "department_sales")
- **CreatedAt** (timestamp) - Date de création de l'association
- **UpdatedAt** (timestamp, nullable) - Date de dernière modification

### Table de Jointure

```sql
CREATE TABLE "UserTenants" (
    "UserId" UUID NOT NULL,
    "TenantId" UUID NOT NULL,
    "Role" VARCHAR(100) NOT NULL,
    "Scope" VARCHAR(200) NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE,
    CONSTRAINT "PK_UserTenants" PRIMARY KEY ("UserId", "TenantId"),
    CONSTRAINT "FK_UserTenants_Users" FOREIGN KEY ("UserId") REFERENCES "users"("id") ON DELETE CASCADE,
    CONSTRAINT "FK_UserTenants_Tenants" FOREIGN KEY ("TenantId") REFERENCES "tenants"("id") ON DELETE CASCADE
);

CREATE INDEX "IX_UserTenants_UserId" ON "UserTenants" ("UserId");
CREATE INDEX "IX_UserTenants_TenantId" ON "UserTenants" ("TenantId");
```

### Endpoints de Gestion

**Ajouter un tenant à un utilisateur:**
```bash
POST /api/users/{userId}/tenants
{
  "tenantId": "guid",
  "role": "admin",
  "scope": "full_access"
}
```

**Modifier le role/scope:**
```bash
PUT /api/users/{userId}/tenants/{tenantId}
{
  "role": "manager",
  "scope": "department_sales"
}
```

**Supprimer l'accès:**
```bash
DELETE /api/users/{userId}/tenants/{tenantId}
```

### Génération des Claims JWT

Lors de la connexion sur un tenant spécifique, le JWT contient **uniquement** les claims de ce tenant:

```json
{
  "sub": "user-guid",
  "email": "user@example.com",
  "tenant_id": "tenant-guid",
  "tenant_role": "admin",
  "tenant_scope": "full_access"
}
```

Si l'utilisateur se connecte sur un autre tenant, il recevra un JWT différent avec les role/scope de ce tenant.

### Exemple Multi-Tenant

**Consultant travaillant pour 3 clients:**

```json
{
  "email": "consultant@agency.com",
  "firstName": "Jane",
  "lastName": "Smith",
  "tenants": [
    {
      "tenantId": "client-a-guid",
      "role": "architect",
      "scope": "project_alpha"
    },
    {
      "tenantId": "client-b-guid",
      "role": "developer",
      "scope": "project_beta"
    },
    {
      "tenantId": "client-c-guid",
      "role": "reviewer",
      "scope": "all_projects"
    }
  ]
}
```

Lors de la connexion:
- Sur `client-a` → JWT avec `tenant_role: "architect"` et `tenant_scope: "project_alpha"`
- Sur `client-b` → JWT avec `tenant_role: "developer"` et `tenant_scope: "project_beta"`
- Sur `client-c` → JWT avec `tenant_role: "reviewer"` et `tenant_scope: "all_projects"`

### Règles de Validation

1. **Role et Scope obligatoires** - Ne peuvent pas être vides
2. **Pas de doublons** - Un utilisateur ne peut pas être associé deux fois au même tenant
3. **Strings libres** - Aucune validation stricte sur les valeurs (définis par l'app tierce)
4. **Isolation contextuelle** - Les JWT ne contiennent que les claims du tenant de connexion

## Synchronisation Tenant ↔ Client

### Logique de synchronisation

**Lors de la création d'un tenant :**
```csharp
foreach (var clientId in tenant.AssociatedClientIds)
{
    var client = await _clientRepository.GetByNameAsync(clientId);
    foreach (var url in tenant.AllowedReturnUrls)
    {
        if (!client.AllowedRedirectUris.Contains(url))
            client.AddRedirectUri(url);
    }
}
```

**Lors de la mise à jour d'un tenant :**
```csharp
foreach (var clientId in tenant.AssociatedClientIds)
{
    var client = await _clientRepository.GetByNameAsync(clientId);
    
    // Supprime TOUS les redirectUris
    foreach (var uri in client.AllowedRedirectUris.ToList())
        client.RemoveRedirectUri(uri);
    
    // Ajoute SEULEMENT les returnUrls du tenant
    foreach (var url in tenant.AllowedReturnUrls)
        client.AddRedirectUri(url);
}
```

### Cas d'usage

**Scénario 1 : Nouveau tenant avec 2 clients**
- Tenant "acme" créé avec returnUrls: `["https://app.com/callback"]`
- Clients associés: `["client-a", "client-b"]`
- **Résultat :** Les deux clients ont maintenant `https://app.com/callback` dans leurs redirectUris

**Scénario 2 : Mise à jour des returnUrls**
- Tenant "acme" met à jour returnUrls: `["https://new.app.com/callback"]`
- **Résultat :** Tous les anciens redirectUris des clients sont supprimés, seul `https://new.app.com/callback` reste

**Scénario 3 : Ajout d'un nouveau client associé**
- Tenant "acme" mis à jour avec `associatedClientIds: ["client-a", "client-b", "client-c"]`
- **Résultat :** Le client-c reçoit les returnUrls du tenant, client-a et client-b sont écrasés

## Validation et Règles Métier

### Règles du domaine

1. **Nom du tenant unique** : Vérifié avant création
2. **Nom normalisé** : Toujours en minuscules
3. **Langue par défaut requise** : Toujours dans les langues supportées
4. **Impossible de supprimer la langue par défaut**
5. **ReturnUrls validées** : Doivent être des URI absolues valides
6. **Timestamps automatiques** : `CreatedAt` et `UpdatedAt`

### Gestion des erreurs

| Erreur | Status HTTP | Message |
|--------|-------------|---------|
| Tenant déjà existant | 400 Bad Request | "A tenant with name 'X' already exists" |
| Tenant non trouvé | 404 Not Found | "Tenant with ID 'X' not found" |
| Langue par défaut invalide | 400 Bad Request | (exception du domaine) |
| ReturnUrl invalide | 400 Bad Request | "Return URL must be a valid absolute URI" |
| Erreur technique | 500 Internal Server Error | "An error occurred while..." |

## Points d'Attention

### Performance

- Les collections (languages, returnUrls, clientIds) sont stockées en **JSONB** pour éviter les tables de jointure
- La synchronisation des clients se fait en **boucle synchrone** - pourrait être optimisée avec des queries batch
- **Pas de cache** implémenté - chaque requête va en base de données

### Sécurité

- Les endpoints CRUD requièrent **authentification** (`[Authorize]`)
- Les endpoints branding/language sont **publics** pour permettre le chargement avant login
- **Aucune validation des droits** sur les opérations - à ajouter selon les rôles

### Évolutions possibles

1. **Cache distribué** : Mettre en cache les tenants (voir `CACHE.md`)
2. **Événements de domaine** : Publier un événement `TenantUpdated` pour la sync asynchrone
3. **Audit trail** : Tracer les modifications des tenants
4. **Soft delete** : Ajouter `DeletedAt` au lieu de supprimer physiquement
5. **Validation des CSS** : Vérifier que le CustomCss ne contient pas de code malveillant
6. **Gestion des droits** : Vérifier que l'utilisateur peut modifier un tenant
7. **Webhook** : Notifier les clients associés lors des mises à jour
8. **Versioning** : Gérer les versions de configuration de branding

## Tests à Effectuer

### Tests manuels recommandés

1. **Créer un tenant** avec toutes les propriétés
2. **Vérifier la synchronisation** des clients associés
3. **Mettre à jour les returnUrls** et confirmer l'écrasement
4. **Récupérer le branding** via l'endpoint public
5. **Tester avec un tenant inexistant** (404)
6. **Créer un tenant avec un nom existant** (400)
7. **Tester le endpoint by-name**
8. **Désactiver un tenant** et vérifier `IsActive`

### Tests unitaires à ajouter

```csharp
// Domain tests
[Test] public void Tenant_Create_ShouldNormalizeName()
[Test] public void Tenant_RemoveDefaultLanguage_ShouldThrow()
[Test] public void Tenant_AddInvalidReturnUrl_ShouldThrow()

// Application tests
[Test] public async Task CreateTenant_WithExistingName_ShouldThrow()
[Test] public async Task CreateTenant_ShouldSyncClients()
[Test] public async Task UpdateTenant_ShouldReplaceClientRedirectUris()

// Integration tests
[Test] public async Task POST_Tenant_ReturnsCreated()
[Test] public async Task PUT_Tenant_UpdatesDatabase()
[Test] public async Task GET_BrandingCss_ReturnsValidCss()
```

## Documentation Liée

- **Logging** : Voir `JOURNALISATION.md` pour les pratiques de logging appliquées
- **Cache** : Voir `CACHE.md` pour implémenter un cache de tenants
- **Architecture** : Voir `ARCHITECTURE.md` pour comprendre la séparation des couches

## Commandes Utiles

```bash
# Build
dotnet build

# Créer une migration
cd src/Johodp.Infrastructure
dotnet ef migrations add MigrationName --startup-project ../Johodp.Api

# Appliquer les migrations
dotnet ef database update --startup-project ../Johodp.Api

# Lancer l'application
dotnet run --project src/Johodp.Api

# Tester les endpoints
curl -X POST https://localhost:5001/api/tenant \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d @tenant.json
```

## Résumé

✅ **Implémenté :**
- Agrégat Tenant complet avec branding, langues, et returnUrls
- Repository pattern avec EF Core et PostgreSQL
- Commands et Queries avec handlers
- API RESTful avec logging
- Synchronisation automatique Tenant → Client
- Migration de base de données
- Validation métier et gestion d'erreurs

⏳ **À faire (optionnel) :**
- Appliquer la migration (nécessite PostgreSQL en cours d'exécution)
- Tests unitaires et d'intégration
- Cache distribué
- Gestion des droits fine
- Événements de domaine pour sync asynchrone
- Webhook pour notifications
