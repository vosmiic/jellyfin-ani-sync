using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Kitsu {
    public class KitsuGet {
        public class KitsuGetAnime {
            [JsonPropertyName("data")] public KitsuSearch.KitsuAnime KitsuAnimeData { get; set; }
            /*[JsonPropertyName("included")]
            public List<KitsuSearch.KitsuAnime> RelatedAnime { get; set; }*/
        }
    }
}