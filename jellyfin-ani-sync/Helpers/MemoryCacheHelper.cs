using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Interfaces;
using jellyfin_ani_sync.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Helpers;

public class MemoryCacheHelper
{
    private static string GetLastCallDateTimeKey(ApiName provider) => $"{provider}LastCallDateTime";
    internal const string XRateLimitRemainingHeader = "X-RateLimit-Remaining";
    internal const string XRateLimitResetHeader = "X-RateLimit-Reset";

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

    /// <summary>
    /// Check if we can make an API call to the provided API provider without running into rate limiting restrictions.
    /// </summary>
    /// <param name="provider">Provider to check the rate limiting of.</param>
    /// <param name="logger">Instance of <see cref="ILogger"/></param>
    /// <param name="memoryCache">Instance of <see cref="IMemoryCache"/></param>
    /// <param name="delayer">Instance of <see cref="IAsyncDelayer"/></param>
    public static async Task CheckRateLimiting(ApiName provider, ILogger logger, IMemoryCache memoryCache, IAsyncDelayer delayer) {
        DateTime lastCallDateTime = memoryCache.Get<DateTime>(GetLastCallDateTimeKey(provider));
        if (lastCallDateTime != default)
        {
            logger.LogDebug($"({provider}) Delaying API call to prevent 429 (too many requests)...");
            await delayer.Delay(DateTime.UtcNow.Subtract(lastCallDateTime));
        }
    }
    
    /// <summary>
    /// Set the rate limiting for the provided API provider.
    /// </summary>
    /// <param name="provider">Provider to set the rate limiting for</param>
    /// <param name="memoryCache">Instance of <see cref="IMemoryCache"/></param>
    /// <param name="backOff">Optional back off time. Default of 5 seconds.</param>
    public static void SetRateLimitingForProvider(ApiName provider, IMemoryCache memoryCache, TimeSpan? backOff = null) =>
        memoryCache.Set(GetLastCallDateTimeKey(provider), DateTime.UtcNow, backOff ?? TimeSpan.FromSeconds(5));

    /// <summary>
    /// Check the provided response message for rate limiting headers and add appropriate rate limiting for the provided API provider.
    /// </summary>
    /// <param name="responseMessage">Response message to search the headers of.</param>
    /// <param name="logger">Instance of <see cref="ILogger"/></param>
    /// <param name="provider">Provider to set the rate limiting for.</param>
    /// <param name="memoryCache">Instance of <see cref="IMemoryCache"/></param>
    public static void CheckResponseHeadersForRateLimiting(HttpResponseMessage responseMessage, ILogger logger, ApiName provider, IMemoryCache memoryCache) {
        if (responseMessage.Headers.TryGetValues(XRateLimitRemainingHeader, out IEnumerable<string> remainingValues)) {
            if (int.TryParse(remainingValues.First(), out int parsedRemaining) && parsedRemaining <= 0) {
                logger.LogWarning("({ApiName}) Reached rate limit. Implementing a generous backoff before running the next request.", provider);
                SetRateLimitingForProvider(provider, memoryCache, TimeSpan.FromMinutes(2));
            }
        }

        if (responseMessage.Headers.TryGetValues(XRateLimitResetHeader, out IEnumerable<string> resetValues)) {
            if (DateTimeOffset.TryParse(resetValues.First(), out DateTimeOffset resetTime) && resetTime > DateTimeOffset.UtcNow) {
                TimeSpan delay = resetTime - DateTimeOffset.UtcNow;
                logger.LogDebug($"({provider}) Rate limit until {resetTime}. Waiting {delay.TotalSeconds} seconds.");
                SetRateLimitingForProvider(provider, memoryCache, delay);
            }
        }
    }
}