using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Kitsu {
    public class KitsuMediaRelationship {
        public class Attributes {
            [JsonPropertyName("role")]
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public RelationType RelationType { get; set; }
        }

        public enum RelationType {
            adaptation,
            alternative_setting,
            alternative_version,
            character,
            full_story,
            other,
            parent_story,
            prequel,
            sequel,
            side_story,
            spinoff,
            summary
        }

        public class RelationshipData {
            [JsonPropertyName("type")] public string Type { get; set; }

            [JsonPropertyName("id")] public string Id { get; set; }

            [JsonPropertyName("attributes")] public Attributes Attributes { get; set; }

            [JsonPropertyName("relationships")] public Relationships Relationships { get; set; }
        }

        public class Destination {
            [JsonPropertyName("data")] public RelationshipData RelationshipData { get; set; }
        }

        public class Relationships {
            [JsonPropertyName("destination")] public Destination Destination { get; set; }
        }

        public class MediaRelationship {
            [JsonPropertyName("data")] public List<RelationshipData> Data { get; set; }

            [JsonPropertyName("included")] public List<KitsuSearch.KitsuAnime> Included { get; set; }
        }
    }
}