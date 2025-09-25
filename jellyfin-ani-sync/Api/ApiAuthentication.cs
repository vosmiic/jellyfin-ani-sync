#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using jellyfin_ani_sync.Api.Simkl;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Models;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api {
    public class ApiAuthentication {
        private ApiName _provider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ApiAuthentication> _logger;
        private readonly string _authApiUrl;
        private readonly string _redirectUrl;
        private readonly ProviderApiAuth _providerApiAuth;
        private readonly IMemoryCache  _memoryCache;
        private readonly string _codeChallenge = "eZBLUX_JPk4~el62z_k3Q4fV5CzCYHoTz4iLKvwJ~9QTsTJNlzwveKCSYCSiSOa5zAm5Zt~cfyVM~3BuO4kQ0iYwCxPoeN0SOmBYR_C.QgnzyYE4KY-xIe4Vy1bf7_B4";

        public ApiAuthentication(ApiName provider, IHttpClientFactory httpClientFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, ILoggerFactory loggerFactory, IMemoryCache memoryCache, ProviderApiAuth? overrideProviderApiAuth = null, string? overrideRedirectUrl = null) {
            _provider = provider;

            switch (provider) {
                case ApiName.Mal:
                    _authApiUrl = "https://myanimelist.net/v1/oauth2";
                    break;
                case ApiName.AniList:
                    _authApiUrl = "https://anilist.co/api/v2/oauth";
                    break;
                case ApiName.Kitsu:
                    _authApiUrl = "https://kitsu.io/api/oauth";
                    break;
                case ApiName.Shikimori:
                    _authApiUrl = "https://shikimori.one/oauth";
                    break;
                case ApiName.Simkl:
                    _authApiUrl = "https://simkl.com/oauth";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(provider), provider, null);
            }

            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _logger = loggerFactory.CreateLogger<ApiAuthentication>();
            if (overrideProviderApiAuth != null) {
                _providerApiAuth = overrideProviderApiAuth;
            } else {
                _providerApiAuth = Plugin.Instance?.PluginConfiguration.ProviderApiAuth?.FirstOrDefault(item => item.Name == _provider) ?? throw new NullReferenceException($"No {provider} provider API auth in plugin config");
            }

            var userCallbackUrl = Plugin.Instance.PluginConfiguration.callbackUrl;
            if (overrideRedirectUrl != null && overrideRedirectUrl != "local") {
                _redirectUrl = overrideRedirectUrl + "/AniSync/authCallback";
            } else {
                if (overrideRedirectUrl is "local" && httpContextAccessor.HttpContext != null) {
                    _redirectUrl = serverApplicationHost.ListenWithHttps ? $"https://{httpContextAccessor.HttpContext.Connection.LocalIpAddress}:{serverApplicationHost.HttpsPort}/AniSync/authCallback" : $"http://{httpContextAccessor.HttpContext.Connection.LocalIpAddress}:{serverApplicationHost.HttpPort}/AniSync/authCallback";
                } else {
                    if (userCallbackUrl != null) {
                        _redirectUrl = userCallbackUrl + "/AniSync/authCallback";
                    } else if (httpContextAccessor.HttpContext != null) {
                        _redirectUrl = serverApplicationHost.ListenWithHttps ? $"https://{httpContextAccessor.HttpContext.Connection.LocalIpAddress}:{serverApplicationHost.HttpsPort}/AniSync/authCallback" : $"http://{httpContextAccessor.HttpContext.Connection.LocalIpAddress}:{serverApplicationHost.HttpPort}/AniSync/authCallback";
                    }
                }
            }
        }

        public string BuildAuthorizeRequestUrl(Guid userId) {
            string state = MemoryCacheHelper.GenerateState(_memoryCache, userId, _provider);
            switch (_provider) {
                case ApiName.Mal:
                    return $"{_authApiUrl}/authorize?response_type=code&client_id={_providerApiAuth.ClientId}&code_challenge={_codeChallenge}&redirect_uri={_redirectUrl}&state={state}";
                case ApiName.AniList:
                case ApiName.Shikimori:
                case ApiName.Simkl:
                    return $"{_authApiUrl}/authorize?response_type=code&client_id={_providerApiAuth.ClientId}&redirect_uri={_redirectUrl}&state={state}";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Get a new API auth token.
        /// </summary>
        /// <param name="httpClientFactory"></param>
        /// <param name="code">Optional auth code to generate a new token with.</param>
        /// <param name="refreshToken">Optional refresh token to refresh an existing token with.</param>
        public UserApiAuth GetToken(Guid userId, string? code = null, string? refreshToken = null) {
            var client = _httpClientFactory.CreateClient(NamedClient.Default);

            HttpContent formUrlEncodedContent;

            if (_provider == ApiName.Kitsu) {
                if (refreshToken != null) {
                    formUrlEncodedContent = new FormUrlEncodedContent(new[] {
                        new KeyValuePair<string, string>("grant_type", "refresh_token"),
                        new KeyValuePair<string, string>("refresh_token", refreshToken)
                    });
                } else {
                    formUrlEncodedContent = new FormUrlEncodedContent(new[] {
                        new KeyValuePair<string, string>("grant_type", "password"),
                        new KeyValuePair<string, string>("username", _providerApiAuth.ClientId),
                        new KeyValuePair<string, string>("password", _providerApiAuth.ClientSecret)
                    });
                }
            } else {
                if (refreshToken != null) {
                    formUrlEncodedContent = new FormUrlEncodedContent(new[] {
                        new KeyValuePair<string, string>("client_id", _providerApiAuth.ClientId),
                        new KeyValuePair<string, string>("client_secret", _providerApiAuth.ClientSecret),
                        new KeyValuePair<string, string>("grant_type", "refresh_token"),
                        new KeyValuePair<string, string>("refresh_token", refreshToken)
                    });
                } else {
                    List<KeyValuePair<string, string>> content = new List<KeyValuePair<string, string>>() {
                        new KeyValuePair<string, string>("client_id", _providerApiAuth.ClientId),
                        new KeyValuePair<string, string>("client_secret", _providerApiAuth.ClientSecret),
                        new KeyValuePair<string, string>("code", code),
                        new KeyValuePair<string, string>("grant_type", "authorization_code"),
                        new KeyValuePair<string, string>("redirect_uri", _redirectUrl)
                    };
                    if (_provider == ApiName.Mal) {
                        content.Add(new KeyValuePair<string, string>("code_verifier", _codeChallenge));
                    }

                    if (_provider == ApiName.Simkl) {
                        formUrlEncodedContent = new StringContent(JsonSerializer.Serialize(content.ToDictionary(item => item.Key, item => item.Value)));
                    } else {
                        formUrlEncodedContent = new FormUrlEncodedContent(content.ToArray());
                    }
                }
            }

            if (_provider == ApiName.Shikimori) {
                string? shikimoriAppName = ConfigHelper.GetShikimoriAppName(_logger);
                if (shikimoriAppName == null) return null;
                client.DefaultRequestHeaders.Add("User-Agent", shikimoriAppName);
            }

            var response = client.PostAsync(new Uri($"{(_provider is not ApiName.Simkl ? _authApiUrl : $"{SimklApiCalls.ApiBaseUrl}/oauth")}/token"), formUrlEncodedContent).Result;

            if (response.IsSuccessStatusCode) {
                var content = response.Content.ReadAsStream();

                StreamReader streamReader = new StreamReader(content);

                TokenResponse tokenResponse = JsonSerializer.Deserialize<TokenResponse>(streamReader.ReadToEnd());

                UserConfig? pluginConfig = Plugin.Instance.PluginConfiguration.UserConfig.FirstOrDefault(item => item.UserId == userId);

                if (pluginConfig != null) {
                    var apiAuth = pluginConfig.UserApiAuth?.FirstOrDefault(item => item.Name == _provider);

                    UserApiAuth newUserApiAuth = new UserApiAuth {
                        Name = _provider,
                        AccessToken = tokenResponse.access_token
                    };

                    if (_provider is ApiName.Mal or ApiName.Kitsu or ApiName.Shikimori) {
                        newUserApiAuth.RefreshToken = tokenResponse.refresh_token;
                    }

                    if (apiAuth != null) {
                        apiAuth.AccessToken = tokenResponse.access_token;
                        if (_provider is ApiName.Mal or ApiName.Kitsu or ApiName.Shikimori) {
                            apiAuth.RefreshToken = tokenResponse.refresh_token;
                        }
                    } else {
                        pluginConfig.AddUserApiAuth(newUserApiAuth);
                    }

                    Plugin.Instance.SaveConfiguration();
                    return newUserApiAuth;
                }

                throw new NullReferenceException("The user you are attempting to authenticate does not exist in the plugins config file");
            }

            throw new AuthenticationException($"Could not retrieve {_provider} token: " + response.StatusCode + " - " + response.ReasonPhrase);
        }

        public static string GeneratePkce() {
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
            var chars = new char[128];
            var random = new Random();

            for (var x = 0; x < chars.Length; x++) {
                chars[x] = characters[random.Next(characters.Length)];
            }

            return new string(chars);
        }
    }

    public class TokenResponse {
        public string access_token { get; set; }
        public string refresh_token { get; set; }
    }
}