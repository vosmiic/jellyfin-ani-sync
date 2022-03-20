using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Kitsu; 

public class KitsuUser {
    public class Attributes
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public class KitsuUserData
    {
        [JsonPropertyName("attributes")]
        public Attributes Attributes { get; set; }
    }

    public class KitsuUserRoot
    {
        [JsonPropertyName("data")]
        public List<KitsuUserData> KitsuUserList { get; set; }
    }
}