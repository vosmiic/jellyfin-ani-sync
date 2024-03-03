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
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("target_id")]
        public int AnimeId { get; set; }
        [JsonPropertyName("episodes")]
        public int Episodes { get; set; }
        [JsonPropertyName("rewatches")]
        public int Rewatches { get; set; }
        [JsonPropertyName("status")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UpdateStatus Status { get; set; }
        [JsonPropertyName("user_id")]
        public int UserId { get; set; }
        [JsonPropertyName("target_type")] public string TargetType => "Anime";
    }
    
    public enum UpdateStatus {
        planned,
        watching,
        rewatching,
        completed,
        on_hold,
        dropped
    }
}
