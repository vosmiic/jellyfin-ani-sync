using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Mal {
    public class UpdateAnimeStatusResponse : ListStatus {
        [JsonPropertyName("finish_date")] public string FinishDate { get; set; }
        [JsonPropertyName("priority")] public int Priority { get; set; }

        [JsonPropertyName("num_times_rewatched")]
        public int NumTimesRewatched { get; set; }

        [JsonPropertyName("rewatch_value")] public int RewatchValue { get; set; }
        [JsonPropertyName("tags")] public List<object> Tags { get; set; }
        [JsonPropertyName("comments")] public string Comments { get; set; }
    }
}