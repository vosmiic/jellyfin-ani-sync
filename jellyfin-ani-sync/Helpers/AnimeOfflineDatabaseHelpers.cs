using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;

namespace jellyfin_ani_sync.Helpers {
    public class AnimeOfflineDatabaseHelpers {
        public static async Task<OfflineDatabaseResponse> GetProviderIdsFromMetadataProvider(HttpClient httpClient, int metadataId, Source source) {
            // See https://arm.haglund.dev/docs#tag/v2/operation/v2-getIds
            // TODO: make URL user-configurable to allow self-hosting the server.
            var response = await httpClient.GetAsync($"https://arm.haglund.dev/api/v2/ids?source={source.ToString().ToLower()}&id={metadataId}");
            StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
            string streamText = await streamReader.ReadToEndAsync();

            var deserializedResponse = JsonSerializer.Deserialize<OfflineDatabaseResponse>(streamText);
            if (deserializedResponse == null) return null;
            return deserializedResponse;
        }

        public class OfflineDatabaseResponse {
            [JsonPropertyName("anilist")] public int? Anilist { get; set; }
            [JsonPropertyName("anidb")] public int? AniDb { get; set; }
            [JsonPropertyName("myanimelist")] public int? MyAnimeList { get; set; }
            [JsonPropertyName("kitsu")] public int? Kitsu { get; set; }
        }
        
        public enum Source {
            Anidb,
            Anilist,
            Myanimelist,
            Kitsu
        }

        public static Source MapFromApiName(ApiName apiName) {
            switch (apiName) {
                case ApiName.Mal:
                    return Source.Myanimelist;
                case ApiName.AniList:
                    return Source.Anilist;
                case ApiName.Kitsu:
                    return Source.Kitsu;
                default:
                    throw new ArgumentOutOfRangeException(nameof(apiName), apiName, null);
            }
        }
    }
}
