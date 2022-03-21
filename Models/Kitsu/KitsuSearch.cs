using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Kitsu; 

public class KitsuSearch {
        public class Titles {
        [JsonPropertyName("en")]
        public string English { get; set; }

        [JsonPropertyName("en_jp")]
        public string EnJp { get; set; }

        [JsonPropertyName("ja_jp")]
        public string Japanese { get; set; }
    }

    public class Attributes {
        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("titles")]
        public Titles Titles { get; set; }

        [JsonPropertyName("canonicalTitle")]
        public string CanonicalTitle { get; set; }

        [JsonPropertyName("abbreviatedTitles")]
        public List<string> AbbreviatedTitles { get; set; }

        [JsonPropertyName("episodeCount")]
        public int EpisodeCount { get; set; }
    }

    public class Links {
        [JsonPropertyName("self")]
        public string Self { get; set; }

        [JsonPropertyName("related")]
        public string Related { get; set; }

        [JsonPropertyName("first")]
        public string First { get; set; }

        [JsonPropertyName("next")]
        public string Next { get; set; }

        [JsonPropertyName("last")]
        public string Last { get; set; }
    }

    public class KitsuAnime {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("id")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int Id { get; set; }

        [JsonPropertyName("attributes")]
        public Attributes Attributes { get; set; }

        [JsonPropertyName("relationships")]
        public Relationships Relationships { get; set; }
    }

    public class MediaRelationships {
        [JsonPropertyName("links")]
        public Links Links { get; set; }

        [JsonPropertyName("data")]
        public List<KitsuAnime> Data { get; set; }
    }

    public class Relationships {
        [JsonPropertyName("mediaRelationships")]
        public MediaRelationships MediaRelationships { get; set; }
    }

    public class KitsuSearchMedia {
        [JsonPropertyName("data")]
        public List<KitsuAnime> KitsuSearchData { get; set; }
        
        [JsonPropertyName("links")]
        public Links Links { get; set; }
    }
}