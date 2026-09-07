namespace RoomRaah.Application.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan ttl);
    Task RemoveAsync(string key);
    Task RemoveByPrefixAsync(string prefix);
    Task<long> IncrementAsync(string key, TimeSpan window);
}
