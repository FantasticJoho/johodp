# 🏥 Health Checks de Johodp Identity Provider

## Vue d'ensemble

Johodp implémente trois types de health checks selon les standards Kubernetes pour permettre un monitoring et un déploiement robuste.

---

## 🎯 Endpoints disponibles

### 1. `/health/live` - Liveness Probe

**Question** : "L'application est-elle vivante ou morte ?"

**Usage** : 
- Kubernetes utilise cet endpoint pour décider s'il doit **redémarrer le pod**
- Monitoring pour détecter les blocages (deadlocks)

**Vérifications** :
- ✅ L'application répond aux requêtes HTTP
- ❌ Ne vérifie PAS la base de données (pour éviter les redémarrages en cascade)

**Réponse (200 OK)** :
```json
{
  "status": "Healthy",
  "timestamp": "2025-11-25T10:30:00Z",
  "description": "Application is alive"
}
```

**Utilisation Kubernetes** :
```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 5000
  initialDelaySeconds: 10
  periodSeconds: 30
  timeoutSeconds: 5
  failureThreshold: 3
```

**Comportement** :
- ✅ 200 = L'app est vivante → Ne pas redémarrer
- ❌ 503/500/timeout = L'app est bloquée → Redémarrer le pod

---

### 2. `/health/ready` - Readiness Probe

**Question** : "L'application est-elle prête à recevoir du trafic ?"

**Usage** :
- Kubernetes utilise cet endpoint pour décider s'il doit **envoyer du trafic au pod**
- Load balancers pour inclure/exclure l'instance

**Vérifications** :
- ✅ PostgreSQL est accessible
- ✅ IdentityServer est opérationnel
- ✅ Les migrations sont appliquées

**Réponse (200 OK)** :
```json
{
  "status": "Healthy",
  "timestamp": "2025-11-25T10:30:00Z",
  "duration": "00:00:00.0456789",
  "checks": [
    {
      "name": "postgresql",
      "status": "Healthy",
      "duration": "00:00:00.0234567",
      "description": "Database connection successful",
      "exception": null
    },
    {
      "name": "identityserver",
      "status": "Healthy",
      "duration": "00:00:00.0012345",
      "description": "IdentityServer is operational (issuer: https://idp.example.com)",
      "exception": null
    }
  ]
}
```

**Réponse (503 Service Unavailable)** :
```json
{
  "status": "Unhealthy",
  "timestamp": "2025-11-25T10:30:00Z",
  "duration": "00:00:05.0000000",
  "checks": [
    {
      "name": "postgresql",
      "status": "Unhealthy",
      "duration": "00:00:05.0000000",
      "description": null,
      "exception": "Npgsql.NpgsqlException: Connection refused"
    }
  ]
}
```

**Utilisation Kubernetes** :
```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 5000
  initialDelaySeconds: 15
  periodSeconds: 10
  timeoutSeconds: 5
  successThreshold: 1
  failureThreshold: 3
```

**Comportement** :
- ✅ 200 = Prêt à recevoir du trafic
- ❌ 503 = Pas prêt → Retirer du load balancer (sans redémarrer)

---

### 3. `/health` - General Health Check

**Question** : "Quel est l'état général de l'application ?"

**Usage** :
- Monitoring général (Prometheus, Grafana, Datadog)
- Load balancers classiques (AWS ELB, Nginx)
- Tableaux de bord opérationnels

**Vérifications** :
- ✅ Agrégation de tous les health checks
- ✅ Retourne la version de l'application

**Réponse (200 OK)** :
```json
{
  "status": "Healthy",
  "timestamp": "2025-11-25T10:30:00Z",
  "version": "1.0.0.0"
}
```

**Utilisation Nginx** :
```nginx
location = /health {
    proxy_pass http://johodp_backend;
    access_log off;
}
```

---

## 🛠️ Scénarios d'utilisation

### Scénario 1 : Démarrage de l'application

```
Étape 1 - Application démarre (0s)
  /health/live  → 200 ✅ (app répond)
  /health/ready → 503 ❌ (migrations en cours)
  → Kubernetes n'envoie PAS de trafic

Étape 2 - Migrations terminées (10s)
  /health/live  → 200 ✅
  /health/ready → 200 ✅ (DB OK, IdentityServer OK)
  → Kubernetes ENVOIE le trafic
```

### Scénario 2 : PostgreSQL crashe

```
  /health/live  → 200 ✅ (l'app tourne encore)
  /health/ready → 503 ❌ (DB inaccessible)
  
  → Kubernetes RETIRE du load balancer
  → Kubernetes NE REDÉMARRE PAS le pod
  → Les autres pods continuent de servir le trafic
```

### Scénario 3 : Deadlock dans l'application

```
  /health/live  → timeout/503 ❌ (app bloquée)
  /health/ready → timeout/503 ❌
  
  → Kubernetes REDÉMARRE le pod
```

### Scénario 4 : Montée en charge

```
Pod 1:
  /health/live  → 200 ✅
  /health/ready → 200 ✅
  → Reçoit 50% du trafic

Pod 2 (nouveau):
  /health/live  → 200 ✅
  /health/ready → 503 ❌ (chargement cache)
  → Ne reçoit PAS de trafic

Pod 2 après 30s:
  /health/live  → 200 ✅
  /health/ready → 200 ✅
  → Reçoit maintenant 50% du trafic
```

---

## 📊 Intégration Prometheus

Pour exposer les métriques de santé dans Prometheus :

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'johodp-idp'
    metrics_path: '/health'
    scrape_interval: 15s
    static_configs:
      - targets: ['idp.example.com:443']
    scheme: https
```

---

## 🔍 Composants vérifiés

### PostgreSQL Health Check
- **Classe** : `AspNetCore.HealthChecks.NpgSql`
- **Vérifie** : Connexion à la base de données
- **Tag** : `db`, `ready`
- **Timeout** : 5 secondes

### IdentityServer Health Check
- **Classe** : `Johodp.Api.HealthChecks.IdentityServerHealthCheck`
- **Vérifie** : Configuration de l'issuer URL
- **Tag** : `identityserver`, `ready`
- **Vérifie** : `IIssuerNameService.GetCurrentAsync()`

---

## 🚀 Testing local

### Tester /health/live
```bash
curl http://localhost:5000/health/live
```

**Résultat attendu** :
```json
{
  "status": "Healthy",
  "timestamp": "2025-11-25T10:30:00Z",
  "description": "Application is alive"
}
```

### Tester /health/ready
```bash
curl http://localhost:5000/health/ready
```

**Si DB OK** :
```json
{
  "status": "Healthy",
  "checks": [
    { "name": "postgresql", "status": "Healthy" },
    { "name": "identityserver", "status": "Healthy" }
  ]
}
```

**Si DB KO** :
```json
{
  "status": "Unhealthy",
  "checks": [
    {
      "name": "postgresql",
      "status": "Unhealthy",
      "exception": "Connection refused"
    }
  ]
}
```

### Tester /health
```bash
curl http://localhost:5000/health
```

---

## ⚙️ Configuration

### Dépendances NuGet

```xml
<PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="8.0.2" />
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="8.0.0" />
```

### Installation
```bash
dotnet add package AspNetCore.HealthChecks.NpgSql
```

### Configuration dans Program.cs

```csharp
// Enregistrement des health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgresql",
        tags: new[] { "db", "ready" })
    .AddCheck<IdentityServerHealthCheck>(
        "identityserver",
        tags: new[] { "identityserver", "ready" });

// Mapping des endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // Aucune vérification
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready") // DB + IdentityServer
});

app.MapHealthChecks("/health", new HealthCheckOptions
{
    // Tous les checks
});
```

---

## 🐳 Exemple Docker Compose

```yaml
services:
  johodp-idp:
    image: johodp-idp:latest
    ports:
      - "5000:5000"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health/live"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 40s
    depends_on:
      postgres:
        condition: service_healthy
  
  postgres:
    image: postgres:16
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5
```

---

## 📚 Références

- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Kubernetes Liveness, Readiness and Startup Probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
- [AspNetCore.Diagnostics.HealthChecks (GitHub)](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks)
