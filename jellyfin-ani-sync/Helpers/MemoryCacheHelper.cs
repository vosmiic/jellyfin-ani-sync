using System;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Models;
using Microsoft.Extensions.Caching.Memory;

namespace jellyfin_ani_sync.Helpers;

public class MemoryCacheHelper
{
    public static string GetLastCallDateTimeKey(ApiName provider) => $"{provider}LastCallDateTime";

    public static string GenerateState(IMemoryCache memoryCache, Guid userId, ApiName provider) {
        var key = Guid.NewGuid().ToString().Replace("-", "");
        memoryCache.Set(key, new StoredState {
            ApiName = provider,
            UserId = userId
        }, DateTimeOffset.Now.AddMinutes(Plugin.Instance?.Configuration.authenticationLinkExpireTimeMinutes ?? 1440));
        return key;
    }

    public static StoredState? ConsumeState(IMemoryCache memoryCache, string key) {
        StoredState? storedState = memoryCache.Get<StoredState>(key);
        memoryCache.Remove(key);
        return storedState;
    }
}