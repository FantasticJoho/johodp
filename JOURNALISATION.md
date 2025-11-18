# 📋 Guide de journalisation (Logging)

Ce document décrit les bonnes pratiques de journalisation implémentées dans l'application Johodp et les standards à suivre pour maintenir une application production-ready.

## Table des matières

- [Vue d'ensemble](#vue-densemble)
- [Configuration Serilog](#configuration-serilog)
- [Niveaux de log](#niveaux-de-log)
- [Bonnes pratiques](#bonnes-pratiques)
- [Exemples par composant](#exemples-par-composant)
- [Logs à éviter](#logs-à-éviter)
- [Monitoring en production](#monitoring-en-production)

---

## Vue d'ensemble

L'application utilise **Serilog** comme framework de journalisation structurée avec les caractéristiques suivantes :

- **Logs structurés** : Utilisation de templates avec paramètres nommés
- **Enrichissement automatique** : Contexte, application, thread
- **Filtrage par niveau** : Réduction du bruit des frameworks
- **Format cohérent** : Timestamps, niveaux, sources clairement identifiés

## Configuration Serilog

### Configuration actuelle (Program.cs)

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Johodp")
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
```

### Middleware de logging HTTP

```csharp
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            diagnosticContext.Set("UserEmail", httpContext.User.FindFirst("email")?.Value);
            diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
        }
    };
});
```

---

## Niveaux de log

### Utilisation des niveaux

| Niveau | Usage | Exemples |
|--------|-------|----------|
| **Verbose/Debug** | Informations de débogage détaillées | Valeurs de variables, flux détaillé |
| **Information** | Événements normaux importants | Connexion utilisateur réussie, opération terminée |
| **Warning** | Situations anormales non bloquantes | Tentative d'accès à un tenant non autorisé, retry |
| **Error** | Erreurs traitées | Échec de création utilisateur, validation échouée |
| **Fatal** | Erreurs critiques application | Crash application, impossibilité de démarrer |

### Exemples par niveau

```csharp
// Debug - détails techniques
_logger.LogDebug("Extracted tenant from acr_values: {TenantId}", tenantId);

// Information - opérations importantes
_logger.LogInformation("Successful login for user: {Email}, tenant: {TenantId}", email, tenantId);

// Warning - situation inhabituelle
_logger.LogWarning("Tenant access denied for user {Email}. User tenant: {UserTenant}, Requested tenant: {RequestedTenant}", 
    email, userTenant, requestedTenant);

// Error - échec traité
_logger.LogError("Failed to create user {Email}: {Errors}", email, errors);

// Fatal - crash critique
Log.Fatal(ex, "Application terminated unexpectedly. Error: {ErrorMessage}", ex.Message);
```

---

## Bonnes pratiques

### 1. ✅ Utiliser des messages structurés

**BON :**
```csharp
_logger.LogInformation("User {Email} logged in from {IpAddress}", email, ipAddress);
```

**MAUVAIS :**
```csharp
_logger.LogInformation($"User {email} logged in from {ipAddress}"); // Interpolation de chaîne
_logger.LogInformation("User " + email + " logged in"); // Concaténation
```

### 2. ✅ Nommer les paramètres de manière significative

**BON :**
```csharp
_logger.LogInformation("Processing order {OrderId} for customer {CustomerId}", orderId, customerId);
```

**MAUVAIS :**
```csharp
_logger.LogInformation("Processing order {Id1} for customer {Id2}", orderId, customerId);
```

### 3. ✅ Logger les informations de contexte

```csharp
_logger.LogInformation("Login attempt for email: {Email}", model.Email);

// Contexte additionnel pour les erreurs
_logger.LogError("Failed to register user {Email}: {Errors}", 
    model.Email, 
    string.Join(", ", createResult.Errors.Select(e => e.Description)));
```

### 4. ✅ Logger au bon endroit

- **Controller** : Requêtes entrantes, résultats, erreurs métier
- **Service/Handler** : Opérations métier importantes (éviter si déjà loggé au controller)
- **Repository** : Éviter (trop de bruit, EF Core log déjà)
- **Middleware** : Requêtes HTTP, exceptions globales

### 5. ✅ Logger les tentatives de sécurité

```csharp
// Toujours logger les authentifications
_logger.LogInformation("Successful login for user: {Email}", email);
_logger.LogWarning("Failed login attempt for user: {Email}", email);

// Refus d'accès
_logger.LogWarning("Tenant access denied for user {Email}. User tenant: {UserTenant}, Requested tenant: {RequestedTenant}", 
    email, userTenant, requestedTenant);

// MFA
_logger.LogInformation("MFA required for user: {Email}", email);
```

### 6. ✅ Inclure l'exception complète

```csharp
try
{
    // code
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error processing request for user {UserId}", userId);
    throw;
}
```

### 7. ✅ Éviter les données sensibles

**NE JAMAIS logger :**
- Mots de passe
- Tokens complets
- Numéros de carte bancaire
- Données personnelles sensibles (sauf si absolument nécessaire et conforme RGPD)

**BON :**
```csharp
_logger.LogInformation("Password reset requested for email: {Email}", email);
```

**MAUVAIS :**
```csharp
_logger.LogInformation("Password reset: {Email}, Token: {Token}", email, resetToken); // ❌ Token en clair
_logger.LogDebug("Login attempt: {Email}, Password: {Password}", email, password); // ❌ Jamais logger les mots de passe
```

### 8. ✅ Utiliser des métriques pour la performance

```csharp
// Le middleware HTTP log déjà le temps d'exécution
// Éviter de logger manuellement sauf pour des opérations spécifiques

var stopwatch = Stopwatch.StartNew();
// opération longue
stopwatch.Stop();
_logger.LogInformation("Long operation completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
```

### 9. ✅ Logger les informations de démarrage

```csharp
Log.Information("Starting Johodp Identity Provider application");
Log.Information("Environment: {Environment}", environment);
Log.Information("Application URLs: {Urls}", urls);
```

### 10. ✅ Scope pour le contexte partagé

```csharp
using (_logger.BeginScope("OrderId: {OrderId}", orderId))
{
    _logger.LogInformation("Processing payment");
    _logger.LogInformation("Sending confirmation"); // OrderId sera inclus automatiquement
}
```

---

## Exemples par composant

### AccountController - Authentification

```csharp
// Requête initiale
_logger.LogInformation("Login page requested. ReturnUrl: {ReturnUrl}", returnUrl);

// Extraction tenant
_logger.LogDebug("Extracted tenant from acr_values: {TenantId}", tenantId);

// Tentative de connexion
_logger.LogInformation("Login attempt for email: {Email}", model.Email);

// Création automatique d'utilisateur
_logger.LogInformation("Creating new user during login: {Email} with tenant: {TenantId}", email, tenantId);

// Validation tenant échouée
_logger.LogWarning("Tenant access denied for user {Email}. User tenant: {UserTenant}, Requested tenant: {RequestedTenant}", 
    email, userTenant, requestedTenant);

// Succès
_logger.LogInformation("Successful login for user: {Email}, tenant: {TenantId}", email, tenantId);

// MFA requis
_logger.LogInformation("MFA required for user: {Email}", email);

// Échec
_logger.LogWarning("Failed login attempt for user: {Email}", email);
```

### UsersController - API CQRS

```csharp
// Requête d'inscription
_logger.LogInformation("User registration requested for email: {Email}", command.Email);

// Succès
_logger.LogInformation("User successfully registered: {Email}, UserId: {UserId}", email, userId);

// Échec
_logger.LogWarning("User registration failed for {Email}: {Error}", email, ex.Message);

// Erreur inattendue
_logger.LogError(ex, "Unexpected error during user registration for {Email}", email);

// Recherche utilisateur
_logger.LogDebug("Get user requested for UserId: {UserId}", userId);
_logger.LogWarning("User not found: {UserId}", userId);
```

### IdentityServerProfileService - Claims OIDC

```csharp
// Génération de profil
_logger.LogDebug("Generating profile data for subject: {Subject}", sub);
_logger.LogInformation("Building claims for user: {Email}, tenant: {TenantId}", email, tenantId);

// Utilisateur non trouvé
_logger.LogWarning("User not found for subject: {Subject}", sub);

// Vérification statut actif
_logger.LogDebug("Checking if user is active for subject: {Subject}", sub);
_logger.LogWarning("User {Email} is not active", email);
```

### GlobalExceptionHandlerMiddleware - Erreurs globales

```csharp
_logger.LogError(ex, 
    "Unhandled exception occurred. Path: {Path}, Method: {Method}, User: {User}", 
    context.Request.Path, 
    context.Request.Method,
    context.User?.Identity?.Name ?? "Anonymous");
```

---

## Logs à éviter

### ❌ Sur-logging

```csharp
// Trop de détails inutiles
_logger.LogDebug("Entering method GetUser");
_logger.LogDebug("userId parameter: {UserId}", userId);
_logger.LogDebug("Calling repository");
var user = await _repository.GetUserAsync(userId);
_logger.LogDebug("Repository returned result");
_logger.LogDebug("Exiting method GetUser");
```

### ❌ Logs redondants

```csharp
// Controller
_logger.LogInformation("Creating user {Email}", email);
await _mediator.Send(command);

// Handler
_logger.LogInformation("Creating user {Email}", email); // ❌ Redondant
```

### ❌ Logs dans les boucles

```csharp
// Éviter si la liste peut être grande
foreach (var item in largeList)
{
    _logger.LogDebug("Processing item {ItemId}", item.Id); // ❌ Peut générer des milliers de logs
}

// Préférer un log agrégé
_logger.LogInformation("Processing {Count} items", largeList.Count);
```

---

## Monitoring en production

### Configuration production recommandée

```csharp
// appsettings.Production.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/johodp-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

### Métriques à surveiller

1. **Authentifications**
   - Tentatives de connexion (succès/échec)
   - Refus d'accès tenant
   - MFA requis

2. **Erreurs**
   - Taux d'erreurs par endpoint
   - Exceptions non gérées
   - Échecs de base de données

3. **Performance**
   - Temps de réponse HTTP
   - Opérations lentes (> 1s)

4. **Sécurité**
   - Tentatives de connexion échouées répétées
   - Accès refusés
   - Token invalides

### Outils de monitoring recommandés

- **Application Insights** (Azure)
- **ELK Stack** (Elasticsearch, Logstash, Kibana)
- **Seq** (analyse logs Serilog)
- **Grafana + Loki**
- **Datadog**

### Alertes à configurer

```yaml
Alertes critiques:
  - Taux d'erreur > 5%
  - Temps de réponse moyen > 2s
  - Plus de 10 tentatives de connexion échouées en 1 minute
  - Exception non gérée (Fatal)
  - Base de données inaccessible

Alertes warning:
  - Taux d'erreur > 2%
  - Temps de réponse moyen > 1s
  - Refus d'accès tenant inhabituel
```

---

## Logs structurés - Requêtes complexes

### Avec Seq ou Application Insights

Les logs structurés permettent des requêtes puissantes :

```sql
-- Trouver tous les refus d'accès tenant
Level = 'Warning' AND MessageTemplate LIKE '%Tenant access denied%'

-- Temps de réponse par endpoint
Aggregate(Elapsed) 
WHERE RequestPath LIKE '/api/%' 
GROUP BY RequestPath

-- Échecs de connexion par utilisateur
WHERE MessageTemplate LIKE '%Failed login%'
GROUP BY Email
HAVING Count > 3
```

---

## Checklist de revue de code

Lors de la revue de code, vérifier :

- [ ] Tous les endpoints critiques ont des logs Information
- [ ] Les erreurs incluent l'exception complète
- [ ] Pas de données sensibles dans les logs
- [ ] Utilisation de templates structurés (pas d'interpolation)
- [ ] Noms de paramètres significatifs
- [ ] Niveau de log approprié
- [ ] Pas de sur-logging dans les boucles
- [ ] Contexte suffisant pour déboguer
- [ ] Logs de sécurité présents (authentification, autorisation)

---

## Ressources

- [Serilog Documentation](https://serilog.net/)
- [Structured Logging Concepts](https://github.com/serilog/serilog/wiki/Structured-Data)
- [ASP.NET Core Logging](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/)
- [Serilog Best Practices](https://benfoster.io/blog/serilog-best-practices/)

---

## Mise à jour du document

**Dernière mise à jour** : 18 novembre 2025  
**Version** : 1.0  
**Auteur** : Équipe Johodp
