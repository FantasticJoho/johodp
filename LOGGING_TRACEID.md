# 🔍 Logging, TraceId et Reverse Proxy

Ce document explique la configuration du logging, du TraceId et des headers X-Forwarded-* pour le déploiement derrière un reverse proxy.

---

## 📊 TraceId - Corrélation des Logs

### Qu'est-ce que le TraceId ?

Le **TraceId** est un **identifiant unique (GUID)** généré automatiquement par ASP.NET Core pour chaque requête HTTP entrante. Il permet de **corréler tous les logs** liés à une même requête.

### Avantages

```
✅ Tracer toute l'exécution d'une requête (de l'entrée à la sortie)
✅ Déboguer facilement les erreurs en filtrant par TraceId
✅ Analyser les performances d'une requête spécifique
✅ Suivre les opérations asynchrones (background jobs, events)
```

### Exemple de logs

```log
[2024-12-05 10:30:15.123] [INF] [0HN4J8K9L2M3N] acme-corp my-app 185.23.45.67 API login attempt for email: user@example.com
[2024-12-05 10:30:15.234] [INF] [0HN4J8K9L2M3N] acme-corp my-app 185.23.45.67 Login successful: user@example.com, tenant: acme-corp
[2024-12-05 10:30:15.345] [INF] [0HN4J8K9L2M3N] acme-corp my-app 185.23.45.67 HTTP POST /api/auth/login 200 in 222.0ms
```

**Tous les logs ont le même TraceId** `0HN4J8K9L2M3N` → facile de reconstituer le flux complet !

### Filtrer les logs par TraceId

```bash
# Grep dans les logs
grep "0HN4J8K9L2M3N" application.log

# Dans ELK/Splunk/Azure Monitor
TraceId:"0HN4J8K9L2M3N"

# Dans Seq
TraceId = '0HN4J8K9L2M3N'
```

---

## 🔀 Headers X-Forwarded-* (Reverse Proxy)

### Architecture typique

```
Client (185.23.45.67) 
    ↓ HTTPS (443)
Nginx / Azure App Gateway / AWS ALB
    ↓ HTTP (5000) - réseau interne
API Johodp (10.0.0.5)
```

**Problème** : Sans configuration, l'API voit :
- IP = `10.0.0.5` (IP du proxy, pas du client)
- Protocol = `http` (alors que le client utilise `https`)
- Host = `localhost:5000` (pas le domaine public)

**Solution** : Le proxy ajoute des headers `X-Forwarded-*` pour préserver l'information originale.

### Headers X-Forwarded-*

| Header | Description | Exemple |
|--------|-------------|---------|
| **X-Forwarded-For** | IP réelle du client (peut être une liste si plusieurs proxies) | `185.23.45.67` ou `185.23.45.67, 10.0.0.100` |
| **X-Forwarded-Proto** | Protocole original (http ou https) | `https` |
| **X-Forwarded-Host** | Host/domaine original demandé par le client | `api.johodp.com` |
| **X-Forwarded-Port** | Port original (80, 443, etc.) | `443` |

### Configuration nginx (exemple)

```nginx
server {
    listen 443 ssl;
    server_name api.johodp.com;

    location / {
        proxy_pass http://localhost:5000;
        
        # Ajouter les headers X-Forwarded-*
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Port $server_port;
        
        # Headers additionnels
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

### Configuration dans Johodp

**Fichier : `Program.cs`**

```csharp
// Configuration des Forwarded Headers
static void ConfigureForwardedHeaders(IServiceCollection services)
{
    services.Configure<ForwardedHeadersOptions>(options =>
    {
        // Lire X-Forwarded-For, X-Forwarded-Proto, X-Forwarded-Host
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor 
                                  | ForwardedHeaders.XForwardedProto 
                                  | ForwardedHeaders.XForwardedHost;

        // ⚠️ DÉVELOPPEMENT: Accepter tous les proxies
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();

        // Limite de sécurité (max 2 proxies en cascade)
        options.ForwardLimit = 2;
    });
}

// Middleware Pipeline (ORDRE IMPORTANT!)
static void ConfigureMiddlewarePipeline(WebApplication app)
{
    // ⚠️ DOIT être appelé EN PREMIER
    app.UseForwardedHeaders();
    
    app.UseRequestLogging();
    app.UseSerilogRequestLogging();
    // ... autres middlewares
}
```

### ⚠️ Sécurité en Production

**DANGER** : Accepter tous les proxies permet le **IP spoofing** !

```csharp
// ❌ DÉVELOPPEMENT SEULEMENT
options.KnownNetworks.Clear();
options.KnownProxies.Clear();

// ✅ PRODUCTION: Spécifier les IPs des proxies de confiance
options.KnownProxies.Add(IPAddress.Parse("10.0.0.100")); // nginx
options.KnownProxies.Add(IPAddress.Parse("10.0.0.101")); // backup nginx

// Ou réseau entier (Azure/AWS)
options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
```

**Pourquoi ?** Un attaquant pourrait envoyer :
```http
GET /api/auth/login HTTP/1.1
X-Forwarded-For: 127.0.0.1
```

Si tu acceptes tous les proxies, l'API croira que la requête vient de `127.0.0.1` (localhost bypass !).

---

## 📝 Format des Logs

### Template Serilog

```csharp
"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{TraceId}] {TenantId} {ClientId} {ClientIP} {Message:lj}{NewLine}{Exception}"
```

### Exemple de sortie

```log
[2024-12-05 10:30:15.123] [INF] [0HN4J8K9L2M3N] acme-corp my-app 185.23.45.67 API login attempt for email: user@example.com, tenant: acme-corp
[2024-12-05 10:30:15.234] [WRN] [0HN4J8K9L2M3N] acme-corp my-app 185.23.45.67 Invalid TOTP code during MFA verification for user d4f2e1a0-1234-5678-9abc-def012345678
[2024-12-05 10:30:15.345] [INF] [0HN4J8K9L2M3N] acme-corp my-app 185.23.45.67 HTTP POST /api/auth/login 401 in 222.0ms
```

### Propriétés enrichies automatiquement

| Propriété | Source | Exemple |
|-----------|--------|---------|
| **Timestamp** | DateTime.UtcNow | `2024-12-05 10:30:15.123` |
| **Level** | LogLevel | `INF`, `WRN`, `ERR` |
| **TraceId** | HttpContext.TraceIdentifier | `0HN4J8K9L2M3N` |
| **TenantId** | TenantClientEnricher | `acme-corp` |
| **ClientId** | TenantClientEnricher | `my-app` |
| **ClientIP** | Connection.RemoteIpAddress (après X-Forwarded-For) | `185.23.45.67` |
| **Message** | Log message | `API login attempt...` |
| **Exception** | Exception stack trace | `System.NullReferenceException...` |

---

## 🔧 Enrichers Serilog

### TraceIdEnricher

**Fichier : `Logging/TraceIdEnricher.cs`**

Ajoute automatiquement le **TraceId**, **ClientIP**, **HttpMethod** et **HttpPath** à chaque log.

```csharp
public class TraceIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        // TraceId unique par requête
        var traceId = httpContext.TraceIdentifier;
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", traceId));

        // IP réelle (après UseForwardedHeaders)
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ClientIP", clientIp));
    }
}
```

### TenantClientEnricher

**Fichier : `Logging/TenantClientEnricher.cs`** (existant)

Ajoute automatiquement **TenantId** et **ClientId** à chaque log.

---

## 🧪 Tests

### Test local (sans proxy)

```powershell
# Démarrer l'API
dotnet run --project src/Johodp.Api

# Faire une requête
curl http://localhost:5000/api/auth/login -X POST `
  -H "Content-Type: application/json" `
  -d '{"email":"user@example.com","password":"test","tenantName":"acme-corp"}'
```

**Logs attendus** :
```log
[2024-12-05 10:30:15.123] [INF] [0HN4J8K9L2M3N] acme-corp  ::1 API login attempt for email: user@example.com
```

### Test avec proxy (nginx local)

```powershell
# Démarrer nginx avec config
nginx -c nginx.conf

# Requête via proxy
curl https://localhost:443/api/auth/login -X POST `
  -H "Content-Type: application/json" `
  -d '{"email":"user@example.com","password":"test","tenantName":"acme-corp"}'
```

**Logs attendus** :
```log
[2024-12-05 10:30:15.123] [INF] [0HN4J8K9L2M3N] acme-corp my-app 185.23.45.67 API login attempt for email: user@example.com
```

**Vérifier les headers** :
```log
[2024-12-05 10:30:15.123] [DBG] [0HN4J8K9L2M3N] X-Forwarded-For: 185.23.45.67
[2024-12-05 10:30:15.123] [DBG] [0HN4J8K9L2M3N] X-Forwarded-Proto: https
[2024-12-05 10:30:15.123] [DBG] [0HN4J8K9L2M3N] X-Forwarded-Host: api.johodp.com
```

---

## 📚 Références

- [ASP.NET Core Forwarded Headers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer)
- [Serilog Enrichers](https://github.com/serilog/serilog/wiki/Enrichment)
- [RFC 7239 - Forwarded HTTP Extension](https://datatracker.ietf.org/doc/html/rfc7239)
- [OWASP - IP Spoofing Prevention](https://cheatsheetseries.owasp.org/cheatsheets/Unvalidated_Redirects_and_Forwards_Cheat_Sheet.html)

---

## 🚀 Déploiement

### Azure Application Gateway

```csharp
// Azure ajoute automatiquement X-Forwarded-* headers
// Configurer le réseau Azure dans KnownNetworks
options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
```

### AWS Application Load Balancer

```csharp
// AWS ALB ajoute X-Forwarded-For, X-Forwarded-Proto, X-Forwarded-Port
// Vérifier les IPs du VPC
options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("172.31.0.0"), 16));
```

### Docker + nginx-proxy

```csharp
// nginx-proxy ajoute automatiquement les headers
// Spécifier l'IP du container nginx-proxy
options.KnownProxies.Add(IPAddress.Parse("172.17.0.2"));
```
