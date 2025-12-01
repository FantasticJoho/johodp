# 🔧 Stratégie de Migration : Approche Hybride (SQL + EF Core)

## Problématique

Entity Framework Core crée par défaut la table `__EFMigrationsHistory` dans le schéma `public` de PostgreSQL, même si les tables métiers sont configurées pour le schéma `dbo`. Cela crée une incohérence dans l'organisation de la base de données.

## Solution Retenue : Approche Hybride

**Combiner SQL pour l'initialisation et EF Core pour les migrations** :

1. **Script SQL minimal** (`init-schema.sql`) : Crée le schéma `dbo` et la table `__EFMigrationsHistory`
2. **Configuration EF Core** : Force l'utilisation de `dbo.__EFMigrationsHistory` via `MigrationsHistoryTable()`
3. **Migrations EF Core** : Appliquées normalement avec `dotnet ef database update`

### Configuration Nécessaire

Pour que cette approche fonctionne, il faut configurer `MigrationsHistoryTable` dans TOUS les contextes :

#### JohodpDbContext

Dans `ServiceCollectionExtensions.cs` :
```csharp
services.AddDbContext<JohodpDbContext>(options =>
    options.UseNpgsql(dataSource,
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("Johodp.Infrastructure");
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
        }));
```

Dans `JohodpDbContextFactory.cs` :
```csharp
optionsBuilder.UseNpgsql(connectionString, 
    sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo"));
```

#### PersistedGrantDbContext (Duende IdentityServer)

Dans `ServiceCollectionExtensions.cs` :
```csharp
.AddOperationalStore(options =>
{
    options.ConfigureDbContext = b =>
        b.UseNpgsql(connectionString,
            sql =>
            {
                sql.MigrationsAssembly("Johodp.Infrastructure");
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
            });
    
    options.DefaultSchema = "dbo";
    // ... autres options
});
```

### Avantages

- ✅ Le schéma `dbo` et `__EFMigrationsHistory` sont créés dans le bon schéma dès le départ
- ✅ Utilisation normale de `dotnet ef migrations add` pour créer de nouvelles migrations
- ✅ Utilisation normale de `dotnet ef database update` pour appliquer les migrations
- ✅ Workflow de développement EF Core standard après l'initialisation
- ✅ Pas besoin de régénérer manuellement les scripts SQL à chaque migration

## Scripts Disponibles

### Approche Hybride (RECOMMANDÉE)

#### Windows (PowerShell)
```powershell
.\init-db-hybrid.ps1
```

#### Linux/Mac (Bash)
```bash
chmod +x init-db-hybrid.sh
./init-db-hybrid.sh
```

### Approche SQL Pure (Alternative)

Si vous préférez gérer les migrations manuellement via SQL :

### 1. Génération des Scripts SQL

```powershell
# Générer le script pour JohodpDbContext
dotnet ef migrations script -p src/Johodp.Infrastructure -s src/Johodp.Api --context JohodpDbContext --idempotent --output migration-johodp.sql

# Générer le script pour PersistedGrantDbContext (IdentityServer)
dotnet ef migrations script -p src/Johodp.Infrastructure -s src/Johodp.Api --context Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext --idempotent --output migration-identityserver.sql
```

### 2. Initialisation de la Base de Données

#### Windows (PowerShell)
```powershell
.\init-db-sql.ps1
```

#### Linux/Mac (Bash)
```bash
chmod +x init-db-sql.sh
./init-db-sql.sh
```

**Note** : Cette approche nécessite de régénérer les scripts SQL à chaque nouvelle migration.

## Workflow Complet

### Première Installation (Approche Hybride)

1. **Démarrer PostgreSQL**
   ```powershell
   docker-compose up -d
   ```

2. **Initialiser la base de données**
   ```powershell
   .\init-db-hybrid.ps1
   ```
   
   Ce script :
   - Crée le schéma `dbo` et `__EFMigrationsHistory` via SQL
   - Applique les migrations JohodpDbContext
   - Applique les migrations PersistedGrantDbContext

### Reset Complet

1. **Supprimer toutes les tables**
   ```powershell
   docker exec -i johodp-postgres psql -U postgres -d johodp -c "DROP SCHEMA dbo CASCADE; CREATE SCHEMA dbo;"
   ```

2. **Réinitialiser**
   ```powershell
   .\init-db-hybrid.ps1
   ```

## Ajout de Nouvelles Migrations

Avec l'approche hybride, le workflow EF Core standard fonctionne normalement après l'initialisation.

### Créer une nouvelle migration

```powershell
# Pour JohodpDbContext
dotnet ef migrations add NomDeLaMigration -p src/Johodp.Infrastructure -s src/Johodp.Api --context JohodpDbContext
```

### Appliquer la migration

```powershell
# Pour JohodpDbContext
dotnet ef database update -p src/Johodp.Infrastructure -s src/Johodp.Api --context JohodpDbContext
```

**Note** : Après l'initialisation hybride, vous pouvez utiliser `dotnet ef database update` directement car `__EFMigrationsHistory` est déjà dans `dbo`.

---

## Alternative : Approche SQL Pure

Si vous préférez continuer à utiliser uniquement du SQL pour toutes les migrations :

### Régénérer les scripts après une nouvelle migration

```powershell
# Régénérer le script pour JohodpDbContext
dotnet ef migrations script -p src/Johodp.Infrastructure -s src/Johodp.Api --context JohodpDbContext --idempotent --output migration-johodp.sql

# Appliquer
.\init-db-sql.ps1
```

## Vérification

Vérifier que toutes les tables sont dans `dbo` :

```powershell
docker exec -i johodp-postgres psql -U postgres -d johodp -c "SELECT schemaname, tablename FROM pg_tables WHERE schemaname = 'dbo' ORDER BY tablename;"
```

Résultat attendu :
```
 schemaname |          tablename
------------+-----------------------------
 dbo        | DeviceCodes
 dbo        | Keys
 dbo        | PersistedGrants
 dbo        | PushedAuthorizationRequests
 dbo        | ServerSideSessions
 dbo        | UserTenants
 dbo        | __EFMigrationsHistory
 dbo        | clients
 dbo        | custom_configurations
 dbo        | tenants
 dbo        | users
```

## Avantages de cette Approche

1. **Contrôle Total** : Vous voyez exactement le SQL exécuté
2. **Idempotence** : Les scripts peuvent être exécutés plusieurs fois sans erreur
3. **Cohérence** : Tout est dans le même schéma dès le départ
4. **CI/CD Friendly** : Facile à intégrer dans un pipeline
5. **Debugging** : Les scripts SQL peuvent être inspectés et modifiés si nécessaire

## Alternative : EF Core Pur (si __EFMigrationsHistory existe déjà dans dbo)

Si vous avez déjà exécuté `init-db-sql.ps1` une fois, vous pouvez utiliser `init-db.ps1` pour les migrations suivantes :

```powershell
.\init-db.ps1
```

Ce script utilisera directement EF Core car `__EFMigrationsHistory` est déjà dans le bon schéma.

## Troubleshooting

### Erreur : "relation dbo.__EFMigrationsHistory does not exist"

**Cause** : La table de tracking des migrations n'existe pas encore.

**Solution** : Utilisez `init-db-sql.ps1` au lieu de `init-db.ps1`

### Erreur : "relation already exists"

**Cause** : Les tables existent déjà.

**Solution** : 
1. Supprimer les tables : `Get-Content drop-all-tables.sql | docker exec -i johodp-postgres psql -U postgres -d johodp`
2. Réinitialiser : `.\init-db-sql.ps1`

### Les tables sont dans 'public' au lieu de 'dbo'

**Cause** : Vous avez utilisé `dotnet ef database update` directement au lieu du script SQL.

**Solution** :
1. Supprimer toutes les tables
2. Utiliser `init-db-sql.ps1`

## Fichiers Importants

- `migration-johodp.sql` : Script SQL pour les tables métiers (généré)
- `migration-identityserver.sql` : Script SQL pour IdentityServer (généré)
- `init-db-sql.ps1` / `init-db-sql.sh` : Scripts d'initialisation via SQL
- `init-db.ps1` / `init-db.sh` : Scripts d'initialisation via EF Core (après première migration SQL)
- `drop-all-tables.sql` : Script pour supprimer toutes les tables du schéma `dbo`
- `drop-all-tables-generic.sql` : Script pour supprimer toutes les tables du schéma `public`

## Documentation Mise à Jour

Cette stratégie est maintenant documentée dans :
- ✅ `QUICKSTART.md` : Guide de démarrage rapide
- ✅ `MIGRATIONS_STRATEGY.md` : Ce document
- ✅ `README.md` : Instructions d'installation

## Commandes de Référence Rapide

```powershell
# Première installation
docker-compose up -d
.\init-db-sql.ps1

# Reset complet
Get-Content drop-all-tables.sql | docker exec -i johodp-postgres psql -U postgres -d johodp
.\init-db-sql.ps1

# Ajouter une migration
dotnet ef migrations add MaMigration -p src/Johodp.Infrastructure -s src/Johodp.Api --context JohodpDbContext
dotnet ef migrations script -p src/Johodp.Infrastructure -s src/Johodp.Api --context JohodpDbContext --idempotent --output migration-johodp.sql
.\init-db-sql.ps1

# Vérifier
docker exec -i johodp-postgres psql -U postgres -d johodp -c "SELECT schemaname, COUNT(*) FROM pg_tables WHERE schemaname = 'dbo' GROUP BY schemaname;"
```
