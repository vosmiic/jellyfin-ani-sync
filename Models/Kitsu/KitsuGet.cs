using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Kitsu; 

public class KitsuGet {
    public class KitsuGetAnime {
        [JsonPropertyName("data")]
        public KitsuSearch.KitsuAnime KitsuAnimeData { get; set; }
    }
}