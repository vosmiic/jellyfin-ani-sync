using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Interfaces;
using jellyfin_ani_sync.Models;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Helpers {
    public class GraphQlHelper {
        public static async Task<HttpResponseMessage> Request(HttpClient httpClient, string query, Dictionary<string, object> variables = null) {
            var call = await httpClient.PostAsync("https://graphql.anilist.co", new StringContent(JsonSerializer.Serialize(new GraphQl { Query = query, Variables = variables }), Encoding.UTF8, "application/json"));

            return call.IsSuccessStatusCode ? call : null;
        }

        public static async Task<HttpResponseMessage> AuthenticatedRequest(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, IAsyncDelayer delayer, UserConfig userConfig, string query, ApiName provider, Dictionary<string, object> variables = null) {
            AuthApiCall authApiCall = new AuthApiCall(httpClientFactory, serverApplicationHost, httpContextAccessor, loggerFactory, memoryCache, delayer, userConfig);
            string url = string.Empty;
            switch (provider) {
                case ApiName.AniList:
                    url = "https://graphql.anilist.co";
                    break;
                case ApiName.Annict:
                    url = "https://api.annict.com/graphql";
                    break;
            }
            var call = await authApiCall.AuthenticatedApiCall(provider, AuthApiCall.CallType.POST, url, stringContent: new StringContent(JsonSerializer.Serialize(new GraphQl { Query = query, Variables = variables }), Encoding.UTF8, "application/json"));

            return call is { IsSuccessStatusCode: true } ? call : null;
        }
        
        public static async Task<T> DeserializeRequest<T>(HttpClient httpClient, string query, Dictionary<string, object> variables) {
            var response = await GraphQlHelper.Request(httpClient, query, variables);
            if (response != null) {
                StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
                return JsonSerializer.Deserialize<T>(await streamReader.ReadToEndAsync());
            }

            return default;
        }

        private class GraphQl {
            [JsonPropertyName("query")] public string Query { get; set; }
            [JsonPropertyName("variables")] public Dictionary<string, object> Variables { get; set; }
        }
    }
}