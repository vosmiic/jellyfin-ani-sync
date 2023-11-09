using System;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Shikimori;

public class ShikimoriRelated {
    [JsonPropertyName("relation")] public string Relation { get; set; }

    public ShikimoriRelation? RelationEnum {
        get {
            if (Relation != null && Enum.TryParse(Relation.Replace(" ", String.Empty), out ShikimoriRelation relation)) {
                return relation;
            } else {
                return null;
            }
        }
    }

    [JsonPropertyName("anime")] public ShikimoriMedia Anime { get; set; }
}

public enum ShikimoriRelation {
    Sequel,
    Sidestory,
    Alternativeversion
}