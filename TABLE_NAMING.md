# Nomenclature des Tables - Snake Case

## Problème résolu
Les tables IdentityServer utilisaient la convention PascalCase alors que les tables de l'application utilisent snake_case. Cela rendait les requêtes SQL incohérentes dans pgAdmin.

## Solution appliquée
Toutes les tables utilisent maintenant la nomenclature **snake_case en minuscules** avec underscores (`_`) comme séparateurs.

## Tables standardisées

### Tables de l'application (déjà en snake_case)
- ✅ `clients`
- ✅ `users`
- ✅ `tenants`
- ✅ `custom_configurations`

### Tables IdentityServer (renommées)
- ❌ `DeviceCodes` → ✅ `device_codes`
- ❌ `Keys` → ✅ `keys`
- ❌ `PersistedGrants` → ✅ `persisted_grants`
- ❌ `PushedAuthorizationRequests` → ✅ `pushed_authorization_requests`
- ❌ `ServerSideSessions` → ✅ `server_side_sessions`

## Fichiers modifiés

### 1. Configuration IdentityServer
**Fichier**: `src/Johodp.Api/Extensions/ServiceCollectionExtensions.cs`
```csharp
.AddOperationalStore(options =>
{
    // ...
    options.PersistedGrants.Name = "persisted_grants";
    options.DeviceFlowCodes.Name = "device_codes";
    options.Keys.Name = "keys";
    options.ServerSideSessions.Name = "server_side_sessions";
    options.PushedAuthorizationRequests.Name = "pushed_authorization_requests";
});
```

### 2. Migration EF Core
**Fichier**: `src/Johodp.Infrastructure/Migrations/PersistedGrant/20251203021924_RenameIdentityServerTablesToSnakeCase.cs`
- Renomme toutes les tables IdentityServer
- Préserve les clés primaires et index

## Migration de base de données existante

### Pour une nouvelle installation
```bash
dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api --context PersistedGrantDbContext
```

### Pour une base de données existante
Exécuter le script SQL: `rename-tables-to-snake-case.sql`

Ce script:
1. Renomme les tables avec ALTER TABLE
2. Affiche la liste des tables pour vérification
3. Fournit la commande pour mettre à jour __EFMigrationsHistory

## Vérification dans pgAdmin
Toutes les requêtes SQL peuvent maintenant utiliser une nomenclature cohérente:

```sql
-- Avant (incohérent)
SELECT * FROM dbo.clients;           -- snake_case
SELECT * FROM dbo."PersistedGrants"; -- PascalCase avec quotes

-- Après (cohérent)
SELECT * FROM dbo.clients;
SELECT * FROM dbo.persisted_grants;
```

## Tests
✅ Tous les tests d'intégration passent (6/6)
✅ Build réussi sans warnings
✅ Migrations générées correctement

## Notes
- Les noms de tables en minuscules sans quotes fonctionnent directement dans PostgreSQL
- Plus besoin de double-quotes pour les noms de tables
- Cohérence avec les conventions SQL standard et PostgreSQL best practices
