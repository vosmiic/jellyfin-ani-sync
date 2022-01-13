using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models;

public class Paging {
    [JsonPropertyName("next")] public string Next { get; set; }
}

public class UserAnimeList {
    [JsonPropertyName("data")] public List<AnimeList> Data { get; set; }
    [JsonPropertyName("paging")] public Paging Paging { get; set; }
}