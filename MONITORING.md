# 📊 Plan de Monitoring - Johodp Identity Provider

## 🎯 État Actuel (Déjà Implémenté)

### ✅ Health Checks (LOT 1 - Production Ready)

**Endpoints Disponibles:**
- `GET /health` - Status général complet (JSON)
- `GET /health/live` - Liveness probe Kubernetes
- `GET /health/ready` - Readiness probe Kubernetes

**Checks Implémentés:**
1. **PostgreSQL** - Connexion base de données
2. **IdentityServer** - Service IdentityServer opérationnel

**Utilisation Actuelle:**
```bash
# Test health checks
curl http://localhost:5000/health
curl http://localhost:5000/health/live
curl http://localhost:5000/health/ready
```

**Configuration Kubernetes (Prêt à déployer):**
```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 5000
  initialDelaySeconds: 10
  periodSeconds: 30
  failureThreshold: 3

readinessProbe:
  httpGet:
    path: /health/ready
    port: 5000
  initialDelaySeconds: 5
  periodSeconds: 10
  failureThreshold: 3
```

**Documentation:** `HEALTH_CHECKS.md` (380 lignes complètes)

---

## 🚀 À Implémenter (LOT 3 - Monitoring Avancé)

### ⭐ Stratégie Retenue: Logs Console → ELK (12-Factor App)

**Architecture Choisie:**
- ✅ **Application logge UNIQUEMENT en console** (stdout/stderr)
- ✅ **Pas de sink Elasticsearch dans l'app** (séparation des responsabilités)
- ✅ **Log shipper externe** envoie logs vers ELK (Filebeat, Fluentd, Docker logging driver)
- ✅ **12-Factor App compliant** (logs as event streams)

**Avantages:**
- ✅ Application stateless (ne connaît pas Elasticsearch)
- ✅ Résilience (app ne plante pas si Elasticsearch down)
- ✅ Flexibilité (changer ELK pour Datadog sans toucher code)
- ✅ Performance (pas de I/O réseau dans l'app)
- ✅ Kubernetes-native (logs stdout/stderr automatiquement collectés)

**Options Log Shipping:**

| Solution | Use Case | Complexité |
|----------|----------|------------|
| **Docker Logging Driver** | Docker Compose simple | Très simple ⭐ |
| **Filebeat** | Kubernetes production | Simple |
| **Fluentd** | Multi-sources, transformations | Moyen |
| **Logstash** | Parsing complexe, filtres | Élevé |
| **AWS CloudWatch → ELK** | AWS ECS/Fargate | Simple (AWS natif) |

**Recommandation Johodp:** **Filebeat** (standard industrie, léger, fiable)

---

### 1. Logs Structurés comme Métriques

#### Objectifs
- Logger tous les événements métier avec metadata
- Utiliser Kibana pour visualiser métriques
- Alerting Elasticsearch Watcher
- Retention 90 jours

#### Package Déjà Disponible ✅
```xml
<!-- src/Johodp.Api/Johodp.Api.csproj -->
<!-- Serilog CONSOLE uniquement (pas de sink Elasticsearch) -->
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="2.3.0" />
<PackageReference Include="Serilog.Enrichers.Thread" Version="3.1.0" />
<PackageReference Include="Serilog.Exceptions" Version="8.4.0" />
```

**❌ PAS BESOIN de:**
```xml
<!-- NE PAS ajouter - Le shipping est externe -->
<PackageReference Include="Serilog.Sinks.Elasticsearch" Version="9.0.3" />
```

#### Métriques via Logs Structurés

**Principe:** Chaque événement métier est loggué avec metadata structurée. Kibana agrège et visualise.

**A. Métriques Business (Logs Enrichis)**

**1. Logins (Compteur + Latence)**
```csharp
// AccountController.cs - Login
_logger.LogInformation(
    "User login {LoginStatus} for {Email} in tenant {TenantName}, MFA={MfaUsed}, Duration={DurationMs}ms, IP={IpAddress}",
    loginSuccess ? "Success" : "Failed",
    email,
    tenantName,
    mfaUsed,
    stopwatch.ElapsedMilliseconds,
    HttpContext.Connection.RemoteIpAddress?.ToString()
);
```

**Kibana Query:**
```
# Compte logins success
message:"User login" AND LoginStatus:"Success"

# P95 latency
Percentiles(DurationMs, 95) WHERE message:"User login"

# Taux échec par tenant
(LoginStatus:"Failed" / LoginStatus:*) GROUP BY TenantName
```

**2. Registrations (Compteur)**
```csharp
// UsersController.cs - Register
_logger.LogInformation(
    "User registration {RegistrationStatus} for {Email} in tenant {TenantName}, Source={Source}",
    "PendingActivation",
    email,
    tenantName,
    "API"
);
```

**3. MFA Enrollments (Compteur + Taux Succès)**
```csharp
// AccountController.cs - MFA Enroll
_logger.LogInformation(
    "MFA enrollment {EnrollmentStatus} for user {UserId} in tenant {TenantName}, Method={Method}",
    success ? "Success" : "Failed",
    userId,
    tenantName,
    "TOTP"
);
```

**4. OAuth2 Tokens (Compteur)**
```csharp
// CustomTokenService.cs (si implémenté)
_logger.LogInformation(
    "OAuth token issued for client {ClientId}, GrantType={GrantType}, Scopes={Scopes}, Duration={DurationMs}ms",
    clientId,
    grantType,
    string.Join(",", scopes),
    stopwatch.ElapsedMilliseconds
);
```

**5. Webhooks (Compteur + Latence)**
```csharp
// WebhookService.cs
_logger.LogInformation(
    "Webhook sent to {WebhookUrl} for tenant {TenantName}, Status={Status}, Duration={DurationMs}ms, ResponseCode={HttpStatus}",
    webhookUrl,
    tenantName,
    success ? "Success" : "Failed",
    stopwatch.ElapsedMilliseconds,
    httpStatusCode
);
```

**B. Métriques Infrastructure (ASP.NET Core)**

**Request Logging (Middleware déjà configuré):**
```csharp
// Serilog RequestLogging déjà actif
_logger.LogInformation(
    "HTTP {Method} {Path} responded {StatusCode} in {Elapsed}ms",
    method,
    path,
    statusCode,
    elapsed
);
```

**C. Métriques PostgreSQL**

**Connection Pool (périodique via Background Service):**
```csharp
// MetricsBackgroundService.cs (à créer)
_logger.LogInformation(
    "Database pool stats: Open={OpenConnections}, Idle={IdleConnections}, Waiting={WaitingRequests}",
    pool.OpenConnections,
    pool.IdleConnections,
    pool.WaitingRequests
);
```

**D. Métriques État (Gauges)**

**Active Users Count:**
```csharp
// MetricsBackgroundService.cs - Toutes les 5 minutes
_logger.LogInformation(
    "Active users count: {ActiveUsers} for tenant {TenantName}",
    activeUsersCount,
    tenantName
);
```

**Pending Activations:**
```csharp
_logger.LogInformation(
    "Pending activations: {PendingCount} for tenant {TenantName}",
    pendingCount,
    tenantName
);
```

#### Configuration Serilog (Console JSON)

**appsettings.Production.json:**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      }
    ],
    "Enrich": [
      "FromLogContext",
      "WithMachineName",
      "WithThreadId",
      "WithEnvironmentName",
      "WithExceptionDetails"
    ],
    "Properties": {
      "Application": "Johodp.Api",
      "Environment": "Production"
    }
  }
}
```

**Package CompactJsonFormatter:**
```bash
dotnet add src/Johodp.Api package Serilog.Formatting.Compact
```

**Output Console (JSON structuré):**
```json
{
  "@t": "2024-12-03T10:30:45.1234567Z",
  "@l": "Information",
  "@m": "User login Success for john@acme.com in tenant acme-corp, MFA=true, Duration=156ms, IP=192.168.1.1",
  "LoginStatus": "Success",
  "Email": "john@acme.com",
  "TenantName": "acme-corp",
  "MfaUsed": true,
  "DurationMs": 156,
  "IpAddress": "192.168.1.1",
  "Application": "Johodp.Api",
  "Environment": "Production",
  "MachineName": "johodp-api-pod-abc123"
}
```

**Avantages Format JSON:**
- ✅ Parsé automatiquement par Filebeat/Fluentd
- ✅ Tous les champs indexés dans Elasticsearch
- ✅ Pas de regex parsing (performance)
- ✅ Type-safe (nombres, booléens, dates)

**Program.cs (déjà configuré):**
```csharp
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Johodp.Api")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName);
});
```

**Estimation:** 2 SP (~1 jour) - Configuration JSON formatter uniquement

---

### 2. Dashboards Kibana (Remplace Grafana)

#### Objectifs
- Visualiser métriques en temps réel
- Dashboards par domaine (Business, Performance, Security)
- Alerting intégré (Elasticsearch Watcher)

#### Dashboard Principal "Johodp Overview"

**Canvas Kibana avec:**

**Section 1 - KPIs Business**
- **Total Users** (Unique count `UserId`, filtré par `TenantName`)
- **Logins Last 24h** (Count `message:"User login"`, split by `LoginStatus`)
- **MFA Enrollments** (Count `message:"MFA enrollment"`, split by `EnrollmentStatus`)
- **Pending Activations** (Last value `PendingCount` par tenant)

**Kibana Query:**
```
# Total logins success today
message:"User login" AND LoginStatus:"Success" AND @timestamp:[now-24h TO now]

# Group by tenant
Aggregation: Terms on TenantName.keyword
```

**Section 2 - Performance (Latence)**
- **Login P50/P95/P99** (Percentiles aggregation sur `DurationMs`)
  ```
  message:"User login" 
  Aggregation: Percentiles(DurationMs, 50, 95, 99)
  Split by: TenantName.keyword
  ```

- **Request Rate** (Count par minute)
  ```
  message:"HTTP"
  Date Histogram: 1 minute interval
  ```

- **Error Rate** (% de StatusCode >= 400)
  ```
  StatusCode:[400 TO 599] / StatusCode:*
  ```

- **Database Query Duration**
  ```
  message:*query* OR SourceContext:*Repository*
  Percentiles(DurationMs, 95)
  ```

**Section 3 - Infrastructure**
- **Memory/CPU** (si logs système activés)
- **GC Collections** (si métriques .NET loggées)
- **Active Connections PostgreSQL**

**Section 4 - OAuth2**
- **Tokens Issued** (Count `message:"OAuth token issued"`)
  ```
  message:"OAuth token issued"
  Split by: GrantType.keyword
  Pie chart
  ```

- **Token Generation Latency**
  ```
  message:"OAuth token issued"
  Percentiles(DurationMs, 95)
  ```

**Section 5 - Security**
- **Failed Login Attempts** (Map par IP)
  ```
  message:"User login" AND LoginStatus:"Failed"
  Aggregation: Terms on IpAddress.keyword
  Top 10 IPs
  ```

- **Suspicious Activities** (trop de tentatives)
  ```
  message:"User login" AND LoginStatus:"Failed"
  Filter: Count > 5 in 5 minutes
  ```

**Template Variables:**
- `TenantName` - Filtrer par tenant
- `Environment` - Dev/Staging/Prod
- Time range picker

**Estimation:** 5 SP (~2-3 jours) - Plus rapide que Grafana car requêtes Kibana natives

---

### 3. Alerting avec Elasticsearch Watcher

#### Objectifs
- Alertes temps réel sur métriques critiques
- Notifications multi-canaux (Email, Slack, PagerDuty)
- Corrélation avec logs contextuels

#### Alertes Critiques

**1. Taux d'Erreur Login Élevé**
```json
{
  "trigger": {
    "schedule": { "interval": "5m" }
  },
  "input": {
    "search": {
      "request": {
        "indices": ["johodp-logs-*"],
        "body": {
          "query": {
            "bool": {
              "must": [
                { "match": { "message": "User login" }},
                { "match": { "LoginStatus": "Failed" }},
                { "range": { "@timestamp": { "gte": "now-5m" }}}
              ]
            }
          },
          "aggs": {
            "total_logins": {
              "filter": { "match": { "message": "User login" }}
            },
            "failed_logins": {
              "filter": { 
                "bool": {
                  "must": [
                    { "match": { "message": "User login" }},
                    { "match": { "LoginStatus": "Failed" }}
                  ]
                }
              }
            }
          }
        }
      }
    }
  },
  "condition": {
    "script": {
      "source": "ctx.payload.aggregations.failed_logins.doc_count / ctx.payload.aggregations.total_logins.doc_count > 0.1"
    }
  },
  "actions": {
    "notify_slack": {
      "webhook": {
        "method": "POST",
        "url": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL",
        "body": "🚨 High login failure rate: {{ctx.payload.aggregations.failed_logins.doc_count}} / {{ctx.payload.aggregations.total_logins.doc_count}} (>10%) in last 5 minutes"
      }
    }
  }
}
```

**2. Latence Login Élevée (P95 > 2s)**
```json
{
  "trigger": { "schedule": { "interval": "5m" }},
  "input": {
    "search": {
      "request": {
        "indices": ["johodp-logs-*"],
        "body": {
          "query": {
            "bool": {
              "must": [
                { "match": { "message": "User login" }},
                { "range": { "@timestamp": { "gte": "now-5m" }}}
              ]
            }
          },
          "aggs": {
            "latency_percentiles": {
              "percentiles": {
                "field": "DurationMs",
                "percents": [95]
              }
            }
          }
        }
      }
    }
  },
  "condition": {
    "script": {
      "source": "ctx.payload.aggregations.latency_percentiles.values['95.0'] > 2000"
    }
  },
  "actions": {
    "notify_email": {
      "email": {
        "to": "ops@example.com",
        "subject": "Johodp: High login latency detected",
        "body": "P95 login latency: {{ctx.payload.aggregations.latency_percentiles.values['95.0']}}ms (threshold: 2000ms)"
      }
    }
  }
}
```

**3. PostgreSQL Down (via Health Check logs)**
```json
{
  "trigger": { "schedule": { "interval": "1m" }},
  "input": {
    "search": {
      "request": {
        "indices": ["johodp-logs-*"],
        "body": {
          "query": {
            "bool": {
              "must": [
                { "match": { "message": "postgresql" }},
                { "match": { "Status": "Unhealthy" }},
                { "range": { "@timestamp": { "gte": "now-2m" }}}
              ]
            }
          }
        }
      }
    }
  },
  "condition": {
    "compare": { "ctx.payload.hits.total": { "gt": 0 }}
  },
  "actions": {
    "notify_pagerduty": {
      "pagerduty": {
        "description": "🔴 CRITICAL: PostgreSQL database is DOWN",
        "event_type": "trigger",
        "incident_key": "johodp-postgres-down"
      }
    }
  }
}
```

**4. Webhook Timeouts Élevés**
```json
{
  "trigger": { "schedule": { "interval": "10m" }},
  "input": {
    "search": {
      "request": {
        "indices": ["johodp-logs-*"],
        "body": {
          "query": {
            "bool": {
              "must": [
                { "match": { "message": "Webhook sent" }},
                { "match": { "Status": "Failed" }},
                { "range": { "@timestamp": { "gte": "now-10m" }}}
              ]
            }
          },
          "aggs": {
            "by_tenant": {
              "terms": { "field": "TenantName.keyword" }
            }
          }
        }
      }
    }
  },
  "condition": {
    "script": {
      "source": "ctx.payload.hits.total > 5"
    }
  },
  "actions": {
    "notify_slack": {
      "webhook": {
        "method": "POST",
        "url": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL",
        "body": "⚠️ Multiple webhook failures detected: {{ctx.payload.hits.total}} failures in 10 minutes"
      }
    }
  }
}
```

**Canaux Notification:**
- **Slack** - Alertes non critiques + résumé quotidien
- **Email** - Alertes importantes (latence, error rate)
- **PagerDuty** - Incidents critiques (DB down, app crash)
- **Teams** - Alertes métier (webhooks, activations)

**Estimation:** 5 SP (~2-3 jours)

---

### 4. Background Service pour Métriques État (Gauges)

Certaines métriques ne peuvent pas être dérivées des logs d'événements (ex: nombre d'utilisateurs actifs). Un Background Service les logge périodiquement.

**MetricsBackgroundService.cs (à créer):**
```csharp
namespace Johodp.Api.Services;

public class MetricsBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricsBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public MetricsBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<MetricsBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await LogMetricsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging metrics");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task LogMetricsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JohodpDbContext>();

        // Active users per tenant (last 24h)
        var activeUsersByTenant = await dbContext.Users
            .Where(u => u.LastLoginAt >= DateTime.UtcNow.AddHours(-24))
            .GroupBy(u => u.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync();

        foreach (var stat in activeUsersByTenant)
        {
            _logger.LogInformation(
                "Active users (24h): {ActiveUsers} for tenant {TenantId}",
                stat.Count,
                stat.TenantId
            );
        }

        // Pending activations per tenant
        var pendingActivations = await dbContext.Users
            .Where(u => u.Status == UserStatus.PendingActivation)
            .GroupBy(u => u.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync();

        foreach (var stat in pendingActivations)
        {
            _logger.LogInformation(
                "Pending activations: {PendingCount} for tenant {TenantId}",
                stat.Count,
                stat.TenantId
            );
        }

        // Database pool stats (si Npgsql expose ces metrics)
        _logger.LogInformation(
            "Database connection pool: Open={OpenConnections}, Idle={IdleConnections}",
            NpgsqlConnection.PoolManager.Pools.Sum(p => p.OpenConnections),
            NpgsqlConnection.PoolManager.Pools.Sum(p => p.IdleConnections)
        );
    }
}
```

**Program.cs (enregistrer service):**
```csharp
builder.Services.AddHostedService<MetricsBackgroundService>();
```

**Kibana Visualization (Gauges):**
```
# Active users trend
message:"Active users"
Metric: Last value of ActiveUsers
Group by: TenantId.keyword
Time series chart
```

**Estimation:** 3 SP (~1-2 jours)

---

### 5. Log Shipping vers ELK

#### Option 1: Filebeat (Recommandé - Production Kubernetes)

**Pourquoi Filebeat:**
- ✅ Léger (~50MB RAM)
- ✅ Officiel Elastic
- ✅ Rétries automatiques
- ✅ Backpressure handling
- ✅ Kubernetes DaemonSet natif

**Configuration Filebeat:**

**filebeat.yml:**
```yaml
filebeat.inputs:
  - type: container
    enabled: true
    paths:
      - /var/lib/docker/containers/*/*.log
    
    # Parser les logs JSON Serilog
    json.keys_under_root: true
    json.add_error_key: true
    json.message_key: "@m"
    
    # Filtrer uniquement Johodp API
    include_lines: ['"Application":"Johodp.Api"']

processors:
  - add_kubernetes_metadata:
      host: ${NODE_NAME}
      matchers:
        - logs_path:
            logs_path: "/var/lib/docker/containers/"
  
  - decode_json_fields:
      fields: ["message"]
      target: ""
      overwrite_keys: true

output.elasticsearch:
  hosts: ["https://elasticsearch:9200"]
  index: "johodp-logs-%{+yyyy.MM}"
  
  # Authentication (si sécurisé)
  username: "elastic"
  password: "${ELASTICSEARCH_PASSWORD}"

setup.ilm.enabled: false
setup.template.name: "johodp-logs"
setup.template.pattern: "johodp-logs-*"
```

**Kubernetes DaemonSet:**
```yaml
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: filebeat
  namespace: logging
spec:
  selector:
    matchLabels:
      app: filebeat
  template:
    metadata:
      labels:
        app: filebeat
    spec:
      serviceAccountName: filebeat
      containers:
      - name: filebeat
        image: docker.elastic.co/beats/filebeat:8.11.0
        args: ["-c", "/etc/filebeat.yml", "-e"]
        env:
        - name: ELASTICSEARCH_HOST
          value: elasticsearch
        - name: ELASTICSEARCH_PORT
          value: "9200"
        - name: NODE_NAME
          valueFrom:
            fieldRef:
              fieldPath: spec.nodeName
        volumeMounts:
        - name: config
          mountPath: /etc/filebeat.yml
          readOnly: true
          subPath: filebeat.yml
        - name: data
          mountPath: /usr/share/filebeat/data
        - name: varlibdockercontainers
          mountPath: /var/lib/docker/containers
          readOnly: true
        - name: varlog
          mountPath: /var/log
          readOnly: true
      volumes:
      - name: config
        configMap:
          name: filebeat-config
      - name: varlibdockercontainers
        hostPath:
          path: /var/lib/docker/containers
      - name: varlog
        hostPath:
          path: /var/log
      - name: data
        hostPath:
          path: /var/lib/filebeat-data
          type: DirectoryOrCreate
```

**Estimation:** 3 SP (~2 jours)

---

#### Option 2: Fluentd (Alternative - Plus Flexible)

**Pourquoi Fluentd:**
- ✅ Multi-destinations (ELK, Datadog, S3)
- ✅ Transformations avancées
- ✅ Plugins riches
- ⚠️ Plus lourd (~200MB RAM)

**fluentd.conf:**
```conf
<source>
  @type tail
  path /var/log/containers/johodp-api-*.log
  pos_file /var/log/fluentd-johodp.pos
  tag johodp.api
  
  <parse>
    @type json
    time_key @t
    time_format %Y-%m-%dT%H:%M:%S.%L%z
  </parse>
</source>

<filter johodp.api>
  @type record_transformer
  <record>
    kubernetes_namespace ${record["kubernetes"]["namespace_name"]}
    kubernetes_pod ${record["kubernetes"]["pod_name"]}
  </record>
</filter>

<match johodp.api>
  @type elasticsearch
  host elasticsearch
  port 9200
  logstash_format true
  logstash_prefix johodp-logs
  
  <buffer>
    @type file
    path /var/log/fluentd-buffers/johodp.buffer
    flush_mode interval
    flush_interval 5s
    retry_type exponential_backoff
  </buffer>
</match>
```

**Estimation:** 3 SP (~2 jours)

---

#### Option 3: Docker Logging Driver (Simple - Dev/Staging)

**Pourquoi Docker Driver:**
- ✅ Très simple (configuration Docker seulement)
- ✅ Pas de process externe
- ⚠️ Moins flexible
- ⚠️ Pas de buffer (perte logs si Elasticsearch down)

**docker-compose.yml:**
```yaml
version: '3.8'

services:
  johodp-api:
    image: johodp/api:latest
    logging:
      driver: "gelf"
      options:
        gelf-address: "udp://localhost:12201"
        tag: "johodp-api"
```

**Logstash GELF Input:**
```conf
input {
  gelf {
    port => 12201
  }
}

filter {
  json {
    source => "message"
  }
  
  mutate {
    add_field => { "[@metadata][index]" => "johodp-logs-%{+YYYY.MM}" }
  }
}

output {
  elasticsearch {
    hosts => ["elasticsearch:9200"]
    index => "%{[@metadata][index]}"
  }
}
```

**Estimation:** 2 SP (~1 jour)

---

#### Comparaison Options

| Aspect | Filebeat | Fluentd | Docker Driver |
|--------|----------|---------|---------------|
| **Complexité** | Simple | Moyenne | Très simple |
| **RAM** | 50MB | 200MB | 0 (intégré) |
| **Résilience** | ✅ Excellent | ✅ Excellent | ⚠️ Faible |
| **Buffer** | ✅ Disk | ✅ Disk | ❌ None |
| **Retry** | ✅ Auto | ✅ Auto | ⚠️ Limité |
| **Multi-dest** | ⚠️ Limité | ✅ Excellent | ❌ None |
| **Kubernetes** | ✅ DaemonSet | ✅ DaemonSet | ❌ N/A |
| **Use Case** | Production | Multi-cloud | Dev/Staging |

**Recommandation:**
- **Production Kubernetes:** Filebeat
- **Multi-destinations:** Fluentd
- **Dev/Docker Compose:** Docker Logging Driver

---
```yaml
version: '3.8'

services:
  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
    container_name: johodp-elasticsearch
    environment:
      - discovery.type=single-node
      - "ES_JAVA_OPTS=-Xms512m -Xmx512m"
      - xpack.security.enabled=false
    ports:
      - "9200:9200"
    volumes:
      - elasticsearch-data:/usr/share/elasticsearch/data
    networks:
      - johodp-network

  kibana:
    image: docker.elastic.co/kibana/kibana:8.11.0
    container_name: johodp-kibana
    environment:
      - ELASTICSEARCH_HOSTS=http://elasticsearch:9200
    ports:
      - "5601:5601"
    depends_on:
      - elasticsearch
    networks:
      - johodp-network

  # Optionnel: Logstash pour filtrage/transformation
  logstash:
    image: docker.elastic.co/logstash/logstash:8.11.0
    container_name: johodp-logstash
    volumes:
      - ./logstash/pipeline:/usr/share/logstash/pipeline
    ports:
      - "5044:5044"
    depends_on:
      - elasticsearch
    networks:
      - johodp-network

volumes:
  elasticsearch-data:
    driver: local

networks:
  johodp-network:
    external: true
```

**Démarrage:**
```bash
docker-compose -f docker-compose.monitoring.yml up -d

# Accès Kibana
open http://localhost:5601
```

**Estimation:** 2 SP (~1 jour)

---

## 📊 Roadmap Monitoring ELK-Only (Recommandé)

### Phase 1 - Logs Console JSON + Filebeat + ELK (LOT 2) - 12 SP
**Priorité:** P1 - Avant mise en production

1. ✅ **Health Checks** (DÉJÀ FAIT)

2. **Serilog JSON Console Output** (2 SP)
   - Package Serilog.Formatting.Compact
   - Configuration appsettings.Production.json
   - Test logs JSON console
   
3. **Logs Enrichis Business** (5 SP)
   - Login avec latence + metadata
   - Registration + activation
   - MFA enrollment/verification
   - OAuth tokens
   - Webhooks
   
4. **Background Service Metrics** (2 SP)
   - Active users count
   - Pending activations
   
5. **ELK Stack + Filebeat** (3 SP)
   - Docker Compose avec Elasticsearch + Kibana + Filebeat
   - Configuration filebeat.yml
   - Test ingestion logs
   
**Livrables:**
- Logs JSON console structurés
- Filebeat shipping vers Elasticsearch
- Index `johodp-logs-*` dans Elasticsearch
- Kibana prêt pour dashboards

**Coût Infrastructure:**
- Self-hosted Docker: **$0**
- AWS OpenSearch + Filebeat sur EC2: **~$100/mois**
- Elastic Cloud (géré): **~$95/mois**

---

### Phase 2 - Dashboards Avancés + Alerting (LOT 3) - 10 SP
**Priorité:** P2 - 1 mois après prod

1. **Dashboards Kibana Avancés** (5 SP)
   - Security dashboard (failed logins map, suspicious IPs)
   - OAuth2 analytics (clients, grant types)
   - Business analytics (registrations funnel, activation rate)
   
2. **Elasticsearch Watcher Alerting** (5 SP)
   - Taux erreur login > 10% → Slack
   - Latence P95 > 2s → Email
   - PostgreSQL down → PagerDuty
   - Webhook timeouts → Slack
   - MFA failures → Email

**Livrables:**
- 5 dashboards Kibana thématiques
- 10+ alertes Elasticsearch Watcher
- Intégration Slack/PagerDuty/Email

---

### Phase 3 - APM et Optimisation (LOT 3+) - 8 SP
**Priorité:** P3 - Optimisation continue

1. **Elastic APM** (5 SP)
   - Distributed tracing automatique
   - Dependency map
   - Transaction profiling
   
2. **Capacity Planning** (3 SP)
   - Trending analysis (croissance trafic)
   - Index lifecycle management (retention)
   - Performance baseline

**Livrables:**
- APM dashboard avec traces
- Reports capacité mensuel
- Optimisations basées données

---

## 🎯 Prochaine Action Immédiate (Quick Start)

### Étape 1 - Configurer Logs JSON Console (30 minutes)

**1. Installer package:**
```bash
dotnet add src/Johodp.Api package Serilog.Formatting.Compact
```

**2. Modifier appsettings.Production.json:**
```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      }
    ]
  }
}
```

**3. Tester:**
```bash
# Build & Run
dotnet run --project src/Johodp.Api --environment Production

# Logs doivent être en JSON
# Output attendu:
# {"@t":"2024-12-03T10:30:45.123Z","@l":"Information","@m":"Application started"}
```

---

### Étape 2 - Démarrer ELK Stack (15 minutes)

**1. Créer docker-compose.monitoring.yml** (voir section 6 ci-dessus)

**2. Créer filebeat.yml** (voir section 5 - Option 1)

**3. Démarrer stack:**
```bash
docker-compose -f docker-compose.monitoring.yml up -d

# Vérifier services
docker ps | grep -E 'elasticsearch|kibana|filebeat'

# Accès Kibana
open http://localhost:5601
```

---

### Étape 3 - Configurer Kibana Index Pattern (10 minutes)

**1. Accéder Kibana:** http://localhost:5601

**2. Créer Index Pattern:**
- Menu → Stack Management → Index Patterns
- Créer pattern: `johodp-logs-*`
- Time field: `@t` (timestamp Serilog)
- Save

**3. Vérifier logs:**
- Menu → Discover
- Sélectionner index `johodp-logs-*`
- Vous devez voir les logs Johodp en temps réel

**4. Tester recherche:**
```
message:"User login"
LoginStatus:"Success"
DurationMs:>1000
```

---

### Étape 2 - Ajouter Logs Métier (2-3 jours)

**AccountController.cs - Login:**
```csharp
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        // ... logique login ...
        
        _logger.LogInformation(
            "User login {LoginStatus} for {Email} in tenant {TenantName}, MFA={MfaUsed}, Duration={DurationMs}ms, IP={IpAddress}",
            "Success",
            email,
            tenantName,
            mfaRequired,
            stopwatch.ElapsedMilliseconds,
            HttpContext.Connection.RemoteIpAddress?.ToString()
        );
        
        return Ok(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "User login {LoginStatus} for {Email}, Duration={DurationMs}ms",
            "Failed",
            request.Email,
            stopwatch.ElapsedMilliseconds
        );
        throw;
    }
}
```

**Répéter pour:**
- `POST /api/users/register`
- `POST /api/auth/activate`
- `POST /api/auth/mfa/enroll`
- `POST /api/auth/mfa/verify-enrollment`
- Webhook calls

---

### Étape 3 - Créer Dashboard Kibana (1 jour)

**Kibana → Dashboard → Create New:**

1. **Panel: Logins Success/Failed (Pie Chart)**
   - Query: `message:"User login"`
   - Split: `LoginStatus.keyword`

2. **Panel: Login Latency P95 (Line Chart)**
   - Query: `message:"User login"`
   - Y-axis: Percentiles(DurationMs, 95)
   - X-axis: Date Histogram

3. **Panel: Failed Logins by IP (Table)**
   - Query: `message:"User login" AND LoginStatus:"Failed"`
   - Top 10: `IpAddress.keyword`

4. **Panel: Active Users (Metric)**
   - Query: `message:"Active users"`
   - Metric: Last value `ActiveUsers`

---

## 💰 Estimation Totale (ELK-Only)

---

## 📋 Checklist Monitoring Production

### Avant Déploiement
- [ ] Health checks testés (`/health`, `/health/live`, `/health/ready`)
- [ ] Prometheus metrics activées (`/metrics`)
- [ ] Dashboard Grafana créé (minimum: error rate, latency, CPU/RAM)
- [ ] Alertes critiques configurées (DB down, error > 5%)
- [ ] Logs structurés (JSON) envoyés vers agrégateur
- [ ] Retention logs définie (30j minimum)
- [ ] Runbooks créés (que faire si alerte X ?)

### Première Semaine Prod
- [ ] Monitoring 24/7 actif
- [ ] Alertes testées (simulation panne)
- [ ] Seuils ajustés (réduire faux positifs)
- [ ] Dashboard partagé avec équipe

### Premier Mois Prod
- [ ] Baseline établie (trafic normal, latence normale)
- [ ] Capacity planning (prévoir 6 mois)
- [ ] Post-mortem incidents (si applicable)
- [ ] Optimisations basées sur métriques

---

## 💰 Estimation Totale

| Phase | Effort (SP) | Durée | Coût Cloud/mois | Coût Self-Hosted |
|-------|-------------|-------|-----------------|------------------|
| Phase 1 - Logs JSON + Filebeat + ELK | 12 | 1.5 semaines | $95 (Elastic Cloud) | $0 (Docker local) |
| Phase 2 - Dashboards + Alerting | 10 | 1 semaine | Inclus | $0 |
| Phase 3 - APM | 8 | 1 semaine | +$50 (Elastic APM) | $0 |
| **TOTAL** | **30 SP** | **~3.5 semaines** | **$145/mois** | **$0** |

**Recommandation:** 
- **Dev/Staging:** Self-hosted Docker ($0)
- **Production:** Elastic Cloud ($95/mois) ou AWS OpenSearch ($100/mois)

**Réduction vs Approche Directe Elasticsearch Sink:**
- **Gain temps:** -6 SP (36 → 30 SP) car pas de sink à configurer
- **Gain résilience:** App ne plante pas si ELK down
- **Gain flexibilité:** Changer backend logs sans toucher code

---

## 📋 Checklist Monitoring Production (ELK)

### Avant Déploiement
- [ ] Health checks testés (`/health`, `/health/live`, `/health/ready`)
- [ ] Serilog.Formatting.Compact configuré (JSON console)
- [ ] Logs structurés pour tous événements critiques (login, register, MFA, webhooks)
- [ ] Background service metrics actif (active users, pending activations)
- [ ] Filebeat configuré et testé (logs arrivent dans Elasticsearch)
- [ ] ELK Stack déployé (Elasticsearch + Kibana)
- [ ] Index pattern Kibana créé (`johodp-logs-*`)
- [ ] Dashboard "Johodp Overview" opérationnel
- [ ] Alertes critiques configurées (DB down, error rate > 5%)
- [ ] Retention logs définie (90j via Index Lifecycle Management)
- [ ] Runbooks créés (que faire si alerte X ?)
- [ ] Test résilience: ELK down → App continue de logger en console

### Première Semaine Prod
- [ ] Monitoring 24/7 actif via Kibana
- [ ] Alertes testées (simulation pannes)
- [ ] Seuils ajustés (réduire faux positifs)
- [ ] Dashboard partagé avec équipe
- [ ] Baseline établie (trafic normal, latence normale)

### Premier Mois Prod
- [ ] Elasticsearch Watcher alerting configuré
- [ ] Intégration Slack/PagerDuty/Email testée
- [ ] Capacity planning (prévoir 6 mois)
- [ ] Post-mortem incidents (si applicable)
- [ ] Optimisations basées sur métriques

---

## ✅ Avantages Architecture Console → Filebeat → ELK

| Aspect | Console + Filebeat | Direct Elasticsearch Sink |
|--------|---------------------|---------------------------|
| **Résilience** | ✅ App continue si ELK down | ❌ App peut planter/ralentir si ELK down |
| **Performance** | ✅ Pas de I/O réseau dans app | ⚠️ Latence réseau dans thread app |
| **Stateless** | ✅ App ne connaît pas backend | ❌ App couplée à Elasticsearch |
| **Flexibilité** | ✅ Changer backend sans rebuild | ❌ Rebuild app pour changer |
| **12-Factor** | ✅ Logs as streams (stdout) | ⚠️ Logs as service |
| **Kubernetes** | ✅ Natif (DaemonSet Filebeat) | ⚠️ Nécessite service mesh |
| **Buffer** | ✅ Filebeat buffer disk | ⚠️ Buffer in-memory app |
| **Backpressure** | ✅ Filebeat gère | ❌ App doit gérer |
| **Multi-dest** | ✅ Facile (config Filebeat) | ❌ Rebuild app |
| **Monitoring** | ✅ Filebeat metrics séparées | ⚠️ Metrics mélangées app |

**Verdict pour Johodp:**
✅ **Console + Filebeat recommandé** car:
1. ✅ Résilience maximale (app découplée)
2. ✅ Kubernetes-native
3. ✅ Performance (pas de I/O réseau dans app)
4. ✅ 12-Factor App compliant
5. ✅ Plus simple à maintenir (-6 SP vs sink direct)

---

## 🔗 Ressources

### Documentation Existante
- [HEALTH_CHECKS.md](HEALTH_CHECKS.md) - Health checks actuels (380 lignes)
- [LOGGING_ENRICHERS.md](LOGGING_ENRICHERS.md) - Serilog enrichers (TenantEnricher, UserEnricher)

### Packages NuGet
- [Serilog.Formatting.Compact](https://github.com/serilog/serilog-formatting-compact) - JSON formatter compact
- [Serilog.Exceptions](https://github.com/RehanSaeed/Serilog.Exceptions) - Exception enrichment

### ELK Stack & Filebeat
- [Elasticsearch Documentation](https://www.elastic.co/guide/en/elasticsearch/reference/current/index.html)
- [Kibana Documentation](https://www.elastic.co/guide/en/kibana/current/index.html)
- [Filebeat Documentation](https://www.elastic.co/guide/en/beats/filebeat/current/index.html) - Log shipping
- [Filebeat Docker](https://www.elastic.co/guide/en/beats/filebeat/current/running-on-docker.html)
- [Elasticsearch Watcher](https://www.elastic.co/guide/en/elasticsearch/reference/current/xpack-alerting.html) - Alerting

### Best Practices
- [12-Factor App - Logs](https://12factor.net/logs) - Logs as event streams
- [Kubernetes Logging Architecture](https://kubernetes.io/docs/concepts/cluster-administration/logging/) - DaemonSet pattern

---

## 🚀 Résumé Exécutif

### ✅ Déjà en Place
- Health checks production-ready (`/health`, `/health/live`, `/health/ready`)
- Serilog configuré avec logs console
- Kubernetes probes compatibles

### 🎯 Architecture Retenue: Console → Filebeat → ELK

**Flux:**
```
Johodp API (logs JSON console) 
   → stdout/stderr 
   → Filebeat (DaemonSet Kubernetes ou container Docker)
   → Elasticsearch
   → Kibana (dashboards + alerting)
```

**Avantages:**
- ✅ **12-Factor App** compliant (logs as streams)
- ✅ **Résilience** (app découplée d'ELK)
- ✅ **Performance** (pas de I/O réseau dans app)
- ✅ **Kubernetes-native** (DaemonSet Filebeat)
- ✅ **Simplicité** (une seule ligne de config: JSON formatter)

### 🎯 Plan Recommandé (30 SP, ~3.5 semaines)

**Phase 1 (12 SP, ~1.5 semaines):**
1. Configurer Serilog JSON formatter (2 SP)
2. Enrichir logs métier (login, MFA, webhooks) (5 SP)
3. Background service métriques état (2 SP)
4. Déployer ELK + Filebeat (3 SP)

**ROI:** Monitoring complet avec **$0** (self-hosted) ou **$95/mois** (Elastic Cloud)

**Phase 2 (10 SP, ~1 semaine):**
- Dashboards Kibana avancés (Security, OAuth2, Business)
- Elasticsearch Watcher alerting (Slack, PagerDuty, Email)

**Phase 3 (8 SP, ~1 semaine):**
- Elastic APM (distributed tracing)
- Capacity planning

### 💡 Quick Win (1 heure)

```bash
# 1. Installer JSON formatter
dotnet add src/Johodp.Api package Serilog.Formatting.Compact

# 2. Configurer appsettings.Production.json
{
  "Serilog": {
    "WriteTo": [{
      "Name": "Console",
      "Args": {
        "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
      }
    }]
  }
}

# 3. Lancer ELK + Filebeat (voir docker-compose.monitoring.yml)
docker-compose -f docker-compose.monitoring.yml up -d

# 4. Vérifier
dotnet run --project src/Johodp.Api --environment Production
open http://localhost:5601  # Kibana
```

**Vous avez maintenant:**
- ✅ Logs JSON structurés en console
- ✅ Filebeat shipping automatique vers Elasticsearch
- ✅ Kibana prêt pour dashboards
- ✅ App résiliente (continue même si ELK down)

**Pas besoin de:**
- ❌ Serilog.Sinks.Elasticsearch (app ne connaît pas ELK)
- ❌ Configuration réseau Elasticsearch dans app
- ❌ Gestion erreurs/retry dans app (Filebeat gère)

---

**Dernière mise à jour:** 2024-12-03  
**Stratégie:** Console JSON → Filebeat → ELK (12-Factor App)  
**Estimation Totale:** 30 SP (~3.5 semaines)  
**Gain vs Sink Direct:** -6 SP + Résilience + Flexibilité
