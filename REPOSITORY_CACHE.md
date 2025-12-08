# 🚀 Repository Cache - Architecture et Utilisation

## 📖 Vue d'ensemble

Le projet utilise le **Decorator Pattern** pour ajouter une couche de cache en mémoire aux repositories des entités stables (`Tenant`, `Client`). Cette stratégie réduit les accès à la base de données pour les données rarement modifiées.

---

## 🎯 Entités Cachées

### ✅ Tenant (Cache activé)
- **Fréquence de modification** : Trimestrielle
- **Fréquence de lecture** : 1000+ fois/jour (chaque requête API)
- **Durée de cache** : 24 heures
- **Invalidation** : Explicite lors de Create/Update/Delete

### ✅ Client (Cache activé)
- **Fréquence de modification** : Hebdomadaire/mensuelle (configuration OAuth2)
- **Fréquence de lecture** : Haute (validation des tokens, redirects)
- **Durée de cache** : 24 heures
- **Invalidation** : Explicite lors de Update/Delete

### ❌ User (Cache désactivé)
- **Raison** : Données volatiles (login/logout, MFA state, email verification)
- **Risque** : Données stale = failles de sécurité

### ❌ CustomConfiguration (Cache désactivé pour le moment)
- **Raison** : À évaluer selon la fréquence de modification en production

---

## 🏗️ Architecture : Decorator Pattern

```
┌────────────────────────────────────────┐
│         Controller / Handler           │
└────────────────┬───────────────────────┘
                 │ inject ITenantRepository
                 ↓
┌────────────────────────────────────────┐
│     CachedTenantRepository             │
│  (Decorator avec IMemoryCache)         │
│  - GetByIdAsync() → Cache HIT/MISS     │
│  - UpdateAsync() → Invalidate          │
└────────────────┬───────────────────────┘
                 │ wrap TenantRepository
                 ↓
┌────────────────────────────────────────┐
│        TenantRepository                │
│  (EF Core DB queries)                  │
└────────────────────────────────────────┘
```

### Enregistrement DI (ServiceCollectionExtensions.cs)

```csharp
// Tenant Repository avec cache
services.AddScoped<TenantRepository>(); // Repository concret
services.AddScoped<ITenantRepository>(sp =>
{
    var inner = sp.GetRequiredService<TenantRepository>();
    var cache = sp.GetRequiredService<IMemoryCache>();
    var logger = sp.GetRequiredService<ILogger<CachedTenantRepository>>();
    return new CachedTenantRepository(inner, cache, logger);
});
```

---

## 📊 Clés de Cache

### Structure des clés

| Type | Format | Exemple |
|------|--------|---------|
| Tenant par ID | `tenant:id:{guid}` | `tenant:id:123e4567-e89b-12d3-a456-426614174000` |
| Tenant par nom | `tenant:name:{name}` | `tenant:name:acme` |
| Tous les Tenants | `tenants:all` | `tenants:all` |
| Tenants actifs | `tenants:active` | `tenants:active` |
| Client par ID | `client:id:{guid}` | `client:id:987e6543-e21b-34d5-a678-426614174111` |
| Client par nom | `client:name:{name}` | `client:name:my-spa-client` |
| Tous les Clients | `clients:all` | `clients:all` |

---

## 🔄 Stratégie d'Invalidation

### Lors d'un **Create** (Tenant/Client)
```csharp
// Invalide les caches de liste
_cache.Remove("tenants:all");
_cache.Remove("tenants:active");
```

### Lors d'un **Update** (Tenant/Client)
```csharp
// Invalide tous les caches liés à l'entité
_cache.Remove($"tenant:id:{tenant.Id.Value}");
_cache.Remove($"tenant:name:{tenant.Name}");
_cache.Remove("tenants:all");
_cache.Remove("tenants:active");
```

### Lors d'un **Delete** (Tenant/Client)
```csharp
// Récupère l'entité AVANT suppression pour invalider par nom
var tenant = await GetByIdAsync(id);
var deleted = await _inner.DeleteAsync(id);

if (deleted && tenant != null)
{
    InvalidateTenantCaches(tenant);
}
```

---

## 📈 Performance Attendue

### Sans cache (baseline)
```
GetTenantById() → DB query (50-100ms)
GetTenantByName() → DB query (50-100ms)
GetAllTenants() → DB query (100-200ms avec 50 tenants)
```

### Avec cache (après 1ère requête)
```
GetTenantById() → Cache HIT (1-5ms) ✅ 95% plus rapide
GetTenantByName() → Cache HIT (1-5ms) ✅ 95% plus rapide
GetAllTenants() → Cache HIT (2-10ms) ✅ 90% plus rapide
```

### Scénario typique (1000 requêtes/jour)
- **1ère requête** : DB query (100ms) + mise en cache
- **999 requêtes suivantes** : Cache HIT (2ms chaque)
- **Économie** : ~98 secondes par jour = 10 heures/an de temps DB évité

---

## 🧪 Logs de Debug

Les logs de cache sont au niveau `Debug` pour éviter la verbosité en production :

```csharp
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Johodp.Infrastructure.Persistence.Repositories.CachedTenantRepository": "Debug",
      "Johodp.Infrastructure.Persistence.Repositories.CachedClientRepository": "Debug"
    }
  }
}
```

**Exemple de logs :**
```
[Debug] Cache MISS: tenant:id:123e4567-e89b-12d3-a456-426614174000
[Info] Retrieved tenant from DB: acme (100ms)
[Debug] Cache HIT: tenant:id:123e4567-e89b-12d3-a456-426614174000
[Debug] Cache HIT: tenant:id:123e4567-e89b-12d3-a456-426614174000
[Info] Cache invalidated after tenant update: 123e4567-e89b-12d3-a456-426614174000
```

---

## ⚙️ Configuration du Cache

### Limites de mémoire (optionnel)

```csharp
// ServiceCollectionExtensions.cs
services.AddMemoryCache(options =>
{
    options.SizeLimit = 1000; // Max 1000 entrées
});

// Chaque entrée a un Size = 1 (GetById) ou Size = 10 (GetAll)
var cacheOptions = new MemoryCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
    Size = 1 // Compte pour la SizeLimit
};
```

### Ajuster la durée de cache

```csharp
// CachedTenantRepository.cs
private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

// Pour modifier (par exemple, 1 heure en staging) :
private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
```

---

## 🔍 Monitoring

### Métriques à surveiller
1. **Taux de HIT** : `(Cache HIT / Total requêtes) × 100`
   - Cible : > 95% après warm-up (première heure)
2. **Latence moyenne** : Doit passer de 50-100ms à 1-5ms
3. **Mémoire consommée** : ~1-5 MB pour 100 tenants + 50 clients

### Vérifier l'efficacité du cache

```csharp
// Ajouter un compteur dans CachedTenantRepository
private static int _cacheHits = 0;
private static int _cacheMisses = 0;

public async Task<Tenant?> GetByIdAsync(TenantId id)
{
    if (_cache.TryGetValue<Tenant>(cacheKey, out var cached))
    {
        Interlocked.Increment(ref _cacheHits);
        // ...
    }
    else
    {
        Interlocked.Increment(ref _cacheMisses);
        // ...
    }
}

// Exposer via endpoint /api/diagnostics/cache-stats
```

---

## 🚀 Évolution Future : Cache Distribué

### Problème actuel (multi-instance)
```
Instance 1 : Cache Tenant A (v1) en mémoire locale
Instance 2 : Modifie Tenant A → v2 en DB
Instance 1 : Sert encore Tenant A v1 ❌ (stale jusqu'à expiration)
```

### Solution : Redis comme cache partagé

```csharp
// Future : remplacer IMemoryCache par IDistributedCache (Redis)
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "johodp:";
});

// CachedTenantRepository utiliserait IDistributedCache
private readonly IDistributedCache _cache;

public async Task<Tenant?> GetByIdAsync(TenantId id)
{
    var cacheKey = $"tenant:id:{id.Value}";
    var cachedBytes = await _cache.GetAsync(cacheKey);
    
    if (cachedBytes != null)
    {
        return JsonSerializer.Deserialize<Tenant>(cachedBytes);
    }
    // ...
}
```

---

## 📚 Références

- **Fichiers créés** :
  - `CachedTenantRepository.cs` (220 lignes)
  - `CachedClientRepository.cs` (180 lignes)
  
- **Fichiers modifiés** :
  - `ServiceCollectionExtensions.cs` (enregistrement DI)

- **Pattern utilisé** : Decorator Pattern (GoF)
- **Cache provider** : `Microsoft.Extensions.Caching.Memory.IMemoryCache`
- **Alternative future** : `Microsoft.Extensions.Caching.StackExchangeRedis.IDistributedCache`

---

## ✅ Checklist de validation

- [x] Build réussi (0 erreurs)
- [x] Decorator pattern implémenté
- [x] Invalidation explicite sur mutations
- [x] Logs de debug configurés
- [ ] Tests d'intégration (vérifier Cache HIT/MISS)
- [ ] Monitoring en production (taux de HIT)
- [ ] Évaluer CustomConfiguration pour cache futur
- [ ] Migration vers Redis si scale-out > 2 instances
