#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Models;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Caching.Memory;

namespace jellyfin_ani_sync.Api {
    public class MalApiAuthentication {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly string _malAuthApiUrl = "https://myanimelist.net/v1/oauth2";
        private readonly string _redirectUrl = "http://localhost:8096/AniSync/authCallback";
        private readonly string _clientId = Secrets.MalClientId;
        private readonly string _clientSecret = Secrets.MalClientSecret;
        private readonly string _codeChallenge = "eZBLUX_JPk4~el62z_k3Q4fV5CzCYHoTz4iLKvwJ~9QTsTJNlzwveKCSYCSiSOa5zAm5Zt~cfyVM~3BuO4kQ0iYwCxPoeN0SOmBYR_C.QgnzyYE4KY-xIe4Vy1bf7_B4";

        public MalApiAuthentication(IHttpClientFactory httpClientFactory, IMemoryCache? memoryCache = null) {
            _httpClientFactory = httpClientFactory;
            if (memoryCache != null) _memoryCache = memoryCache;
        }

        public string BuildAuthorizeRequestUrl(string userId) {
            var memoryCacheOptions = new MemoryCacheEntryOptions {
                Priority = CacheItemPriority.NeverRemove,
                AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(1)
            };
            _memoryCache.Set("userId", userId, memoryCacheOptions);
            return $"{_malAuthApiUrl}/authorize?response_type=code&client_id={_clientId}&code_challenge={_codeChallenge}&redirect_uri={_redirectUrl}";
        }

        /// <summary>
        /// Get a new MAL API auth token.
        /// </summary>
        /// <param name="httpClientFactory"></param>
        /// <param name="code">Optional auth code to generate a new token with.</param>
        /// <param name="refreshToken">Optional refresh token to refresh an existing token with.</param>
        public ApiAuth GetMalToken(Guid userId, string? code = null, string? refreshToken = null) {
            var client = _httpClientFactory.CreateClient(NamedClient.Default);

            FormUrlEncodedContent formUrlEncodedContent;

            if (refreshToken != null) {
                formUrlEncodedContent = new FormUrlEncodedContent(new[] {
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret),
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", refreshToken)
                });
            } else {
                formUrlEncodedContent = new FormUrlEncodedContent(new[] {
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("code_verifier", _codeChallenge),
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("redirect_uri", _redirectUrl)
                });
            }

            var response = client.PostAsync(new Uri($"{_malAuthApiUrl}/token"), formUrlEncodedContent).Result;

            var content = response.Content.ReadAsStream();

            StreamReader streamReader = new StreamReader(content);

            TokenResponse tokenResponse = JsonSerializer.Deserialize<TokenResponse>(streamReader.ReadToEnd());

            UserConfig pluginConfig = Plugin.Instance.PluginConfiguration.UserConfig.FirstOrDefault(item => item.UserId == userId);

            var apiAuth = pluginConfig.ApiAuth?.FirstOrDefault(item => item.Name == ApiName.Mal);

            ApiAuth newApiAuth = new ApiAuth {
                Name = ApiName.Mal,
                AccessToken = tokenResponse.access_token,
                RefreshToken = tokenResponse.refresh_token
            };

            if (apiAuth != null) {
                apiAuth.AccessToken = tokenResponse.access_token;
                apiAuth.RefreshToken = tokenResponse.refresh_token;
            } else {
                pluginConfig.AddApiAuth(newApiAuth);
            }

            Plugin.Instance.SaveConfiguration();
            return newApiAuth;
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