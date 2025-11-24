# Migration vers le schéma `dbo`

## 📋 Résumé

Toutes les tables de la base de données PostgreSQL ont été déplacées du schéma par défaut `public` vers le schéma `dbo` pour suivre une convention cohérente avec SQL Server.

## 🎯 Changements effectués

### 1. **JohodpDbContext** - Configuration du schéma par défaut

**Fichier**: `src/Johodp.Infrastructure/Persistence/DbContext/JohodpDbContext.cs`

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // Use 'dbo' schema instead of default 'public'
    modelBuilder.HasDefaultSchema("dbo");
    
    // ... rest of configuration
}
```

### 2. **PersistedGrantDbContext** - Configuration du schéma IdentityServer

**Fichier**: `src/Johodp.Api/Extensions/ServiceCollectionExtensions.cs`

```csharp
.AddOperationalStore(options =>
{
    options.ConfigureDbContext = b =>
        b.UseNpgsql(connectionString,
            sql => sql.MigrationsAssembly("Johodp.Infrastructure"));
    
    // Use 'dbo' schema for IdentityServer tables
    options.DefaultSchema = "dbo";
    
    options.EnableTokenCleanup = true;
    options.TokenCleanupInterval = 3600;
});
```

### 3. **Nouvelles migrations créées**

#### Migration JohodpDbContext
- **Nom**: `20251124140240_MoveToDbOSchema`
- **Action**: Déplace 8 tables vers le schéma `dbo`
  - `clients`
  - `tenants`
  - `users`
  - `roles`
  - `permissions`
  - `scopes`
  - `UserRoles` (table de jointure)
  - `UserPermissions` (table de jointure)

#### Migration PersistedGrantDbContext
- **Nom**: `20251124140311_MoveToDbOSchema`
- **Action**: Déplace 5 tables IdentityServer vers le schéma `dbo`
  - `PersistedGrants`
  - `DeviceCodes`
  - `Keys`
  - `ServerSideSessions`
  - `PushedAuthorizationRequests`

## 📊 Inventaire des migrations

### Avant (12 migrations)
- **JohodpDbContext**: 11 migrations
- **PersistedGrantDbContext**: 1 migration

### Après (14 migrations)
- **JohodpDbContext**: 12 migrations (+ MoveToDbOSchema)
- **PersistedGrantDbContext**: 2 migrations (+ MoveToDbOSchema)

## 🚀 Déploiement

### Sur une base de données vierge
Exécutez simplement le script d'initialisation :

**Windows PowerShell:**
```powershell
.\init-db.ps1
```

**Linux/Mac:**
```bash
./init-db.sh
```

Les 14 migrations seront appliquées automatiquement.

### Sur une base de données existante (avec données)

#### Option 1: Migration automatique (RECOMMANDÉ)
```powershell
# Appliquer la migration JohodpDbContext
cd src/Johodp.Infrastructure
dotnet ef database update --startup-project ../Johodp.Api --context JohodpDbContext

# Appliquer la migration PersistedGrantDbContext
dotnet ef database update --startup-project ../Johodp.Api --context Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext
```

#### Option 2: Script SQL manuel (si __EFMigrationsHistory tronquée)
```bash
psql -U postgres -d johodp -f rebuild-migration-history.sql
```

## 🔍 Vérification

### Vérifier que les tables sont dans le bon schéma

```sql
-- Liste toutes les tables avec leur schéma
SELECT schemaname, tablename 
FROM pg_tables 
WHERE schemaname = 'dbo'
ORDER BY tablename;
```

**Résultat attendu (13 tables):**
```
 schemaname |          tablename           
------------+------------------------------
 dbo        | DeviceCodes
 dbo        | Keys
 dbo        | PersistedGrants
 dbo        | PushedAuthorizationRequests
 dbo        | ServerSideSessions
 dbo        | UserPermissions
 dbo        | UserRoles
 dbo        | clients
 dbo        | permissions
 dbo        | roles
 dbo        | scopes
 dbo        | tenants
 dbo        | users
```

### Vérifier les migrations appliquées

```sql
SELECT "MigrationId", "ProductVersion" 
FROM "__EFMigrationsHistory" 
ORDER BY "MigrationId";
```

**Résultat attendu (14 lignes):**
- 12 migrations JohodpDbContext (dont `20251124140240_MoveToDbOSchema`)
- 2 migrations PersistedGrantDbContext (dont `20251124140311_MoveToDbOSchema`)

## ⚠️ Points d'attention

### 1. **Schéma `dbo` doit exister**
La migration crée automatiquement le schéma `dbo` avec `EnsureSchema("dbo")`.

### 2. **Backward compatibility**
Si vous revenez en arrière avec `dotnet ef database update <previous-migration>`, les tables retourneront dans le schéma `public`.

### 3. **Scripts SQL existants**
Tous les scripts SQL qui référencent des tables doivent maintenant utiliser:
- ❌ `SELECT * FROM users`
- ✅ `SELECT * FROM dbo.users`

Ou définir le search_path:
```sql
SET search_path TO dbo;
SELECT * FROM users; -- OK, cherche dans dbo
```

### 4. **Connection string PostgreSQL**
Aucun changement nécessaire dans la connection string. Le schéma est géré au niveau de l'application.

## 📝 Fichiers modifiés

### Code source
- ✅ `src/Johodp.Infrastructure/Persistence/DbContext/JohodpDbContext.cs`
- ✅ `src/Johodp.Api/Extensions/ServiceCollectionExtensions.cs`
- ✅ `src/Johodp.Api/Program.cs` (auto-migration en dev)

### Migrations
- ✅ `src/Johodp.Infrastructure/Migrations/20251124140240_MoveToDbOSchema.cs`
- ✅ `src/Johodp.Infrastructure/Migrations/PersistedGrantDb/20251124140311_MoveToDbOSchema.cs`

### Scripts de déploiement
- ✅ `init-db.ps1` (mis à jour: 14 migrations)
- ✅ `init-db.sh` (mis à jour: 14 migrations)
- ✅ `rebuild-migration-history.sql` (mis à jour: 14 migrations)

## 🎓 Pourquoi `dbo` ?

### Avantages
1. **Convention standard**: `dbo` est le schéma par défaut dans SQL Server
2. **Cohérence multi-SGBD**: Facilite la migration vers SQL Server si nécessaire
3. **Organisation**: Sépare clairement les tables applicatives des tables système
4. **Clarté**: Explicite que ces tables appartiennent au "Database Owner"

### PostgreSQL vs SQL Server
| Aspect | PostgreSQL | SQL Server |
|--------|------------|------------|
| **Schéma par défaut** | `public` | `dbo` |
| **Multiple schémas** | ✅ Oui | ✅ Oui |
| **Création auto** | ✅ Oui (search_path) | ✅ Oui |
| **Permissions** | Par schéma | Par schéma |

## 🔄 Rollback (si nécessaire)

Si vous devez revenir au schéma `public` :

```powershell
# Supprimer les migrations MoveToDbOSchema
cd src/Johodp.Infrastructure
dotnet ef migrations remove --startup-project ../Johodp.Api --context JohodpDbContext
dotnet ef migrations remove --startup-project ../Johodp.Api --context Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext

# Retirer HasDefaultSchema() du code
# Dans JohodpDbContext.cs, commenter/supprimer:
# modelBuilder.HasDefaultSchema("dbo");

# Dans ServiceCollectionExtensions.cs, commenter/supprimer:
# options.DefaultSchema = "dbo";

# Réappliquer les migrations
dotnet ef database update --startup-project ../Johodp.Api --context JohodpDbContext
```

## ✅ Statut

- [x] Configuration JohodpDbContext
- [x] Configuration PersistedGrantDbContext  
- [x] Migration JohodpDbContext créée
- [x] Migration PersistedGrantDbContext créée
- [x] Scripts init-db mis à jour
- [x] Script rebuild-migration-history mis à jour
- [x] Build réussi
- [ ] Migration appliquée sur base de données (à faire par l'utilisateur)
- [ ] Tests de vérification (à faire par l'utilisateur)

## 📚 Références

- [EF Core - Default Schema](https://learn.microsoft.com/en-us/ef/core/modeling/relational/schemas)
- [PostgreSQL - Schemas](https://www.postgresql.org/docs/current/ddl-schemas.html)
- [Duende IdentityServer - Operational Store](https://docs.duendesoftware.com/identityserver/v7/data/operational/)
