using System.Text.Json;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Interfaces;
using StackExchange.Redis;

namespace RoomRaah.Infrastructure.Caching;

/// <summary>
/// Every method swallows Redis failures and behaves as a cache miss. A cache that
/// is down must slow the product, never break it — the test suite runs the cache
/// tests a second time with Redis stopped and expects them to pass.
/// </summary>
public class CacheService : ICacheService
{
    private readonly IConnectionMultiplexer _connection;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IConnectionMultiplexer connection, ILogger<CacheService> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var value = await Database().StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                _logger.LogDebug("Cache {Result} for key {CacheKey}", "miss", key);
                return default;
            }

            _logger.LogDebug("Cache {Result} for key {CacheKey}", "hit", key);
            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for key {CacheKey}; treating as a miss.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
    {
        try
        {
            await Database().StringSetAsync(key, JsonSerializer.Serialize(value), ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write failed for key {CacheKey}.", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await Database().KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache eviction failed for key {CacheKey}.", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        try
        {
            var keys = new List<RedisKey>();

            foreach (var endpoint in _connection.GetEndPoints())
            {
                var server = _connection.GetServer(endpoint);
                await foreach (var key in server.KeysAsync(pattern: prefix + "*"))
                {
                    keys.Add(key);
                }
            }

            if (keys.Count > 0)
            {
                await Database().KeyDeleteAsync(keys.ToArray());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache eviction failed for prefix {CachePrefix}.", prefix);
        }
    }

    public async Task<long> IncrementAsync(string key, TimeSpan window)
    {
        try
        {
            var database = Database();
            var count = await database.StringIncrementAsync(key);

            if (count == 1)
            {
                await database.KeyExpireAsync(key, window);
            }

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Counter increment failed for key {CacheKey}; the limit is not enforced.", key);
            return 0;
        }
    }

    private IDatabase Database() => _connection.GetDatabase();
}
