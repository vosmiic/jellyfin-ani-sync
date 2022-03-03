using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace jellyfin_ani_sync.Helpers; 

public class GraphQlHelper {
    public static async Task<HttpResponseMessage> Request(HttpClient httpClient, string query, Dictionary<string, string> variables) {
        var apiCall = await httpClient.PostAsync("https://graphql.anilist.co", new StringContent(JsonSerializer.Serialize(new GraphQl {Query = query, Variables = variables}), Encoding.UTF8, "application/json"));

        return apiCall.IsSuccessStatusCode ? apiCall : null;
    }

    private class GraphQl {
        [JsonPropertyName("query")] public string Query { get; set; }
        [JsonPropertyName("variables")] public Dictionary<string, string> Variables { get; set; }
    }
}