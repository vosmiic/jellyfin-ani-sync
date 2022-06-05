using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;

namespace jellyfin_ani_sync.Helpers;

public class AnimeOfflineDatabaseHelpers {
    public static async Task<int?> GetProviderIdFromAniDbId(HttpClient httpClient, ApiName provider, int aniDbId) {
        var response = await httpClient.GetAsync($"https://relations.yuna.moe/api/ids?source=anidb&id={aniDbId}");
        StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
        string streamText = await streamReader.ReadToEndAsync();

        var deserializedResponse = JsonSerializer.Deserialize<OfflineDatabaseResponse>(streamText);
        if (deserializedResponse == null) return null;
        switch (provider) {
            case ApiName.Mal:
                return deserializedResponse.MyAnimeList;
            case ApiName.AniList:
                return deserializedResponse.Anilist;
            case ApiName.Kitsu:
                return deserializedResponse.Kitsu;
        }

        return null;
    }

    public class OfflineDatabaseResponse {
        [JsonPropertyName("anilist")] public int Anilist { get; set; }
        [JsonPropertyName("anidb")] public int AniDb { get; set; }
        [JsonPropertyName("myanimelist")] public int MyAnimeList { get; set; }
        [JsonPropertyName("kitsu")] public int Kitsu { get; set; }
    }
}