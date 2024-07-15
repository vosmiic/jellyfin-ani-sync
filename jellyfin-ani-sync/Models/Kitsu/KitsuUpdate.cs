#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Kitsu {
    public class KitsuUpdate {
        public class Attributes {
            [JsonPropertyName("status")]
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public Status? Status { get; set; }

            [JsonPropertyName("progress")] public int? Progress { get; set; }

            [JsonPropertyName("reconsuming")] public bool? Reconsuming { get; set; }

            [JsonPropertyName("reconsumeCount")] public int? ReconsumeCount { get; set; }

            [JsonPropertyName("progressedAt")] public DateTime? ProgressedAt { get; set; }

            [JsonPropertyName("startedAt")] public DateTime? StartedAt { get; set; }

            [JsonPropertyName("finishedAt")] public DateTime? FinishedAt { get; set; }
        }

        public enum Status {
            completed,
            current,
            dropped,
            on_hold,
            planned
        }

        public class Relationships {
            [JsonPropertyName("anime")] public AnimeData? AnimeData { get; set; }

            [JsonPropertyName("user")] public UserData? UserData { get; set; }
        }

        public class AnimeData {
            [JsonPropertyName("data")] public KitsuSearch.KitsuAnime? Anime { get; set; }
        }

        public class UserData {
            [JsonPropertyName("data")] public KitsuGetUser.KitsuUser? User { get; set; }
        }

        public class KitsuLibraryEntry {
            [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
            [JsonPropertyName("id")]
            public int? Id { get; set; }

            [JsonPropertyName("type")] public string? Type { get; set; }

            [JsonPropertyName("attributes")] public Attributes? Attributes { get; set; }

            [JsonPropertyName("relationships")] public Relationships? Relationships { get; set; }
        }

        public class KitsuLibraryEntryListRoot {
            [JsonPropertyName("data")] public List<KitsuLibraryEntry>? Data { get; set; }
        }

        public class KitsuLibraryEntryPostPatchRoot {
            [JsonPropertyName("data")] public KitsuLibraryEntry? Data { get; set; }
        }
    }
}