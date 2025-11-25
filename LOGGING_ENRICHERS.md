# Logging Enrichers - TenantId & ClientId

## Vue d'ensemble

Le système de logging utilise Serilog avec des enrichers personnalisés pour capturer automatiquement le contexte `TenantId` et `ClientId` dans tous les événements de log.

## TenantClientEnricher

### Localisation
`src/Johodp.Api/Logging/TenantClientEnricher.cs`

### Fonctionnement

L'enricher s'exécute pour chaque événement de log et extrait le contexte depuis `HttpContext`.

#### Extraction du TenantId (ordre de priorité)

1. **acr_values** (OIDC standard) - paramètre de requête
   - Format: `acr_values=tenant:xxx` ou `acr_values=tenant:xxx%20other:yyy`
   - Exemple: `/connect/authorize?acr_values=tenant:acme-corp`
   - Utilisé lors de l'authentification initiale

2. **Claim tenant_id** - depuis le token utilisateur authentifié
   - Ajouté par `IdentityServerProfileService` après authentification
   - Disponible dans `HttpContext.User.Claims`

3. **Query string tenant** - paramètre de requête
   - Exemple: `?tenant=acme-corp`

4. **Header X-Tenant-Id** - header HTTP personnalisé
   - Exemple: `X-Tenant-Id: acme-corp`

#### Extraction du ClientId (ordre de priorité)

1. **Claim client_id** - depuis le token utilisateur authentifié
   - Standard OIDC, présent dans les tokens d'accès

2. **Query string client_id** - paramètre de requête
   - Exemple: `?client_id=spa-client`

3. **Header X-Client-Id** - header HTTP personnalisé
   - Exemple: `X-Client-Id: spa-client`

### Configuration

Enregistré dans `Program.cs`:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .Enrich.WithProperty("Application", "Johodp")
       .Enrich.With(new TenantClientEnricher(
           services.GetRequiredService<IHttpContextAccessor>()))
       .WriteTo.Console(outputTemplate: 
           "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] " +
           "{TenantId} {ClientId} [{SourceContext}] {Message:lj}{NewLine}{Exception}");
});
```

### Format de sortie

Les logs affichent automatiquement `TenantId` et `ClientId` quand disponibles:

```
[2025-11-25 10:30:45.123 +00:00] [INF] acme-corp spa-client [Johodp.Api.Controllers.Account.AccountController] User login successful for email@example.com
```

Si absents, les champs restent vides:
```
[2025-11-25 10:30:45.123 +00:00] [INF]   [Johodp.Api.Program] Starting Johodp Identity Provider application
```

## Cas d'usage

### 1. Authentification OIDC initiale

Le client envoie:
```
GET /connect/authorize?
  response_type=code&
  client_id=spa-client&
  acr_values=tenant:acme-corp&
  ...
```

→ Les logs capturent `TenantId=acme-corp` depuis `acr_values`.

### 2. Requêtes API avec token

Le client envoie:
```
GET /api/users
Authorization: Bearer eyJ...
```

Le token contient les claims `tenant_id` et `client_id`.

→ Les logs capturent automatiquement ces valeurs depuis les claims.

### 3. Logs de débogage multi-tenant

Tous les logs liés à une requête affichent le tenant/client, permettant:
- Filtrage par tenant: `TenantId="acme-corp"`
- Analyse par client: `ClientId="mobile-app"`
- Traçabilité des opérations multi-tenant
- Débogage des problèmes spécifiques tenant

## Intégration avec IdentityServer

Le `IdentityServerProfileService` émet les claims après authentification:

```csharp
foreach (var tenantId in user.TenantIds)
{
    claims.Add(new Claim("tenant_id", tenantId));
}
```

Ces claims sont ensuite disponibles dans `HttpContext.User` pour l'enricher.

## Avantages

1. **Automatique** - aucun code métier nécessaire pour logger tenant/client
2. **Centralisé** - logique d'extraction unique dans l'enricher
3. **Conforme OIDC** - utilise le standard `acr_values` pour le contexte
4. **Traçabilité** - chaque requête est associée à son contexte
5. **Debugging** - filtrage rapide des logs par tenant/client dans Seq, Kibana, etc.

## Extension

Pour ajouter d'autres propriétés contextuelles:

1. Modifier `TenantClientEnricher.Enrich()`
2. Extraire depuis `HttpContext` (claims, headers, query)
3. Appeler `logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(...))`
4. Mettre à jour le template de sortie si besoin

## Limitations

- L'enricher ne fonctionne que pour les logs générés pendant une requête HTTP
- Les logs de startup/shutdown n'auront pas ces propriétés
- Nécessite `HttpContextAccessor` (léger overhead de performance)
