# 🔧 Guide de dépannage et FAQ

## Installation et démarrage

### ❌ "dotnet: command not found"

**Cause** : .NET SDK n'est pas installé ou pas dans le PATH

**Solution** :
1. Télécharger .NET 8.0 SDK depuis https://dotnet.microsoft.com/download
2. Installer le SDK
3. Vérifier l'installation :
```bash
dotnet --version
```

---

### ❌ "Could not connect to the database"

**Cause** : PostgreSQL n'est pas en cours d'exécution

**Solution** :

**Option 1 - Avec Docker Compose** (recommandé)
```bash
docker-compose up -d
# Attendre 10 secondes que PostgreSQL démarre
```

**Option 2 - Docker directement**
```bash
docker run -d \
  --name johodp-postgres \
  -e POSTGRES_PASSWORD=password \
  -e POSTGRES_DB=johodp \
  -p 5432:5432 \
  postgres:15
```

**Option 3 - PostgreSQL localement**
- Installer PostgreSQL depuis https://www.postgresql.org/download/
- Créer une base de données : `johodp`
- Vérifier la connection string dans `appsettings.json`

**Option 4 - Tester la connexion**
```bash
# Avec psql
psql -h localhost -U postgres -d johodp -c "SELECT 1"

# Avec Docker
docker-compose exec postgres psql -U postgres -d johodp -c "SELECT 1"
```

---

### ❌ "Host localhost:5432 refused"

**Cause** : PostgreSQL démarre mais pas encore prêt

**Solution** :
```bash
# Docker Compose - Attendre l'healthcheck
docker-compose up -d
docker-compose logs -f postgres

# Attendre le message:
# database system is ready to accept connections
```

---

## Migrations Entity Framework

### ❌ "The 'JohodpDbContext' entity type couldn't be mapped"

**Cause** : Configuration Entity Framework incomplète

**Solution** :
```bash
# Régénérer les migrations
dotnet ef migrations remove --project src/Johodp.Infrastructure --startup-project src/Johodp.Api

# Recréer
dotnet ef migrations add InitialCreate --project src/Johodp.Infrastructure --startup-project src/Johodp.Api

# Appliquer
dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api
```

---

### ❌ "Unable to create an object of type 'JohodpDbContext'"

**Cause** : Service n'est pas enregistré ou connection string absente

**Solution** :
1. Vérifier `ServiceCollectionExtensions.cs` enregistre le DbContext
2. Vérifier `appsettings.json` a la connection string `DefaultConnection`
3. Vérifier PostgreSQL est en cours d'exécution

---

### ❌ "Keyword not recognized: 'host'"

**Cause** : Connection string mal formatée ou pour une base de données différente

**Solution** : Connection string correct pour PostgreSQL
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=johodp;Username=postgres;Password=password"
  }
}
```

---

## API et Contrôleurs

### ❌ "The type or namespace name 'Application' does not exist"

**Cause** : Références de projet manquantes dans les .csproj

**Solution** :
```bash
# Vérifier que Johodp.Api référence les autres projets
# Dans src/Johodp.Api/Johodp.Api.csproj:

<ItemGroup>
  <ProjectReference Include="..\Johodp.Domain\Johodp.Domain.csproj" />
  <ProjectReference Include="..\Johodp.Application\Johodp.Application.csproj" />
  <ProjectReference Include="..\Johodp.Infrastructure\Johodp.Infrastructure.csproj" />
</ItemGroup>
```

---

### ❌ "error CS0103: The name 'ValueObject' does not exist"

**Cause** : Import manquant dans un fichier

**Solution** :
Ajouter au début du fichier :
```csharp
using Johodp.Domain.Common;
```

---

### ❌ "swagger" endpoint not found (404)

**Cause** : Swagger n'est pas enregistré

**Solution** :
Vérifier `Program.cs` contient :
```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

---

## Tests

### ❌ "Test project failed to load"

**Cause** : Références de projet ou dépendances manquantes

**Solution** :
```bash
# Restaurer les packages
dotnet restore tests/Johodp.Tests/

# Reconstruire
dotnet build tests/Johodp.Tests/

# Relancer les tests
dotnet test tests/Johodp.Tests/
```

---

### ❌ "Unable to find test adapter"

**Cause** : xUnit adapter manquant

**Solution** :
Réinstaller les packages de test :
```bash
dotnet add tests/Johodp.Tests/ package xunit.runner.visualstudio
dotnet add tests/Johodp.Tests/ package Microsoft.NET.Test.Sdk
```

---

## Docker et Docker Compose

### ❌ "docker: command not found"

**Cause** : Docker n'est pas installé

**Solution** :
1. Installer Docker Desktop https://www.docker.com/products/docker-desktop
2. Vérifier :
```bash
docker --version
docker-compose --version
```

---

### ❌ "port 5432 is already allocated"

**Cause** : Une autre instance PostgreSQL utilise le port

**Solution** :
```bash
# Option 1 - Changer le port dans docker-compose.yml
# Modifier "5432:5432" en "5433:5432"

# Option 2 - Tuer le conteneur existant
docker stop johodp-postgres
docker rm johodp-postgres
docker-compose up -d
```

---

### ❌ "Cannot connect to the Docker daemon"

**Cause** : Le daemon Docker ne fonctionne pas

**Solution** :
```bash
# Sur Windows
# Ouvrir Docker Desktop

# Sur Linux
sudo systemctl start docker

# Vérifier
docker ps
```

---

## Performance et optimisation

### API lente

**Cause possible** : Pas d'index sur la base de données

**Solution** :
```sql
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_clients_name ON clients(client_name);
```

---

### Consommation mémoire élevée

**Cause** : Caching ou pooling de connexion mal configuré

**Solution** :
Vérifier dans `ServiceCollectionExtensions.cs` :
```csharp
// Augmenter le pool de connexions
services.AddDbContext<JohodpDbContext>(options =>
    options.UseNpgsql(connectionString,
        npgsqlOptions => 
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
        }));
```

---

## Sécurité

### ⚠️ Connection string en dur dans le code

**Problème** : Credentials exposées

**Solution** :
```bash
# Utiliser User Secrets en développement
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;..."

# Utiliser des variables d'environnement
$env:ConnectionStrings__DefaultConnection = "Host=localhost;..."
```

---

### ⚠️ Pas d'authentification sur les endpoints

**Status** : À implémenter avec IdentityServer

**Solution** : Ajouter l'attribut `[Authorize]`
```csharp
[Authorize]
[HttpGet("{userId}")]
public async Task<ActionResult<UserDto>> GetUser(Guid userId)
{
    // ...
}
```

---

## Commandes utiles

### Restaurer tous les packages
```bash
dotnet restore
```

### Nettoyer la solution
```bash
dotnet clean
dotnet build
```

### Compiler en Release
```bash
dotnet build -c Release
```

### Publier pour déploiement
```bash
dotnet publish -c Release -o ./publish
```

### Vérifier les violations de style
```bash
dotnet format --verify-no-changes --verbosity diagnostic
```

### Exécuter les tests avec couverture
```bash
dotnet test /p:CollectCoverage=true
```

### Afficher les logs PostgreSQL
```bash
docker-compose logs postgres
```

### Accéder à PgAdmin
```
http://localhost:5050
Email: admin@example.com
Password: admin
```

---

## Ressources d'aide

- 📚 [Documentation .NET 8](https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- 📚 [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- 📚 [PostgreSQL Documentation](https://www.postgresql.org/docs/15/index.html)
- 📚 [Docker Documentation](https://docs.docker.com/)
- 💬 [Stack Overflow](https://stackoverflow.com/questions/tagged/dotnet)
- 💬 [GitHub Issues](https://github.com/search?q=label:help)

---

## Signaler un bug

1. Vérifier si le bug existe déjà
2. Créer une issue GitHub avec:
   - Description du problème
   - Logs d'erreur complets
   - Étapes pour reproduire
   - Environnement (OS, version .NET, etc.)
   - Solution tentée

---

## Support supplémentaire

Pour toute question, consulter:
- `README.md` - Vue d'ensemble
- `QUICKSTART.md` - Démarrage rapide
- `ARCHITECTURE.md` - Architecture technique
- `API_ENDPOINTS.md` - Endpoints disponibles
