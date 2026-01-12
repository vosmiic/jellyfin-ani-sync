using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Helpers {
    public class AnimeOfflineDatabaseHelpers {
        public static async Task<OfflineDatabaseResponse> GetProviderIdsFromMetadataProvider(HttpClient httpClient, ILogger logger, int metadataId, Source source) {
            // See https://arm.haglund.dev/docs#tag/v2/operation/v2-getIds
            string baseUrl = "https://arm.haglund.dev"; // fallback value, the public API
            string customArmServerBaseUrl = Plugin.Instance?.Configuration.armServerBaseUrl;
            if (!string.IsNullOrEmpty(customArmServerBaseUrl)) {
                if (Uri.TryCreate(customArmServerBaseUrl, UriKind.Absolute, out Uri baseUri) && (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps)) {
                    baseUrl = baseUri.AbsoluteUri;
                } else {
                    logger.LogWarning($"ARM server base URL ({customArmServerBaseUrl}) could not be parsed. Confirm the URL is valid. Falling back to public API...");
                }
            }

            HttpResponseMessage response;
            try {
                response = await httpClient.GetAsync($"{baseUrl}/api/v2/ids?source={source.ToString().ToLower()}&id={metadataId}");
            } catch (Exception e) {
                logger.LogError("Error encountered while retrieving provider IDs: {Message}\n{StackTrace}", e.Message, e.StackTrace);
                return null;
            }
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
