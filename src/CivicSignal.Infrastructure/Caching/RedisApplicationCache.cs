using System.Text.Json;
using CivicSignal.Application.Abstractions.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace CivicSignal.Infrastructure.Caching;

internal sealed class RedisApplicationCache(IDistributedCache cache) : IApplicationCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var json = await cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, SerializerOptions);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan absoluteExpirationRelativeToNow,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow
        };

        return cache.SetStringAsync(key, json, options, cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        return cache.RemoveAsync(key, cancellationToken);
    }
}
