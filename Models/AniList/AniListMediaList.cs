using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models;

public class AniListMediaList {
    public class Data {
        [JsonPropertyName("Page")] public Page Page { get; set; }
    }

    public class MediaList {
        [JsonPropertyName("media")] public AniListSearch.Media Media { get; set; }
        [JsonPropertyName("completedAt")] public AniListSearch.FuzzyDate CompletedAt { get; set; }
    }

    public class Page {
        [JsonPropertyName("mediaList")] public List<MediaList> MediaList { get; set; }

        [JsonPropertyName("pageInfo")] public AniListSearch.PageInfo PageInfo { get; set; }
    }

    public class AniListUserMediaList {
        [JsonPropertyName("data")] public Data Data { get; set; }
    }
}