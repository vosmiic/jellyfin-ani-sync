using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using jellyfin_ani_sync.Models;
using MediaBrowser.Common.Net;

namespace jellyfin_ani_sync.Api.Anilist;

public class AniListApiCalls {
    private HttpClient _httpClient;
    private string endpoint = "https://graphql.anilist.co";

    public AniListApiCalls(IHttpClientFactory httpClientFactory) {
        _httpClient = httpClientFactory.CreateClient(NamedClient.Default);
    }

    public async Task<AniListSearch.AniListSearchMedia> SearchAnime(string searchString) {
        string query = @"query ($search: String!) {
        Page(perPage: 100, page: 1) {
            pageInfo {
                total
                    perPage
                currentPage
                    lastPage
                hasNextPage
            }
            media(search: $search) {
                id
                title {
                    romaji
                        english
                    native
                        userPreferred
                }
            }
        }
    }
    ";
        var garphql = new GraphQl {
            Query = query,
            Variables = new Dictionary<string, string> {
                { "search", searchString }
            }
        };
        var apiCall = await _httpClient.PostAsync(endpoint, new StringContent(JsonSerializer.Serialize(garphql), Encoding.UTF8, "application/json"));
        StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
        return JsonSerializer.Deserialize<AniListSearch.AniListSearchMedia>(await streamReader.ReadToEndAsync());
    }

    public class GraphQl {
        [JsonPropertyName("query")] public string Query { get; set; }
        [JsonPropertyName("variables")] public Dictionary<string, string> Variables { get; set; }
    }
}