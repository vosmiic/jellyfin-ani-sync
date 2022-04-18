using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Models;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Helpers {
    public class GraphQlHelper {
        public static async Task<HttpResponseMessage> Request(HttpClient httpClient, string query, Dictionary<string, string> variables = null) {
            var call = await httpClient.PostAsync("https://graphql.anilist.co", new StringContent(JsonSerializer.Serialize(new GraphQl { Query = query, Variables = variables }), Encoding.UTF8, "application/json"));

            return call.IsSuccessStatusCode ? call : null;
        }

        public static async Task<HttpResponseMessage> AuthenticatedRequest(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, UserConfig userConfig, string query, Dictionary<string, string> variables = null) {
            AuthApiCall authApiCall = new AuthApiCall(ApiName.AniList, httpClientFactory, serverApplicationHost, httpContextAccessor, loggerFactory, userConfig);
            var xd = JsonSerializer.Serialize(new GraphQl { Query = query, Variables = variables });
            var call = await authApiCall.AuthenticatedApiCall(ApiName.AniList, AuthApiCall.CallType.POST, "https://graphql.anilist.co", stringContent: new StringContent(JsonSerializer.Serialize(new GraphQl { Query = query, Variables = variables }), Encoding.UTF8, "application/json"));

            return call is { IsSuccessStatusCode: true } ? call : null;
        }

        private class GraphQl {
            [JsonPropertyName("query")] public string Query { get; set; }
            [JsonPropertyName("variables")] public Dictionary<string, string> Variables { get; set; }
        }
    }
}