#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Interfaces;
using jellyfin_ani_sync.Models;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api {
    public class AuthApiCall (
        IHttpClientFactory httpClientFactory,
        IServerApplicationHost serverApplicationHost,
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IMemoryCache memoryCache,
        IAsyncDelayer delayer,
        UserConfig userConfig) {
        private readonly ILogger<AuthApiCall> _logger = loggerFactory.CreateLogger<AuthApiCall>();
        private UserConfig UserConfig { get; } = userConfig;
        private const int DefaultTimeoutSeconds = 5;
        private const int TimeoutIncrementMultiplier = 2;

        /// <summary>
        /// Make an authenticated API call.
        /// </summary>
        /// <param name="provider">Provider to make the authenticated API call to.</param>
        /// <param name="callType">The type of call to make.</param>
        /// <param name="url">The URL that you want to make the request to.</param>
        /// <param name="formUrlEncodedContent">The form data to be posted.</param>
        /// <param name="stringContent">Content in string format to include int the API call.</param>
        /// <param name="requestHeaders">Request headers to include in the API call.</param>
        /// <returns>API call response.</returns>
        /// <exception cref="NullReferenceException">Authentication details not found.</exception>
        /// <exception cref="Exception">Non-200 response.</exception>
        public async Task<HttpResponseMessage?> AuthenticatedApiCall(ApiName provider, CallType callType, string url, FormUrlEncodedContent? formUrlEncodedContent = null, StringContent? stringContent = null, Dictionary<string, string>? requestHeaders = null) {
            int attempts = 0;
            int timeoutSeconds = DefaultTimeoutSeconds;
            UserApiAuth? auth = UserConfig.UserApiAuth?.FirstOrDefault(item => item.Name == provider);
            if (auth == null) {
                _logger.LogError("({ApiName}) Could not find authentication details, please authenticate the plugin first", provider);
                return null;
            }

            var client = httpClientFactory.CreateClient(NamedClient.Default);
            DateTime lastCallDateTime = memoryCache.Get<DateTime>(MemoryCacheHelper.GetLastCallDateTimeKey(provider));
            if (lastCallDateTime != default)
            {
                _logger.LogDebug("({ApiName}) Delaying API call to prevent 429 (too many requests)...", provider);
                await delayer.Delay(DateTime.UtcNow.Subtract(lastCallDateTime));
            }
            while (attempts < 3) {
                if (requestHeaders != null) {
                    foreach (KeyValuePair<string,string> requestHeader in requestHeaders) {
                        client.DefaultRequestHeaders.Add(requestHeader.Key, requestHeader.Value);
                    }
                }

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
                HttpResponseMessage responseMessage = new HttpResponseMessage();
                try {
                    memoryCache.Set(MemoryCacheHelper.GetLastCallDateTimeKey(provider), DateTime.UtcNow, TimeSpan.FromSeconds(5));
                    switch (callType) {
                        case CallType.GET:
                            responseMessage = await client.GetAsync(url);
                            break;
                        case CallType.POST:
                            responseMessage = await client.PostAsync(url, formUrlEncodedContent != null ? formUrlEncodedContent : stringContent);
                            break;
                        case CallType.PATCH:
                            responseMessage = await client.PatchAsync(url, formUrlEncodedContent != null ? formUrlEncodedContent : stringContent);
                            break;
                        case CallType.PUT:
                            responseMessage = await client.PutAsync(url, formUrlEncodedContent);
                            break;
                        case CallType.DELETE:
                            responseMessage = await client.DeleteAsync(url);
                            break;
                        default:
                            responseMessage = await client.GetAsync(url);
                            break;
                    }
                } catch (Exception e) {
                    _logger.LogError("({ApiName} Could not make authenticated API call: {EMessage}", provider, e.Message);
                }


                if (responseMessage.IsSuccessStatusCode) {
                    return responseMessage;
                }

                switch (responseMessage.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        // token has probably expired; try refreshing it
                        UserApiAuth newAuth;
                        try {
                            newAuth = await new ApiAuthentication(provider, httpClientFactory, serverApplicationHost, httpContextAccessor, loggerFactory, memoryCache).GetToken(UserConfig.UserId, refreshToken: auth.RefreshToken);
                        } catch (Exception e) {
                            _logger.LogError("({ApiName}) Could not re-authenticate: {EMessage}, please manually re-authenticate the user via the Ani-Sync configuration page", provider, e.Message);
                            return null;
                        }

                        // and then make the call again, using the new auth details
                        auth = newAuth;
                        attempts++;
                        break;
                    case HttpStatusCode.TooManyRequests:
                        _logger.LogWarning("({ApiName}) API rate limit exceeded, retrying the API call again in {TimeoutSeconds} seconds...", provider, timeoutSeconds);
                        await delayer.Delay(TimeSpan.FromSeconds(timeoutSeconds));
                        timeoutSeconds *= TimeoutIncrementMultiplier;
                        attempts++;
                        break;
                    default:
                        _logger.LogError("({ApiName}) Unable to complete {Provider} API call ({ToString} {Url}), reason: {ResponseMessageStatusCode}, content: \n{ReadAsStringAsync}", provider, provider, callType.ToString(), url, responseMessage.StatusCode, await responseMessage.Content.ReadAsStringAsync());
                        return null;
                }
            }

            _logger.LogError("({ApiName}) Unable to authenticate the API call, re-authenticate the plugin", provider);
            return null;
        }
        
        public enum CallType {
            GET,
            POST,
            PATCH,
            PUT,
            DELETE
        }
    }
}