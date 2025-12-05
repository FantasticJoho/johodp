# IdentityServer Event Logging avec IEventSink

## Vue d'ensemble

Duende IdentityServer expose une interface `IEventSink` qui permet d'intercepter et de logger **tous** les événements liés à l'authentification, l'autorisation et la gestion des tokens. C'est l'approche recommandée pour la télémétrie et le monitoring d'un Identity Provider en production.

## Implémentation

### IdentityServerEventSink.cs

```csharp
public class IdentityServerEventSink : IEventSink
{
    private readonly ILogger<IdentityServerEventSink> _logger;

    public Task PersistAsync(Event evt)
    {
        // Détermine le niveau de log selon la catégorie
        var logLevel = GetLogLevel(evt);
        
        // Log structuré avec contexte complet
        _logger.Log(logLevel,
            "IdentityServer Event: {EventName} | Category: {Category} | SubjectId: {SubjectId} | ClientId: {ClientId}",
            evt.Name, evt.Category, GetSubjectId(evt), GetClientId(evt));
        
        // Détails spécifiques par type d'événement
        LogEventDetails(evt, logLevel);
        
        return Task.CompletedTask;
    }
}
```

### Enregistrement dans DI Container

```csharp
// ServiceCollectionExtensions.cs
services.AddSingleton<Duende.IdentityServer.Services.IEventSink, IdentityServerEventSink>();
```

**Note**: Utiliser `AddSingleton` car l'event sink doit être thread-safe et performant (pas d'état mutable).

## Événements Capturés

### 1. Token Events (LogLevel.Information)

**TokenIssuedSuccessEvent**
- GrantType (authorization_code, client_credentials, refresh_token, etc.)
- Scopes accordés
- TokenType (JWT, Reference)
- Lifetime du token
- Présence d'un refresh token

**TokenRevokedSuccessEvent**
- TokenType (access_token, refresh_token)
- Raison de la révocation

**TokenIntrospectionSuccessEvent**
- IsActive (token valide ou non)
- TokenType inspectué
- ClientId demandeur

### 2. User Authentication Events

**UserLoginSuccessEvent** (LogLevel.Information)
- Username/Email
- Provider (local, Google, Azure AD, etc.)
- RemoteIpAddress
- SubjectId

**UserLoginFailureEvent** (LogLevel.Warning)
- Username tenté
- Raison de l'échec (invalid_credentials, account_locked, etc.)
- RemoteIpAddress

**UserLogoutSuccessEvent** (LogLevel.Information)
- SubjectId déconnecté

### 3. Consent Events

**ConsentGrantedEvent** (LogLevel.Information)
- Scopes consentis
- RememberConsent (consent persistant)
- ClientId

**ConsentDeniedEvent** (LogLevel.Warning)
- Scopes refusés
- Raison du refus

### 4. Client Authentication Events

**ClientAuthenticationSuccessEvent** (LogLevel.Information)
- ClientId
- AuthenticationMethod (client_secret_post, private_key_jwt, etc.)

**ClientAuthenticationFailureEvent** (LogLevel.Warning)
- ClientId tenté
- Raison de l'échec (invalid_secret, certificate_expired, etc.)

### 5. API Authentication Events

**ApiAuthenticationSuccessEvent** (LogLevel.Information)
- ApiName
- Scopes demandés
- ClientId

**ApiAuthenticationFailureEvent** (LogLevel.Warning)
- ApiName
- Erreur (insufficient_scope, token_expired, etc.)

### 6. Device Flow Events

**DeviceAuthorizationSuccessEvent**
- UserCode (code affiché sur l'appareil)
- Scopes autorisés

**DeviceAuthorizationFailureEvent**
- Erreur (expired_token, authorization_pending, etc.)

### 7. Error Events (LogLevel.Error)

**InvalidClientConfigurationEvent**
- Message d'erreur de configuration
- ClientId affecté

**UnhandledExceptionEvent**
- Détails de l'exception non gérée
- Stack trace complet

## Cas d'Usage en Production

### 1. Détection de Fraude

```csharp
// Détecter des tentatives de login répétées depuis la même IP
if (evt is UserLoginFailureEvent loginFailure)
{
    _logger.LogWarning(
        "Failed login attempt: Username={Username}, IP={IP}, Error={Error}",
        loginFailure.Username, 
        loginFailure.RemoteIpAddress, 
        loginFailure.Error);
    
    // Trigger rate limiting ou blocage IP
    await _securityService.RecordFailedAttemptAsync(
        loginFailure.RemoteIpAddress, 
        loginFailure.Username);
}
```

### 2. Audit Trail pour Compliance (GDPR, SOC2, ISO27001)

```csharp
// Logger tous les accès aux données utilisateur
if (evt is TokenIssuedSuccessEvent tokenIssued)
{
    await _auditService.RecordTokenIssuanceAsync(new AuditEntry
    {
        Timestamp = DateTime.UtcNow,
        SubjectId = tokenIssued.SubjectId,
        ClientId = tokenIssued.ClientId,
        Scopes = tokenIssued.Scopes,
        GrantType = tokenIssued.GrantType,
        IpAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress
    });
}
```

### 3. Monitoring Business Metrics

```csharp
// Métriques Prometheus/StatsD
if (evt is TokenIssuedSuccessEvent tokenIssued)
{
    _metrics.IncrementCounter("identityserver.tokens.issued", 
        tags: new { 
            grant_type = tokenIssued.GrantType,
            client_id = tokenIssued.ClientId,
            token_type = tokenIssued.TokenType
        });
    
    _metrics.RecordHistogram("identityserver.token.lifetime", 
        tokenIssued.Lifetime,
        tags: new { client_id = tokenIssued.ClientId });
}
```

### 4. Alertes Temps Réel

```csharp
// Alertes PagerDuty/Slack
if (evt is UnhandledExceptionEvent exception)
{
    await _alertService.SendCriticalAlertAsync(
        title: "IdentityServer Unhandled Exception",
        details: exception.Details,
        severity: AlertSeverity.Critical);
}

if (evt is ClientAuthenticationFailureEvent clientFailure)
{
    // Alerte si client production échoue
    if (IsProductionClient(clientFailure.ClientId))
    {
        await _alertService.SendWarningAlertAsync(
            title: "Production Client Authentication Failed",
            details: $"Client: {clientFailure.ClientId}, Error: {clientFailure.Message}");
    }
}
```

### 5. Exportation vers SIEM (Security Information and Event Management)

```csharp
public async Task PersistAsync(Event evt)
{
    // Log local
    _logger.Log(GetLogLevel(evt), "Event: {EventName}", evt.Name);
    
    // Exportation vers Splunk, QRadar, ELK, etc.
    await _siemExporter.ExportAsync(new SiemEvent
    {
        EventId = evt.Id,
        EventName = evt.Name,
        Category = evt.Category,
        SubjectId = GetSubjectId(evt),
        ClientId = GetClientId(evt),
        Timestamp = DateTime.UtcNow,
        RawData = JsonSerializer.Serialize(evt)
    });
}
```

## Configuration des Événements

Par défaut, IdentityServer ne raise pas tous les événements. Activer selon les besoins :

```csharp
services.AddIdentityServer(options =>
{
    options.Events.RaiseSuccessEvents = true;     // Logins réussis, tokens émis
    options.Events.RaiseFailureEvents = true;     // Échecs d'authentification
    options.Events.RaiseInformationEvents = true; // Consent, logout
    options.Events.RaiseErrorEvents = true;       // Erreurs internes
})
```

**Recommandation Production**:
- `RaiseSuccessEvents = true` → Audit trail complet
- `RaiseFailureEvents = true` → Détection d'intrusions
- `RaiseInformationEvents = false` → Réduit le volume de logs (sauf si compliance stricte)
- `RaiseErrorEvents = true` → Monitoring des erreurs critiques

## Logs Structurés (Serilog)

Les événements sont automatiquement enrichis avec :

```csharp
_logger.LogInformation(
    "Token issued: GrantType={GrantType}, Scopes={Scopes}, TokenType={TokenType}, ClientId={ClientId}",
    tokenIssued.GrantType,
    string.Join(" ", tokenIssued.Scopes),
    tokenIssued.TokenType,
    tokenIssued.ClientId);
```

**Exemple de log JSON structuré** :

```json
{
  "timestamp": "2025-12-05T01:46:28.951Z",
  "level": "INFO",
  "message": "Token issued",
  "properties": {
    "GrantType": "authorization_code",
    "Scopes": "openid profile email johodp.api offline_access",
    "TokenType": "Jwt",
    "ClientId": "workflow-test-spa-1764895144",
    "SubjectId": "e1826f1b-1278-4a41-883a-a8efd4d38aa3",
    "Lifetime": 3600,
    "HasRefreshToken": true,
    "TraceId": "96f20392c6e413c107e46615bfdb8aa0",
    "TenantId": "51146303-8ff4-494d-a77b-5e5f451138c8"
  }
}
```

## Performance Considerations

### 1. Singleton Pattern
`IEventSink` est enregistré en **Singleton** car :
- Pas d'état mutable
- Appels très fréquents (chaque requête token)
- Besoin de haute performance

### 2. Async Operations
Si l'event sink fait des appels réseau (SIEM, webhooks), utiliser `Task.Run` pour éviter de bloquer :

```csharp
public Task PersistAsync(Event evt)
{
    // Log local synchrone (rapide)
    _logger.Log(GetLogLevel(evt), "Event: {EventName}", evt.Name);
    
    // Exportation async (sans bloquer)
    _ = Task.Run(async () => 
    {
        try
        {
            await _siemExporter.ExportAsync(evt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export event to SIEM");
        }
    });
    
    return Task.CompletedTask;
}
```

### 3. Sampling pour High-Traffic

En production avec 1000+ RPS, sampler les événements low-value :

```csharp
public Task PersistAsync(Event evt)
{
    // Toujours logger les failures et errors
    if (evt.Category == EventCategories.Error || 
        evt.Category == EventCategories.Failure)
    {
        LogEvent(evt);
        return Task.CompletedTask;
    }
    
    // Sampler 10% des success events
    if (evt.Category == EventCategories.Token && Random.Shared.Next(100) < 10)
    {
        LogEvent(evt);
    }
    
    return Task.CompletedTask;
}
```

## Dashboard Monitoring Exemple (Grafana)

**Queries Prometheus** :

```promql
# Tokens émis par seconde
rate(identityserver_tokens_issued_total[5m])

# Taux d'échec login (>5% = alerte)
rate(identityserver_login_failures_total[5m]) / 
rate(identityserver_login_attempts_total[5m]) * 100

# Top 5 clients par volume de tokens
topk(5, sum by (client_id) (
  rate(identityserver_tokens_issued_total[1h])
))

# Latence P95 de génération de token
histogram_quantile(0.95, 
  rate(identityserver_token_issuance_duration_seconds_bucket[5m])
)
```

**Alertes Critiques** :

```yaml
groups:
  - name: identityserver_alerts
    rules:
      - alert: HighLoginFailureRate
        expr: |
          rate(identityserver_login_failures_total[5m]) /
          rate(identityserver_login_attempts_total[5m]) > 0.05
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High login failure rate (>5%)"
          
      - alert: ClientAuthenticationFailed
        expr: identityserver_client_auth_failures_total > 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Client {{ $labels.client_id }} authentication failed"
```

## Intégration avec Application Insights

L'event sink s'intègre automatiquement avec Application Insights via l'extension Serilog :

```csharp
// Program.cs
Log.Logger = new LoggerConfiguration()
    .WriteTo.ApplicationInsights(
        telemetryConfiguration,
        TelemetryConverter.Traces)
    .Enrich.FromLogContext()
    .CreateLogger();
```

**Queries KQL (Kusto Query Language)** :

```kql
// Top 10 clients par token émis
traces
| where message contains "Token issued"
| where customDimensions.ClientId != ""
| summarize count() by tostring(customDimensions.ClientId)
| top 10 by count_ desc

// Échecs login par IP
traces
| where message contains "User login failed"
| extend IP = tostring(customDimensions.RemoteIpAddress)
| summarize failures = count() by IP
| where failures > 10
| order by failures desc

// Distribution des grant types
traces
| where message contains "Token issued"
| extend GrantType = tostring(customDimensions.GrantType)
| summarize count() by GrantType
| render piechart
```

## Sécurité et Compliance

### PII (Personally Identifiable Information)

**NE JAMAIS logger** :
- Mots de passe
- Client secrets complets
- Tokens complets (seulement les 4 derniers caractères)
- Adresses email complètes en production (masquer : `j***n@example.com`)

**Exemple de masquage** :

```csharp
private string MaskEmail(string email)
{
    if (string.IsNullOrEmpty(email) || !email.Contains('@'))
        return email;
        
    var parts = email.Split('@');
    var localPart = parts[0];
    var domain = parts[1];
    
    if (localPart.Length <= 2)
        return $"***@{domain}";
        
    return $"{localPart[0]}***{localPart[^1]}@{domain}";
}
```

### Retention des Logs

**Compliance GDPR** : Conserver max 90 jours (sauf audit légal)

```csharp
// appsettings.Production.json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/identityserver-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 90,
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

## Checklist Production

- [x] `IEventSink` implémenté et enregistré en Singleton
- [x] Logs structurés JSON avec Serilog
- [ ] Configuration `RaiseSuccessEvents/FailureEvents` selon besoins
- [ ] Masquage PII (email, tokens)
- [ ] Rotation et retention des logs (90 jours max)
- [ ] Exportation vers SIEM (Splunk, ELK, Application Insights)
- [ ] Dashboards Grafana/Kibana configurés
- [ ] Alertes critiques (login failures, client auth failed)
- [ ] Métriques Prometheus/StatsD pour business analytics
- [ ] Tests de charge pour valider performance (pas de bottleneck)

## Ressources

- [Duende IdentityServer Events Documentation](https://docs.duendesoftware.com/identityserver/v7/fundamentals/events/)
- [ASP.NET Core Logging Best Practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/)
- [Serilog Structured Logging](https://serilog.net/)
- [TELEMETRY_ECOMMERCE.md](TELEMETRY_ECOMMERCE.md) - Métriques complémentaires pour e-commerce
