using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Kitsu {
    public class KitsuGetUser {
        public class KitsuUser {
            [JsonPropertyName("id")] public int Id { get; set; }
            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("type")] public string Type { get; set; }
        }

        public class KitsuUserData {
            [JsonPropertyName("attributes")] public KitsuUser KitsuUser { get; set; }

            [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
            [JsonPropertyName("id")]
            public int Id { get; set; }
        }

        public class KitsuUserRoot {
            [JsonPropertyName("data")] public List<KitsuUserData> KitsuUserList { get; set; }
        }
    }
}