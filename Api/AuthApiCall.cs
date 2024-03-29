#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api.Anilist;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Models;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api {
    public class AuthApiCall {
        private ApiName _provider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<AuthApiCall> _logger;
        public UserConfig UserConfig { get; set; }

        public AuthApiCall(ApiName provider, IHttpClientFactory httpClientFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, ILoggerFactory loggerFactory, UserConfig userConfig) {
            _provider = provider;
            _httpClientFactory = httpClientFactory;
            _serverApplicationHost = serverApplicationHost;
            _httpContextAccessor = httpContextAccessor;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<AuthApiCall>();
            UserConfig = userConfig;
        }

        /// <summary>
        /// Make an authenticated API call.
        /// </summary>
        /// <param name="callType">The type of call to make.</param>
        /// <param name="url">The URL that you want to make the request to.</param>
        /// <param name="formUrlEncodedContent">The form data to be posted.</param>
        /// <returns>API call response.</returns>
        /// <exception cref="NullReferenceException">Authentication details not found.</exception>
        /// <exception cref="Exception">Non-200 response.</exception>
        /// <exception cref="AuthenticationException">Could not authenticate with the API.</exception>
        public async Task<HttpResponseMessage?> AuthenticatedApiCall(ApiName provider, CallType callType, string url, FormUrlEncodedContent formUrlEncodedContent = null, StringContent stringContent = null, Dictionary<string, string>? requestHeaders = null) {
            int attempts = 0;
            UserApiAuth auth;
            try {
                auth = UserConfig.UserApiAuth.FirstOrDefault(item => item.Name == provider);
                if (auth == null || auth.AccessToken == null) throw new NullReferenceException();
            } catch (NullReferenceException) {
                _logger.LogError("Could not find authentication details, please authenticate the plugin first");
                return null;
            }

            while (attempts < 2) {
                var client = _httpClientFactory.CreateClient(NamedClient.Default);
                if (requestHeaders != null) {
                    foreach (KeyValuePair<string,string> requestHeader in requestHeaders) {
                        client.DefaultRequestHeaders.Add(requestHeader.Key, requestHeader.Value);
                    }
                }

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
                HttpResponseMessage responseMessage = new HttpResponseMessage();
                try {
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
                    _logger.LogError(e.Message);
                }


                if (responseMessage.IsSuccessStatusCode) {
                    return responseMessage;
                } else {
                    if (responseMessage.StatusCode == HttpStatusCode.Unauthorized) {
                        // token has probably expired; try refreshing it
                        UserApiAuth newAuth;
                        try {
                            newAuth = new ApiAuthentication(provider, _httpClientFactory, _serverApplicationHost, _httpContextAccessor, _loggerFactory).GetToken(UserConfig.UserId, refreshToken: auth.RefreshToken);
                        } catch (Exception) {
                            _logger.LogError("Could not re-authenticate. Please manually re-authenticate the user via the AniSync configuration page");
                            return null;
                        }

                        // and then make the call again, using the new auth details
                        auth = newAuth;
                        attempts++;
                    } else {
                        _logger.LogError($"Unable to complete {provider} API call ({callType.ToString()} {url}), reason: {responseMessage.StatusCode}; {responseMessage.ReasonPhrase}");
                        return null;
                    }
                }
            }

            _logger.LogError("Unable to authenticate the API call, re-authenticate the plugin");
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