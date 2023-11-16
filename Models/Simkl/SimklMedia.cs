using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Simkl;

public class SimklMedia {
    [JsonPropertyName("title")] public string Title { get; set; }
    [JsonPropertyName("title_romaji")] public string TitleRomaji { get; set; }
    [JsonPropertyName("all_titles")] public List<string> AllTitles { get; set; }
    [JsonPropertyName("ep_count")] public int? Episodes { get; set; }
    [JsonPropertyName("ids")] public SimklIds Ids { get; set; }

    public class SimklIds {
        [JsonPropertyName("simkl_id")] public int SimklId { get; set; }
        [JsonPropertyName("tmdb")] public string Tmdb { get; set; }
    }
}