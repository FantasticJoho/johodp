namespace Johodp.Infrastructure.Persistence.Repositories;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Clients.Aggregates;
using Johodp.Domain.Clients.ValueObjects;

/// <summary>
/// Decorator pattern pour ClientRepository avec cache en mémoire.
/// Cache les entités Client (configurations OAuth2/OIDC stables).
/// Invalidation explicite lors des modifications.
/// </summary>
public class CachedClientRepository : IClientRepository
{
    private readonly IClientRepository _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedClientRepository> _logger;

    // Clés de cache structurées
    private const string CacheKeyById = "client:id:{0}";
    private const string CacheKeyByName = "client:name:{0}";
    private const string CacheKeyAll = "clients:all";
    
    // Durée de cache (24h pour configurations OAuth2 stables)
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public CachedClientRepository(
        IClientRepository inner,
        IMemoryCache cache,
        ILogger<CachedClientRepository> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Client?> GetByIdAsync(ClientId id)
    {
        var cacheKey = string.Format(CacheKeyById, id.Value);

        if (_cache.TryGetValue<Client>(cacheKey, out var cached))
        {
            _logger.LogDebug("Cache HIT: {CacheKey}", cacheKey);
            return cached;
        }

        _logger.LogDebug("Cache MISS: {CacheKey}", cacheKey);
        var client = await _inner.GetByIdAsync(id);

        if (client != null)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = 1
            };
            _cache.Set(cacheKey, client, cacheOptions);
        }

        return client;
    }

    public async Task<Client?> GetByNameAsync(string clientName)
    {
        var cacheKey = string.Format(CacheKeyByName, clientName);

        if (_cache.TryGetValue<Client>(cacheKey, out var cached))
        {
            _logger.LogDebug("Cache HIT: {CacheKey}", cacheKey);
            return cached;
        }

        _logger.LogDebug("Cache MISS: {CacheKey}", cacheKey);
        var client = await _inner.GetByNameAsync(clientName);

        if (client != null)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = 1
            };
            _cache.Set(cacheKey, client, cacheOptions);
            
            // Cache aussi par ID
            var cacheKeyById = string.Format(CacheKeyById, client.Id.Value);
            _cache.Set(cacheKeyById, client, cacheOptions);
        }

        return client;
    }

    public async Task<Client?> GetByClientNameAsync(string clientName)
    {
        // Alias de GetByNameAsync
        return await GetByNameAsync(clientName);
    }

    public async Task<IEnumerable<Client>> GetAllAsync()
    {
        if (_cache.TryGetValue<IEnumerable<Client>>(CacheKeyAll, out var cached))
        {
            _logger.LogDebug("Cache HIT: {CacheKey}", CacheKeyAll);
            return cached!; // TryGetValue garantit non-null si true
        }

        _logger.LogDebug("Cache MISS: {CacheKey}", CacheKeyAll);
        var clients = await _inner.GetAllAsync();

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration,
            Size = 10
        };
        _cache.Set(CacheKeyAll, clients, cacheOptions);

        return clients;
    }

    // ========== MUTATIONS : Invalidation du cache ==========

    public async Task<Client> AddAsync(Client client)
    {
        var result = await _inner.AddAsync(client);

        // Invalide le cache de liste
        _cache.Remove(CacheKeyAll);
        _logger.LogInformation("Cache invalidated after client creation: {ClientId}", client.Id.Value);

        return result;
    }

    public async Task<Client> UpdateAsync(Client client)
    {
        var result = await _inner.UpdateAsync(client);

        // Invalide tous les caches liés à ce client
        InvalidateClientCaches(client);
        _logger.LogInformation("Cache invalidated after client update: {ClientId}", client.Id.Value);

        return result;
    }

    public async Task<bool> DeleteAsync(ClientId id)
    {
        // Récupère le client avant suppression pour invalider le cache par nom
        var client = await GetByIdAsync(id);
        
        var result = await _inner.DeleteAsync(id);

        if (result && client != null)
        {
            InvalidateClientCaches(client);
            _logger.LogInformation("Cache invalidated after client deletion: {ClientId}", id.Value);
        }

        return result;
    }

    // ========== HELPERS : Invalidation ==========

    /// <summary>
    /// Invalide tous les caches liés à un client spécifique.
    /// </summary>
    private void InvalidateClientCaches(Client client)
    {
        var cacheKeyById = string.Format(CacheKeyById, client.Id.Value);
        var cacheKeyByName = string.Format(CacheKeyByName, client.ClientName);

        _cache.Remove(cacheKeyById);
        _cache.Remove(cacheKeyByName);
        _cache.Remove(CacheKeyAll);
    }
}
