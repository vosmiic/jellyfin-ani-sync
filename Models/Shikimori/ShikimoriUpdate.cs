using System;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Shikimori; 

public class ShikimoriUpdate {
    public class UpdateBody
    {
        [JsonPropertyName("user_rate")]
        public UserRate UserRate { get; set; }
    }

    public class UserRate
    {
        [JsonPropertyName("target_id")]
        public string AnimeId { get; set; }

        [JsonPropertyName("episodes")]
        public int Episodes { get; set; }

        [JsonPropertyName("rewatches")]
        public int Rewatches { get; set; }

        [JsonPropertyName("status")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ShikimoriUserRate.StatusEnum Status { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("target_type")]
        public string TargetType => "Anime";
    }
}
