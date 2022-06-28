using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Mal {
    public class Paging {
        [JsonPropertyName("next")] public string Next { get; set; }
    }

    public enum Status {
        [EnumMember(Value = "watching")] Watching,
        [EnumMember(Value = "completed")] Completed,
        [EnumMember(Value = "on_hold")] On_hold,
        [EnumMember(Value = "dropped")] Dropped,
        [EnumMember(Value = "plan_to_watch")] Plan_to_watch,
        Rewatching
    }

    public class ListStatus {
        [JsonPropertyName("status")] public Status Status { get; set; }
        [JsonPropertyName("score")] public int Score { get; set; }

        [JsonPropertyName("num_episodes_watched")]
        public int NumEpisodesWatched { get; set; }

        [JsonPropertyName("is_rewatching")] public bool IsRewatching { get; set; }
        [JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; set; }
    }

    public class UserAnimeListData {
        [JsonPropertyName("node")] public Anime Anime { get; set; }
        [JsonPropertyName("list_status")] public MyListStatus ListStatus { get; set; }
    }

    public class UserAnimeList {
        [JsonPropertyName("data")] public List<UserAnimeListData> Data { get; set; }
        [JsonPropertyName("paging")] public Paging Paging { get; set; }
    }
}