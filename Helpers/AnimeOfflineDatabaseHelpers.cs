using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;

namespace jellyfin_ani_sync.Helpers;

public class AnimeOfflineDatabaseHelpers {
    public static async Task<OfflineDatabaseResponse> GetProviderIdsFromMetadataProvider(HttpClient httpClient, int metadataId, bool useAniDb) {
        var response = await httpClient.GetAsync($"https://relations.yuna.moe/api/ids?source={(useAniDb ? "anidb" : "anilist")}&id={metadataId}");
        StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
        string streamText = await streamReader.ReadToEndAsync();

        var deserializedResponse = JsonSerializer.Deserialize<OfflineDatabaseResponse>(streamText);
        if (deserializedResponse == null) return null;
        return deserializedResponse;
    }

    public class OfflineDatabaseResponse {
        [JsonPropertyName("anilist")] public int Anilist { get; set; }
        [JsonPropertyName("anidb")] public int AniDb { get; set; }
        [JsonPropertyName("myanimelist")] public int MyAnimeList { get; set; }
        [JsonPropertyName("kitsu")] public int Kitsu { get; set; }
    }
}