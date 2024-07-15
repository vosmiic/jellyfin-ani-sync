using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models;

public class AniListMediaList {
    public class Data {
        [JsonPropertyName("MediaListCollection")] public MediaListCollection MediaListCollection { get; set; }
    }

    public class Entries {
        [JsonPropertyName("media")] public AniListSearch.Media Media { get; set; }
        [JsonPropertyName("completedAt")] public AniListSearch.FuzzyDate CompletedAt { get; set; }
        [JsonPropertyName("progress")] public int? Progress { get; set; }
    }

    public class EntriesContainer {
        [JsonPropertyName("entries")] public List<Entries> Entries { get; set; }
    }

    public class MediaListCollection {
        [JsonPropertyName("lists")] public List<EntriesContainer> MediaList { get; set; }

        [JsonPropertyName("hasNextChunk")] public bool HasNextChunk { get; set; }
    }

    public class AniListUserMediaList {
        [JsonPropertyName("data")] public Data Data { get; set; }
    }
}