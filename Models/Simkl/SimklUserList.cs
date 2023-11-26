using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Simkl;

public class SimklUserList {
    [JsonPropertyName("anime")] public List<SimklUserEntry> Entry { get; set; }
}

public class SimklUserEntry {
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyName("status")] public SimklStatus Status { get; set; }

    [JsonPropertyName("watched_episodes_count")]
    public int WatchedEpisodesCount { get; set; }

    [JsonPropertyName("total_episodes_count")]
    public int TotalEpisodesCount { get; set; }

    [JsonPropertyName("show")] public Show Show { get; set; }
    [JsonPropertyName("anime_type")] public string AnimeType { get; set; }
}