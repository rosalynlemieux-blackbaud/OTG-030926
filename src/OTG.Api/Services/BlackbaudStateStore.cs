using Microsoft.Extensions.Caching.Memory;

namespace OTG.Api.Services;

public sealed class BlackbaudStateStore(IMemoryCache memoryCache) : IBlackbaudStateStore
{
    private readonly object syncRoot = new();

    public void Store(string state, string? origin, TimeSpan ttl)
    {
        memoryCache.Set(GetKey(state), origin, ttl);
    }

    public bool TryConsume(string state, out string? origin)
    {
        lock (syncRoot)
        {
            var key = GetKey(state);
            if (!memoryCache.TryGetValue<string?>(key, out origin))
            {
                origin = null;
                return false;
            }

            memoryCache.Remove(key);
            return true;
        }
    }

    private static string GetKey(string state) => $"blackbaud-oauth-state:{state}";
}
