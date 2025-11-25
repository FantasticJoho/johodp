# 🔄 Migrations API de Johodp Identity Provider

## Vue d'ensemble

L'API Migrations permet de gérer les migrations de base de données via HTTP au lieu de la ligne de commande. Utile pour les déploiements automatisés et les environnements de développement.

⚠️ **ATTENTION** : Ces endpoints sont **désactivés en production** pour des raisons de sécurité.

---

## 🎯 Endpoints disponibles

### 1. `POST /api/migrations/up` - Appliquer les migrations

**Description** : Applique toutes les migrations pending pour JohodpDbContext et PersistedGrantDbContext.

**Méthode** : `POST`

**Headers** : Aucun requis

**Sécurité** :
- ❌ Désactivé en production (retourne 403)
- ✅ Activé en Development/Staging

**Réponse (200 OK)** :
```json
{
  "success": true,
  "message": "All migrations applied successfully",
  "johodpDbContext": {
    "appliedMigrations": 11,
    "migrations": [
      "20250101000000_InitialCreate",
      "20250102000000_AddTenants",
      "..."
    ]
  },
  "persistedGrantDbContext": {
    "appliedMigrations": 1,
    "migrations": [
      "20250101000000_IdentityServerPersistedGrant"
    ]
  }
}
```

**Réponse (403 Forbidden)** - En production :
```json
{
  "error": "Forbidden",
  "message": "Migration endpoints are disabled in production. Use init-db.ps1 script instead."
}
```

**Réponse (500 Internal Server Error)** :
```json
{
  "error": "Migration failed",
  "message": "Npgsql.NpgsqlException: Connection refused",
  "stackTrace": "..." // Uniquement en Development
}
```

**Exemple cURL** :
```bash
curl -X POST http://localhost:5000/api/migrations/up
```

**Exemple PowerShell** :
```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/migrations/up"
```

---

### 2. `POST /api/migrations/down` - Rollback complet

**Description** : Supprime TOUTES les tables et données (DROP DATABASE). **Opération destructive !**

**Méthode** : `POST`

**Sécurité** :
- ❌ Désactivé en production (retourne 403)
- ⚠️ Utiliser uniquement en développement local

**Réponse (200 OK)** :
```json
{
  "success": true,
  "message": "All databases dropped successfully. Run POST /api/migrations/up to recreate."
}
```

**Réponse (403 Forbidden)** - En production :
```json
{
  "error": "Forbidden",
  "message": "Migration DOWN is disabled in production for safety."
}
```

**Exemple cURL** :
```bash
curl -X POST http://localhost:5000/api/migrations/down
```

**Avertissement** :
```
⚠️ DANGER : Cette opération SUPPRIME TOUTES LES DONNÉES
Utilisez uniquement avec des données de test
```

---

### 3. `GET /api/migrations/status` - État des migrations

**Description** : Affiche l'état actuel des migrations (appliquées et en attente).

**Méthode** : `GET`

**Sécurité** : Disponible en tous environnements (lecture seule)

**Réponse (200 OK)** :
```json
{
  "timestamp": "2025-11-25T10:30:00Z",
  "environment": "Development",
  "johodpDbContext": {
    "canConnect": true,
    "appliedMigrations": 10,
    "pendingMigrations": 1,
    "applied": [
      "20250101000000_InitialCreate",
      "20250102000000_AddTenants",
      "..."
    ],
    "pending": [
      "20250115000000_AddUserScopes"
    ]
  },
  "persistedGrantDbContext": {
    "canConnect": true,
    "appliedMigrations": 1,
    "pendingMigrations": 0,
    "applied": [
      "20250101000000_IdentityServerPersistedGrant"
    ],
    "pending": []
  }
}
```

**Réponse (500 Internal Server Error)** :
```json
{
  "error": "Failed to get migration status",
  "message": "Npgsql.NpgsqlException: Connection refused"
}
```

**Exemple cURL** :
```bash
curl http://localhost:5000/api/migrations/status
```

**Exemple PowerShell** :
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/migrations/status"
```

---

## 🔄 Workflows typiques

### Workflow 1 : Premier déploiement

```bash
# 1. Vérifier l'état des migrations
curl http://localhost:5000/api/migrations/status

# 2. Appliquer toutes les migrations
curl -X POST http://localhost:5000/api/migrations/up

# 3. Vérifier que tout est appliqué
curl http://localhost:5000/api/migrations/status
# → appliedMigrations: 11, pendingMigrations: 0
```

### Workflow 2 : Reset complet (développement)

```bash
# 1. Supprimer toutes les données
curl -X POST http://localhost:5000/api/migrations/down

# 2. Recréer les tables
curl -X POST http://localhost:5000/api/migrations/up

# 3. Vérifier
curl http://localhost:5000/api/migrations/status
```

### Workflow 3 : Déploiement continu (CI/CD)

```yaml
# .github/workflows/deploy.yml
steps:
  - name: Check migration status
    run: |
      STATUS=$(curl -s http://staging.example.com/api/migrations/status)
      PENDING=$(echo $STATUS | jq '.johodpDbContext.pendingMigrations')
      
      if [ "$PENDING" -gt 0 ]; then
        echo "⚠️ $PENDING pending migrations detected"
        curl -X POST http://staging.example.com/api/migrations/up
      else
        echo "✅ All migrations already applied"
      fi
```

---

## 🛡️ Sécurité

### Protection en Production

Le contrôleur vérifie l'environnement :

```csharp
if (_environment.IsProduction())
{
    return StatusCode(403, new
    {
        error = "Forbidden",
        message = "Migration endpoints are disabled in production."
    });
}
```

### Bloquer complètement dans Nginx (Production)

```nginx
location ~ ^/api/migrations/(up|down|status)$ {
    return 404; # Bloquer tout accès
}
```

### Variables d'environnement

```bash
# Development
ASPNETCORE_ENVIRONMENT=Development

# Staging (migrations autorisées)
ASPNETCORE_ENVIRONMENT=Staging

# Production (migrations désactivées)
ASPNETCORE_ENVIRONMENT=Production
```

---

## 🧪 Testing

### Test avec Docker Compose

```yaml
version: '3.8'

services:
  johodp-idp:
    image: johodp-idp:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=johodp;Username=postgres;Password=postgres
    depends_on:
      - postgres
  
  postgres:
    image: postgres:16
    environment:
      - POSTGRES_PASSWORD=postgres
```

**Tester les migrations** :
```bash
# Démarrer les services
docker-compose up -d

# Attendre que la DB soit prête
sleep 5

# Vérifier l'état
curl http://localhost:5000/api/migrations/status

# Appliquer les migrations
curl -X POST http://localhost:5000/api/migrations/up
```

---

## 📊 Logs

Les migrations génèrent des logs détaillés :

```
[2025-11-25 10:30:00.123 UTC] [INF] Starting database migrations (UP)...
[2025-11-25 10:30:00.234 UTC] [INF] Applying JohodpDbContext migrations...
[2025-11-25 10:30:02.567 UTC] [INF] ✅ JohodpDbContext migrations applied. Total: 11
[2025-11-25 10:30:02.678 UTC] [INF] Applying PersistedGrantDbContext migrations...
[2025-11-25 10:30:03.123 UTC] [INF] ✅ PersistedGrantDbContext migrations applied. Total: 1
```

En cas d'erreur :
```
[2025-11-25 10:30:00.123 UTC] [ERR] ❌ Migration UP failed
Npgsql.NpgsqlException: Connection refused
   at Npgsql.NpgsqlConnection.Open()
   at Microsoft.EntityFrameworkCore.Storage.RelationalConnection.OpenDbConnection()
   ...
```

---

## 🚀 Alternative : Script PowerShell

Si vous préférez la ligne de commande, utilisez `init-db.ps1` :

```powershell
# Appliquer les migrations
.\init-db.ps1

# Ou directement avec dotnet CLI
dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api
```

---

## 🔍 Comparaison : API vs CLI

| Critère | API Migrations | CLI (dotnet ef) |
|---------|----------------|-----------------|
| Automatisation CI/CD | ✅ Facile (HTTP) | ⚠️ Nécessite SDK |
| Déploiement sans SDK | ✅ Oui | ❌ Non |
| Sécurité Production | ✅ Désactivable | ✅ Pas d'exposition |
| Logs centralisés | ✅ Oui | ⚠️ Stdout uniquement |
| Rollback granulaire | ❌ Tout ou rien | ✅ Migration spécifique |
| Monitoring | ✅ HTTP 200/500 | ⚠️ Exit codes |

---

## 📚 Références

- [Entity Framework Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [ASP.NET Core Environments](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/environments)
- [Database.Migrate() documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.relationaldatabasefacadeextensions.migrate)

---

## ⚠️ Mises en garde

1. **Production** : Utilisez toujours `init-db.ps1` ou des migrations via CI/CD pipeline avec contrôles.
2. **Rollback** : `POST /down` supprime TOUT. Aucun rollback partiel disponible.
3. **Concurrence** : Ne pas exécuter `/up` en parallèle (risque de deadlock).
4. **Backup** : Toujours sauvegarder avant d'utiliser `/down`.
5. **Timeouts** : Les migrations longues peuvent timeout (augmenter request timeout Nginx).

---

## 🎯 Résumé

```
GET  /api/migrations/status → État actuel (safe)
POST /api/migrations/up     → Appliquer migrations (dev/staging)
POST /api/migrations/down   → DROP DATABASE (dev only, dangerous)
```

**En production** : Utilisez `init-db.ps1` ou des pipelines CI/CD sécurisés.
