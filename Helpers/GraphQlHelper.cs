using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api;
using jellyfin_ani_sync.Configuration;

namespace jellyfin_ani_sync.Helpers; 

public class GraphQlHelper {
    public static async Task<HttpResponseMessage> Request(HttpClient httpClient, string query, Dictionary<string, string> variables = null) {
        var call = await httpClient.PostAsync("https://graphql.anilist.co", new StringContent(JsonSerializer.Serialize(new GraphQl {Query = query, Variables = variables}), Encoding.UTF8, "application/json"));

        return call.IsSuccessStatusCode ? call : null;
    }

    public static async Task<HttpResponseMessage> AuthenticatedRequest(ApiCall apiCall, string query, Dictionary<string, string> variables = null) {
        var call = await apiCall.AuthenticatedApiCall(ApiName.AniList, MalApiCalls.CallType.POST, "https://graphql.anilist.co", stringContent: new StringContent(JsonSerializer.Serialize(new GraphQl {Query = query, Variables = variables}), Encoding.UTF8, "application/json"));

        return call.IsSuccessStatusCode ? call : null;
    }

    private class GraphQl {
        [JsonPropertyName("query")] public string Query { get; set; }
        [JsonPropertyName("variables")] public Dictionary<string, string> Variables { get; set; }
    }
}