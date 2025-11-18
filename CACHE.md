# 🚀 Guide de gestion du cache - Architecture DDD et environnements distribués

Ce document décrit les bonnes pratiques de mise en cache dans le contexte d'une architecture Domain-Driven Design (DDD) et d'un environnement distribué, appliquées au projet Johodp Identity Provider.

## Table des matières

- [Principes fondamentaux](#principes-fondamentaux)
- [Cache dans l'architecture DDD](#cache-dans-larchitecture-ddd)
- [Stratégies de cache](#stratégies-de-cache)
- [Cache distribué](#cache-distribué)
- [Invalidation du cache](#invalidation-du-cache)
- [Bonnes pratiques](#bonnes-pratiques)
- [Patterns avancés](#patterns-avancés)
- [Monitoring et observabilité](#monitoring-et-observabilité)
- [Implémentation pour Johodp](#implémentation-pour-johodp)

---

## Principes fondamentaux

### Pourquoi le cache ?

1. **Performance** : Réduction de la latence des requêtes
2. **Scalabilité** : Diminution de la charge sur la base de données
3. **Disponibilité** : Tolérance aux pannes temporaires
4. **Coût** : Réduction des appels coûteux (DB, APIs externes)

### Types de cache

| Type | Usage | Durée de vie | Scope |
|------|-------|--------------|-------|
| **In-Memory** | Données fréquemment lues | Court (minutes) | Instance unique |
| **Distributed** | Données partagées entre instances | Moyen (heures) | Multi-instances |
| **CDN/Edge** | Contenu statique | Long (jours) | Global |
| **Browser** | Ressources client | Variable | Client-side |

---

## Cache dans l'architecture DDD

### 🏗️ Principe de séparation des responsabilités

Dans une architecture DDD, le cache doit respecter les frontières des couches :

```
┌─────────────────────────────────────────────┐
│           Presentation Layer                │
│  (Controllers, API, Views)                  │
│  • Cache HTTP (Response Caching)            │
│  • Output Caching                           │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│         Application Layer                   │
│  (Use Cases, Commands, Queries)             │
│  • Cache de résultats de requêtes           │
│  • Cache de DTOs                            │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│           Domain Layer                      │
│  (Aggregates, Entities, Value Objects)      │
│  ⚠️ PAS DE CACHE ICI                        │
│  • Logique métier pure                      │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│       Infrastructure Layer                  │
│  (Repositories, External Services)          │
│  • Cache de requêtes DB                     │
│  • Cache d'entités hydratées                │
│  • Cache d'API externes                     │
└─────────────────────────────────────────────┘
```

### ✅ Règles DDD pour le cache

1. **Jamais dans le Domain** : Le domain doit rester pur et sans effets de bord
2. **Infrastructure ou Application** : Le cache appartient à ces couches
3. **Abstractions** : Utiliser des interfaces pour découpler
4. **Cohérence** : Respecter les invariants du domaine

### Exemple : Repository avec cache

```csharp
// ❌ MAUVAIS - Cache dans le Domain
public class User : AggregateRoot
{
    private static Dictionary<UserId, User> _cache = new(); // NON !
    
    public static User GetById(UserId id)
    {
        if (_cache.ContainsKey(id))
            return _cache[id];
        // ...
    }
}

// ✅ BON - Cache dans l'Infrastructure
public class CachedUserRepository : IUserRepository
{
    private readonly IUserRepository _innerRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachedUserRepository> _logger;

    public CachedUserRepository(
        IUserRepository innerRepository,
        IDistributedCache cache,
        ILogger<CachedUserRepository> logger)
    {
        _innerRepository = innerRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<User?> GetByIdAsync(UserId id)
    {
        var cacheKey = $"user:{id.Value}";
        
        // Tentative de récupération depuis le cache
        var cachedUser = await _cache.GetStringAsync(cacheKey);
        if (cachedUser != null)
        {
            _logger.LogDebug("Cache HIT for user {UserId}", id.Value);
            return JsonSerializer.Deserialize<User>(cachedUser);
        }

        _logger.LogDebug("Cache MISS for user {UserId}", id.Value);
        
        // Récupération depuis la DB
        var user = await _innerRepository.GetByIdAsync(id);
        
        if (user != null)
        {
            // Mise en cache pour 15 minutes
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            };
            
            await _cache.SetStringAsync(
                cacheKey, 
                JsonSerializer.Serialize(user),
                options);
        }
        
        return user;
    }

    public async Task<User> AddAsync(User user)
    {
        var result = await _innerRepository.AddAsync(user);
        
        // Pas de mise en cache lors de l'ajout (sera fait lors de la prochaine lecture)
        
        return result;
    }

    public async Task<User> UpdateAsync(User user)
    {
        var result = await _innerRepository.UpdateAsync(user);
        
        // Invalidation du cache après mise à jour
        var cacheKey = $"user:{user.Id.Value}";
        await _cache.RemoveAsync(cacheKey);
        
        _logger.LogDebug("Cache invalidated for user {UserId}", user.Id.Value);
        
        return result;
    }
}
```

---

## Stratégies de cache

### 1. Cache-Aside (Lazy Loading)

Le pattern le plus courant : l'application vérifie le cache avant la DB.

```csharp
public async Task<UserDto> GetUserAsync(Guid userId)
{
    var cacheKey = $"user:{userId}";
    
    // 1. Vérifier le cache
    var cached = await _cache.GetAsync<UserDto>(cacheKey);
    if (cached != null)
        return cached;
    
    // 2. Cache MISS -> Charger depuis la DB
    var user = await _repository.GetByIdAsync(UserId.From(userId));
    if (user == null)
        throw new KeyNotFoundException();
    
    var dto = _mapper.Map<UserDto>(user);
    
    // 3. Mettre en cache
    await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(15));
    
    return dto;
}
```

**Avantages** :
- Simple à implémenter
- Contrôle total sur le cache
- Pas de mise en cache inutile

**Inconvénients** :
- Cache MISS penalty (latence initiale)
- Duplication de logique

### 2. Write-Through

Les écritures passent par le cache, qui met à jour la DB de manière synchrone.

```csharp
public async Task<User> UpdateUserAsync(User user)
{
    // 1. Mettre à jour la DB
    var updated = await _repository.UpdateAsync(user);
    
    // 2. Mettre à jour le cache immédiatement
    var cacheKey = $"user:{user.Id.Value}";
    await _cache.SetAsync(cacheKey, updated, TimeSpan.FromMinutes(15));
    
    return updated;
}
```

**Avantages** :
- Cache toujours cohérent avec la DB
- Pas de latence de lecture après écriture

**Inconvénients** :
- Latence d'écriture augmentée
- Cache inutile si pas de lecture

### 3. Write-Behind (Write-Back)

Les écritures sont faites dans le cache, puis la DB est mise à jour de manière asynchrone.

```csharp
public async Task UpdateUserAsync(User user)
{
    var cacheKey = $"user:{user.Id.Value}";
    
    // 1. Mise à jour immédiate du cache
    await _cache.SetAsync(cacheKey, user, TimeSpan.FromMinutes(30));
    
    // 2. Marquage pour écriture différée
    await _writeQueue.EnqueueAsync(new WriteOperation
    {
        EntityType = "User",
        EntityId = user.Id.Value,
        Operation = "Update",
        Data = user
    });
}
```

**Avantages** :
- Écritures très rapides
- Absorbe les pics de charge

**Inconvénients** :
- Complexe à implémenter
- Risque de perte de données
- Cohérence éventuelle uniquement

### 4. Refresh-Ahead

Le cache est rechargé avant expiration pour éviter les cache MISS.

```csharp
public class RefreshAheadCache<T>
{
    private readonly IDistributedCache _cache;
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _refreshThreshold = TimeSpan.FromMinutes(12);

    public async Task<T?> GetAsync(string key, Func<Task<T>> fetchFunc)
    {
        var item = await _cache.GetAsync<CachedItem<T>>(key);
        
        if (item == null)
        {
            // Cache MISS - chargement normal
            return await LoadAndCacheAsync(key, fetchFunc);
        }
        
        // Cache HIT - vérifier si refresh nécessaire
        var age = DateTime.UtcNow - item.CachedAt;
        if (age > _refreshThreshold)
        {
            // Refresh en arrière-plan
            _ = Task.Run(async () => await LoadAndCacheAsync(key, fetchFunc));
        }
        
        return item.Value;
    }

    private async Task<T> LoadAndCacheAsync(string key, Func<Task<T>> fetchFunc)
    {
        var value = await fetchFunc();
        var item = new CachedItem<T>
        {
            Value = value,
            CachedAt = DateTime.UtcNow
        };
        
        await _cache.SetAsync(key, item, _ttl);
        return value;
    }
}
```

---

## Cache distribué

### Technologies recommandées

#### Redis (Recommandé pour Johodp)

```csharp
// Configuration dans Program.cs
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "Johodp:";
});
```

**Avantages** :
- Haute performance (mémoire)
- Types de données riches (Hash, Set, Sorted Set)
- Pub/Sub pour invalidation
- Persistance optionnelle
- TTL automatique

**Cas d'usage** :
- Sessions utilisateur
- Cache de tokens
- Cache de données fréquemment lues
- Rate limiting

#### SQL Server Distributed Cache

```csharp
builder.Services.AddDistributedSqlServerCache(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.SchemaName = "dbo";
    options.TableName = "AppCache";
});
```

**Avantages** :
- Utilise l'infrastructure existante
- Transactions ACID
- Pas d'infrastructure supplémentaire

**Inconvénients** :
- Plus lent que Redis
- Charge supplémentaire sur la DB

### Configuration multi-niveaux (Hybrid)

```csharp
public class HybridCache : IDistributedCache
{
    private readonly IMemoryCache _l1Cache; // Cache local (in-memory)
    private readonly IDistributedCache _l2Cache; // Cache distribué (Redis)
    private readonly ILogger<HybridCache> _logger;

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        // L1 - Mémoire locale (très rapide)
        if (_l1Cache.TryGetValue(key, out byte[]? value))
        {
            _logger.LogTrace("L1 cache HIT for key {Key}", key);
            return value;
        }

        // L2 - Redis (rapide)
        value = await _l2Cache.GetAsync(key, token);
        if (value != null)
        {
            _logger.LogTrace("L2 cache HIT for key {Key}", key);
            
            // Peupler L1 pour les prochaines requêtes
            _l1Cache.Set(key, value, TimeSpan.FromMinutes(5));
        }
        else
        {
            _logger.LogTrace("Cache MISS for key {Key}", key);
        }

        return value;
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        // Écrire dans les deux caches
        await _l2Cache.SetAsync(key, value, options, token);
        
        _l1Cache.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        });
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        _l1Cache.Remove(key);
        await _l2Cache.RemoveAsync(key, token);
    }
}
```

---

## Invalidation du cache

### Stratégies d'invalidation

#### 1. Time-To-Live (TTL)

Le plus simple : expiration automatique.

```csharp
await _cache.SetAsync(
    "user:123", 
    userData, 
    new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
        SlidingExpiration = TimeSpan.FromMinutes(5) // Reset à chaque accès
    });
```

**Quand utiliser** :
- Données qui changent rarement
- Cohérence éventuelle acceptable
- Simplicité prioritaire

#### 2. Invalidation explicite (Event-Based)

Invalidation immédiate lors des modifications.

```csharp
public class UserService
{
    private readonly IUserRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly IDomainEventPublisher _eventPublisher;

    public async Task UpdateUserAsync(User user)
    {
        await _repository.UpdateAsync(user);
        await _cache.RemoveAsync($"user:{user.Id.Value}");
        
        // Notifier les autres instances
        await _eventPublisher.PublishAsync(new UserUpdatedEvent(user.Id.Value));
    }
}

// Handler dans chaque instance
public class UserCacheInvalidationHandler : INotificationHandler<UserUpdatedEvent>
{
    private readonly IDistributedCache _cache;
    private readonly IMemoryCache _localCache;

    public async Task Handle(UserUpdatedEvent notification, CancellationToken cancellationToken)
    {
        var cacheKey = $"user:{notification.UserId}";
        
        // Invalider le cache local et distribué
        _localCache.Remove(cacheKey);
        await _cache.RemoveAsync(cacheKey);
    }
}
```

#### 3. Cache Tags (Invalidation groupée)

Invalider plusieurs entrées liées.

```csharp
public class TaggedCacheService
{
    private readonly IDistributedCache _cache;

    public async Task SetWithTagsAsync<T>(string key, T value, string[] tags, TimeSpan ttl)
    {
        // Stocker la valeur
        await _cache.SetAsync(key, value, ttl);
        
        // Associer les tags
        foreach (var tag in tags)
        {
            var tagKey = $"tag:{tag}";
            var keys = await GetKeysForTagAsync(tagKey) ?? new List<string>();
            keys.Add(key);
            await _cache.SetAsync(tagKey, keys, ttl);
        }
    }

    public async Task InvalidateByTagAsync(string tag)
    {
        var tagKey = $"tag:{tag}";
        var keys = await GetKeysForTagAsync(tagKey);
        
        if (keys != null)
        {
            foreach (var key in keys)
            {
                await _cache.RemoveAsync(key);
            }
            await _cache.RemoveAsync(tagKey);
        }
    }
}

// Utilisation
await _taggedCache.SetWithTagsAsync(
    "user:123:profile",
    userProfile,
    new[] { "user:123", "profiles" },
    TimeSpan.FromMinutes(15));

// Invalider tous les caches liés à l'utilisateur
await _taggedCache.InvalidateByTagAsync("user:123");
```

#### 4. Pub/Sub pour environnements distribués

```csharp
// Configuration Redis Pub/Sub
public class RedisCacheInvalidator
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IMemoryCache _localCache;
    private readonly ILogger<RedisCacheInvalidator> _logger;

    public RedisCacheInvalidator(
        IConnectionMultiplexer redis,
        IMemoryCache localCache,
        ILogger<RedisCacheInvalidator> logger)
    {
        _redis = redis;
        _localCache = localCache;
        _logger = logger;

        // S'abonner au canal d'invalidation
        var subscriber = _redis.GetSubscriber();
        subscriber.Subscribe("cache:invalidate", OnInvalidationMessage);
    }

    private void OnInvalidationMessage(RedisChannel channel, RedisValue message)
    {
        var key = message.ToString();
        _localCache.Remove(key);
        _logger.LogDebug("Local cache invalidated for key {Key} via Pub/Sub", key);
    }

    public async Task InvalidateAsync(string key)
    {
        // Invalider localement
        _localCache.Remove(key);
        
        // Publier pour les autres instances
        var subscriber = _redis.GetSubscriber();
        await subscriber.PublishAsync("cache:invalidate", key);
    }
}
```

---

## Bonnes pratiques

### 1. ✅ Définir des clés de cache cohérentes

```csharp
// Pattern recommandé : {entity}:{id}:{aspect}
public static class CacheKeys
{
    public static string User(Guid userId) => $"user:{userId}";
    public static string UserProfile(Guid userId) => $"user:{userId}:profile";
    public static string UserRoles(Guid userId) => $"user:{userId}:roles";
    public static string TenantUsers(string tenantId) => $"tenant:{tenantId}:users";
    public static string ClientConfig(string clientId) => $"client:{clientId}:config";
}
```

### 2. ✅ Choisir des TTL appropriés

```csharp
public static class CacheDurations
{
    // Données quasi-statiques
    public static TimeSpan ClientConfiguration => TimeSpan.FromHours(1);
    public static TimeSpan IdentityScopes => TimeSpan.FromHours(6);
    
    // Données dynamiques
    public static TimeSpan UserProfile => TimeSpan.FromMinutes(15);
    public static TimeSpan UserRoles => TimeSpan.FromMinutes(10);
    
    // Données volatiles
    public static TimeSpan ActiveSessions => TimeSpan.FromMinutes(5);
    public static TimeSpan RateLimitCounter => TimeSpan.FromMinutes(1);
}
```

### 3. ✅ Gérer les cache stampede (thundering herd)

Éviter que plusieurs threads ne rechargent simultanément la même donnée.

```csharp
public class CacheStampedeProtection
{
    private readonly IDistributedCache _cache;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan ttl)
    {
        var cached = await _cache.GetAsync<T>(key);
        if (cached != null)
            return cached;

        // Verrou pour éviter les chargements concurrents
        await _semaphore.WaitAsync();
        try
        {
            // Double-check après acquisition du verrou
            cached = await _cache.GetAsync<T>(key);
            if (cached != null)
                return cached;

            // Chargement unique
            var value = await factory();
            await _cache.SetAsync(key, value, ttl);
            return value;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

### 4. ✅ Implémenter le Circuit Breaker

Protéger contre les pannes du cache.

```csharp
public class ResilientCache : IDistributedCache
{
    private readonly IDistributedCache _innerCache;
    private readonly ILogger<ResilientCache> _logger;
    private int _failureCount = 0;
    private DateTime? _circuitOpenedAt = null;
    private const int FailureThreshold = 5;
    private readonly TimeSpan _circuitResetTimeout = TimeSpan.FromMinutes(1);

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        if (IsCircuitOpen())
        {
            _logger.LogWarning("Cache circuit is OPEN, bypassing cache");
            return null;
        }

        try
        {
            var result = await _innerCache.GetAsync(key, token);
            ResetFailureCount();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache operation failed for key {Key}", key);
            IncrementFailureCount();
            return null; // Fallback : continuer sans cache
        }
    }

    private bool IsCircuitOpen()
    {
        if (_circuitOpenedAt == null)
            return false;

        if (DateTime.UtcNow - _circuitOpenedAt.Value > _circuitResetTimeout)
        {
            _circuitOpenedAt = null;
            _failureCount = 0;
            _logger.LogInformation("Cache circuit RESET");
            return false;
        }

        return true;
    }

    private void IncrementFailureCount()
    {
        _failureCount++;
        if (_failureCount >= FailureThreshold && _circuitOpenedAt == null)
        {
            _circuitOpenedAt = DateTime.UtcNow;
            _logger.LogWarning("Cache circuit OPENED after {Count} failures", _failureCount);
        }
    }

    private void ResetFailureCount()
    {
        if (_failureCount > 0)
        {
            _failureCount = 0;
            _logger.LogDebug("Cache failure count reset");
        }
    }
}
```

### 5. ✅ Versionner les données en cache

Éviter les problèmes lors des déploiements.

```csharp
public class VersionedCache
{
    private const string Version = "v2"; // Incrémenter lors des changements de structure

    public static string GetKey(string baseKey) => $"{Version}:{baseKey}";
}

// Utilisation
var cacheKey = VersionedCache.GetKey($"user:{userId}");
```

### 6. ✅ Monitorer les performances du cache

```csharp
public class MonitoredCache : IDistributedCache
{
    private readonly IDistributedCache _innerCache;
    private readonly ILogger<MonitoredCache> _logger;
    private long _hits = 0;
    private long _misses = 0;

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await _innerCache.GetAsync(key, token);
        stopwatch.Stop();

        if (result != null)
        {
            Interlocked.Increment(ref _hits);
            _logger.LogTrace("Cache HIT for {Key} in {ElapsedMs}ms", key, stopwatch.ElapsedMilliseconds);
        }
        else
        {
            Interlocked.Increment(ref _misses);
            _logger.LogTrace("Cache MISS for {Key} in {ElapsedMs}ms", key, stopwatch.ElapsedMilliseconds);
        }

        return result;
    }

    public double GetHitRate()
    {
        var total = _hits + _misses;
        return total == 0 ? 0 : (double)_hits / total * 100;
    }
}
```

### 7. ✅ Compresser les grandes données

```csharp
public class CompressedCache
{
    private readonly IDistributedCache _cache;

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
    {
        var json = JsonSerializer.Serialize(value);
        var bytes = Encoding.UTF8.GetBytes(json);

        // Compresser si > 1KB
        if (bytes.Length > 1024)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
            {
                await gzip.WriteAsync(bytes);
            }
            bytes = output.ToArray();
            
            // Marquer comme compressé
            await _cache.SetStringAsync($"{key}:compressed", "true", 
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
        }

        await _cache.SetAsync(key, bytes, 
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var bytes = await _cache.GetAsync(key);
        if (bytes == null)
            return default;

        var isCompressed = await _cache.GetStringAsync($"{key}:compressed");
        
        if (isCompressed == "true")
        {
            using var input = new MemoryStream(bytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            await gzip.CopyToAsync(output);
            bytes = output.ToArray();
        }

        var json = Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<T>(json);
    }
}
```

---

## Patterns avancés

### 1. Cache de requêtes CQRS

```csharp
public class CachedQueryHandler<TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : ICacheableQuery<TResult>
{
    private readonly IRequestHandler<TQuery, TResult> _innerHandler;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachedQueryHandler<TQuery, TResult>> _logger;

    public async Task<TResult> Handle(TQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = request.GetCacheKey();
        
        if (string.IsNullOrEmpty(cacheKey))
        {
            // Pas de cache pour cette requête
            return await _innerHandler.Handle(request, cancellationToken);
        }

        var cached = await _cache.GetAsync<TResult>(cacheKey);
        if (cached != null)
        {
            _logger.LogDebug("Cache HIT for query {QueryType}", typeof(TQuery).Name);
            return cached;
        }

        _logger.LogDebug("Cache MISS for query {QueryType}", typeof(TQuery).Name);
        var result = await _innerHandler.Handle(request, cancellationToken);

        await _cache.SetAsync(cacheKey, result, request.GetCacheDuration());

        return result;
    }
}

// Interface pour les requêtes cachables
public interface ICacheableQuery<TResult> : IRequest<TResult>
{
    string GetCacheKey();
    TimeSpan GetCacheDuration();
}

// Exemple de requête
public class GetUserByIdQuery : ICacheableQuery<UserDto>
{
    public Guid UserId { get; set; }

    public string GetCacheKey() => $"user:{UserId}";
    public TimeSpan GetCacheDuration() => TimeSpan.FromMinutes(15);
}
```

### 2. Cache-Aside automatique avec décorateur

```csharp
public class CachedRepository<T> : IRepository<T> where T : AggregateRoot
{
    private readonly IRepository<T> _innerRepository;
    private readonly IDistributedCache _cache;
    private readonly string _prefix;

    public CachedRepository(IRepository<T> innerRepository, IDistributedCache cache)
    {
        _innerRepository = innerRepository;
        _cache = cache;
        _prefix = typeof(T).Name.ToLowerInvariant();
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        var key = $"{_prefix}:{id}";
        var cached = await _cache.GetAsync<T>(key);
        
        if (cached != null)
            return cached;

        var entity = await _innerRepository.GetByIdAsync(id);
        
        if (entity != null)
        {
            await _cache.SetAsync(key, entity, TimeSpan.FromMinutes(15));
        }

        return entity;
    }

    public async Task<T> AddAsync(T entity)
    {
        var result = await _innerRepository.AddAsync(entity);
        // Pas de cache lors de l'ajout
        return result;
    }

    public async Task<T> UpdateAsync(T entity)
    {
        var result = await _innerRepository.UpdateAsync(entity);
        
        // Invalider le cache
        var key = $"{_prefix}:{entity.Id}";
        await _cache.RemoveAsync(key);

        return result;
    }

    public async Task DeleteAsync(Guid id)
    {
        await _innerRepository.DeleteAsync(id);
        
        var key = $"{_prefix}:{id}";
        await _cache.RemoveAsync(key);
    }
}
```

### 3. Batch Invalidation

```csharp
public class BatchInvalidationService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<BatchInvalidationService> _logger;
    private readonly Channel<string> _invalidationQueue;

    public BatchInvalidationService(IDistributedCache cache, ILogger<BatchInvalidationService> logger)
    {
        _cache = cache;
        _logger = logger;
        _invalidationQueue = Channel.CreateUnbounded<string>();
        
        // Démarrer le processeur en arrière-plan
        _ = ProcessInvalidationsAsync();
    }

    public async Task InvalidateAsync(string key)
    {
        await _invalidationQueue.Writer.WriteAsync(key);
    }

    private async Task ProcessInvalidationsAsync()
    {
        var batch = new List<string>();
        
        await foreach (var key in _invalidationQueue.Reader.ReadAllAsync())
        {
            batch.Add(key);
            
            // Traiter par lots de 100 toutes les 100ms
            if (batch.Count >= 100)
            {
                await FlushBatchAsync(batch);
                batch.Clear();
            }
            else
            {
                await Task.Delay(100);
                if (batch.Count > 0)
                {
                    await FlushBatchAsync(batch);
                    batch.Clear();
                }
            }
        }
    }

    private async Task FlushBatchAsync(List<string> keys)
    {
        _logger.LogInformation("Invalidating {Count} cache entries", keys.Count);
        
        var tasks = keys.Select(key => _cache.RemoveAsync(key));
        await Task.WhenAll(tasks);
    }
}
```

---

## Monitoring et observabilité

### Métriques à surveiller

```csharp
public class CacheMetrics
{
    private readonly ILogger<CacheMetrics> _logger;
    
    // Compteurs
    public long CacheHits { get; private set; }
    public long CacheMisses { get; private set; }
    public long CacheWrites { get; private set; }
    public long CacheInvalidations { get; private set; }
    public long CacheErrors { get; private set; }
    
    // Latences
    private readonly List<long> _getLatencies = new();
    private readonly List<long> _setLatencies = new();

    public void RecordHit(long latencyMs)
    {
        Interlocked.Increment(ref CacheHits);
        lock (_getLatencies) _getLatencies.Add(latencyMs);
    }

    public void RecordMiss(long latencyMs)
    {
        Interlocked.Increment(ref CacheMisses);
        lock (_getLatencies) _getLatencies.Add(latencyMs);
    }

    public void RecordWrite(long latencyMs)
    {
        Interlocked.Increment(ref CacheWrites);
        lock (_setLatencies) _setLatencies.Add(latencyMs);
    }

    public CacheStatistics GetStatistics()
    {
        var totalRequests = CacheHits + CacheMisses;
        var hitRate = totalRequests > 0 ? (double)CacheHits / totalRequests * 100 : 0;

        lock (_getLatencies)
        {
            var avgGetLatency = _getLatencies.Any() ? _getLatencies.Average() : 0;
            var p95GetLatency = _getLatencies.Any() 
                ? _getLatencies.OrderBy(x => x).ElementAt((int)(_getLatencies.Count * 0.95)) 
                : 0;

            return new CacheStatistics
            {
                HitRate = hitRate,
                TotalRequests = totalRequests,
                CacheHits = CacheHits,
                CacheMisses = CacheMisses,
                CacheWrites = CacheWrites,
                CacheInvalidations = CacheInvalidations,
                CacheErrors = CacheErrors,
                AverageGetLatencyMs = avgGetLatency,
                P95GetLatencyMs = p95GetLatency
            };
        }
    }
}

public class CacheStatistics
{
    public double HitRate { get; set; }
    public long TotalRequests { get; set; }
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public long CacheWrites { get; set; }
    public long CacheInvalidations { get; set; }
    public long CacheErrors { get; set; }
    public double AverageGetLatencyMs { get; set; }
    public double P95GetLatencyMs { get; set; }
}
```

### Endpoint de monitoring

```csharp
[ApiController]
[Route("api/monitoring")]
public class MonitoringController : ControllerBase
{
    private readonly CacheMetrics _cacheMetrics;

    [HttpGet("cache-stats")]
    public IActionResult GetCacheStatistics()
    {
        var stats = _cacheMetrics.GetStatistics();
        return Ok(stats);
    }
}
```

### Alertes à configurer

```yaml
Alertes cache:
  - Hit rate < 70% sur 5 minutes
  - Latence P95 > 100ms
  - Taux d'erreur > 1%
  - Pic de cache misses soudain (> 50% vs moyenne)
```

---

## Implémentation pour Johodp

### Configuration recommandée

```csharp
// Program.cs
public static void ConfigureCaching(IServiceCollection services, IConfiguration configuration)
{
    // Redis pour cache distribué
    services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = configuration.GetConnectionString("Redis");
        options.InstanceName = "Johodp:";
    });

    // Cache mémoire pour L1
    services.AddMemoryCache(options =>
    {
        options.SizeLimit = 1024; // Limite de 1024 entrées
        options.CompactionPercentage = 0.25; // Compacter 25% si limite atteinte
    });

    // Enregistrer les services de cache
    services.AddSingleton<HybridCache>();
    services.AddSingleton<CacheMetrics>();
    services.AddSingleton<RedisCacheInvalidator>();
    
    // Décorateurs de repositories
    services.Decorate<IUserRepository, CachedUserRepository>();
    services.Decorate<IClientRepository, CachedClientRepository>();
}
```

### Données à mettre en cache dans Johodp

| Donnée | TTL | Stratégie | Raison |
|--------|-----|-----------|--------|
| **User Profile** | 15 min | Cache-Aside | Lecture fréquente, changements modérés |
| **User Roles** | 10 min | Cache-Aside + Invalidation | Affecte l'autorisation |
| **Client Configuration** | 1 heure | Cache-Aside | Quasi-statique |
| **Identity Scopes** | 6 heures | Cache-Aside | Très statique |
| **Tenant Branding** | 30 min | Cache-Aside | Changements rares |
| **Active Sessions** | 5 min | Write-Through | Volatil, critique |
| **Rate Limit Counters** | 1 min | Write-Through | Temps réel |
| **OIDC Discovery** | 24 heures | Cache-Aside | Statique |

### Exemple d'implémentation complète

```csharp
// Services/CachedUserService.cs
public class CachedUserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IDistributedCache _cache;
    private readonly RedisCacheInvalidator _invalidator;
    private readonly ILogger<CachedUserService> _logger;

    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        var cacheKey = CacheKeys.User(userId);
        
        var cached = await _cache.GetAsync<UserDto>(cacheKey);
        if (cached != null)
        {
            _logger.LogDebug("Cache HIT for user {UserId}", userId);
            return cached;
        }

        _logger.LogDebug("Cache MISS for user {UserId}", userId);
        
        var user = await _userRepository.GetByIdAsync(UserId.From(userId));
        if (user == null)
            return null;

        var dto = MapToDto(user);
        
        await _cache.SetAsync(
            cacheKey, 
            dto, 
            CacheDurations.UserProfile);

        return dto;
    }

    public async Task UpdateUserAsync(UpdateUserCommand command)
    {
        var user = await _userRepository.GetByIdAsync(UserId.From(command.UserId));
        if (user == null)
            throw new KeyNotFoundException();

        // Appliquer les modifications
        user.UpdateProfile(command.FirstName, command.LastName);

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        // Invalider le cache (local + distribué)
        await _invalidator.InvalidateAsync(CacheKeys.User(command.UserId));
        
        _logger.LogInformation("User {UserId} updated and cache invalidated", command.UserId);
    }
}
```

---

## Checklist de revue de code

Lors de l'implémentation du cache, vérifier :

- [ ] Cache uniquement dans Infrastructure ou Application (jamais Domain)
- [ ] Clés de cache cohérentes et versionées
- [ ] TTL appropriés pour chaque type de donnée
- [ ] Invalidation explicite après les écritures
- [ ] Gestion des erreurs de cache (fallback)
- [ ] Circuit breaker implémenté pour la résilience
- [ ] Logging des hits/misses pour monitoring
- [ ] Compression pour les données volumineuses (> 1KB)
- [ ] Protection contre cache stampede
- [ ] Tests de cohérence multi-instances
- [ ] Pas de données sensibles non chiffrées
- [ ] Documentation des stratégies de cache

---

## Ressources

- [Redis Documentation](https://redis.io/documentation)
- [ASP.NET Core Caching](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/)
- [Cache-Aside Pattern](https://docs.microsoft.com/en-us/azure/architecture/patterns/cache-aside)
- [Distributed Caching Best Practices](https://aws.amazon.com/caching/best-practices/)
- [Martin Fowler - Cache Patterns](https://martinfowler.com/bliki/TwoHardThings.html)

---

## Mise à jour du document

**Dernière mise à jour** : 18 novembre 2025  
**Version** : 1.0  
**Auteur** : Équipe Johodp
