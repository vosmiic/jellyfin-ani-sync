using System;
using System.Text.Json.Serialization;
using jellyfin_ani_sync.Models.Shikimori;

namespace jellyfin_ani_sync.Models.Shikimori;

public class ShikimoriRelated {
    [JsonPropertyName("anime")]
    public ShikimoriAnime? Anime { get; set; }

    [JsonPropertyName("relationEn")]
    public string Relation { get; set; }

    public ShikimoriRelation? RelationEnum {
        get {
            if (Relation != null && Enum.TryParse(Relation.Replace(" ", String.Empty), out ShikimoriRelation relation)) {
                return relation;
            } else {
                return null;
            }
        }
    }
}

public enum ShikimoriRelation {
    Sequel,
    Prequel,
    Sidestory,
    Alternativeversion
}
