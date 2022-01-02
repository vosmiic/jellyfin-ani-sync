using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using jellyfin_ani_sync.Configuration;
using MediaBrowser.Common.Net;

namespace jellyfin_ani_sync.Api {
    public class ControllerFunctions {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _malAuthApiUrl = "https://myanimelist.net/v1/oauth2";
        private readonly string _redirectUrl = "http://localhost:8096/AniSync/authCallback";
        private readonly string _clientId = "";
        private readonly string _clientSecret = "";
        private readonly string _codeChallenge = GeneratePkce();

        public ControllerFunctions(IHttpClientFactory httpClientFactory) {
            _httpClientFactory = httpClientFactory;
        }

        public string BuildAuthorizeRequestUrl() {
            return $"{_malAuthApiUrl}/authorize?response_type=code&client_id={_clientId}&code_challenge={_codeChallenge}&redirect_uri={_redirectUrl}";
        }

        public async void GetMalToken(string code) {
            var client = _httpClientFactory.CreateClient(NamedClient.Default);

            var formContent = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("code_verifier", _codeChallenge),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", _redirectUrl)
            });

            var xd = await client.PostAsync(new Uri($"{_malAuthApiUrl}/token"), formContent);

            TokenResponse tokenResponse = JsonSerializer.Deserialize<TokenResponse>(await xd.Content.ReadAsStringAsync());
            foreach (var apiAuth in Plugin.Instance.PluginConfiguration.ApiAuth) {
                if (apiAuth.Name == ApiName.Mal) {
                    apiAuth.AccessToken = tokenResponse.access_token;
                    apiAuth.RefreshToken = tokenResponse.refresh_token;
                    Plugin.Instance.SaveConfiguration();
                    break;
                }
            }
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