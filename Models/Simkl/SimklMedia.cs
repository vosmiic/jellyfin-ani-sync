using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Simkl;

public class SimklBaseMedia {
    [JsonPropertyName("title")] public string Title { get; set; }
}

public class SimklMedia : SimklBaseMedia {
    [JsonPropertyName("ids")] public SimklIds Ids { get; set; }
    [JsonPropertyName("title_romaji")] public string TitleRomaji { get; set; }
    [JsonPropertyName("all_titles")] public List<string> AllTitles { get; set; }
    [JsonPropertyName("ep_count")] public int? Episodes { get; set; }
}

public class SimklIds {
    [JsonPropertyName("simkl_id")] public int SimklId { get; set; }
    [JsonPropertyName("tmdb")] public string Tmdb { get; set; }
}

public class SimklExtendedMedia : SimklBaseMedia {
    [JsonPropertyName("title")] public string Title { get; set; }
    [JsonPropertyName("ids")] public SimklExtendedIds Ids { get; set; }
    [JsonPropertyName("en_title")] public object EnTitle { get; set; }
    [JsonPropertyName("alt_titles")] public List<AltTitle> AllTitles { get; set; }
    [JsonPropertyName("season")] public string Season { get; set; }
    [JsonPropertyName("total_episodes")] public int? TotalEpisodes { get; set; }
    [JsonPropertyName("relations")] public List<Relation> Relations { get; set; }
}

public class SimklBaseExtendedIds {
    [JsonPropertyName("simkl")] public int Simkl { get; set; }
    [JsonPropertyName("slug")] public string Slug { get; set; }
}

public class SimklExtendedIds : SimklBaseExtendedIds {
    [JsonPropertyName("anidb")] public string Anidb { get; set; }
    [JsonPropertyName("tmdb")] public string Tmdb { get; set; }
    [JsonPropertyName("imdb")] public string Imdb { get; set; }
    [JsonPropertyName("mal")] public string Mal { get; set; }
    [JsonPropertyName("tvdbslug")] public string Tvdbslug { get; set; }
    [JsonPropertyName("anilist")] public string Anilist { get; set; }
    [JsonPropertyName("kitsu")] public string Kitsu { get; set; }
}

public class AltTitle {
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("lang")] public int Lang { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
}

public class Relation : Show {
    [JsonPropertyName("en_title")] public string EnTitle { get; set; }
    [JsonPropertyName("year")] public int Year { get; set; }
    [JsonPropertyName("poster")] public string Poster { get; set; }
    [JsonPropertyName("anime_type")] public string AnimeType { get; set; }
    [JsonPropertyName("relation_type")] public string RelationType { get; set; }
    [JsonPropertyName("is_direct")] public bool IsDirect { get; set; }
}

public class Show
{
    [JsonPropertyName("title")]
    public string Title { get; set; }
    [JsonPropertyName("ids")]
    public SimklExtendedIds Ids { get; set; }
}

public enum SimklStatus {
    watching,
    plantowatch,
    hold,
    completed,
    dropped
}