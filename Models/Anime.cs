using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models;

public class MainPicture {
    [JsonPropertyName("medium")] public string Medium { get; set; }
    [JsonPropertyName("large")] public string Large { get; set; }
}

public class AlternativeTitles {
    [JsonPropertyName("synonyms")] public List<string> Synonyms { get; set; }
    [JsonPropertyName("en")] public string En { get; set; }
    [JsonPropertyName("ja")] public string Ja { get; set; }
}

public class Genre {
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
}

public class MyListStatus {
    [JsonPropertyName("status")] public string Status { get; set; }
    [JsonPropertyName("score")] public int Score { get; set; }

    [JsonPropertyName("num_episodes_watched")]
    public int NumEpisodesWatched { get; set; }

    [JsonPropertyName("is_rewatching")] public bool IsRewatching { get; set; }
    [JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; set; }
    [JsonPropertyName("start_date")] public string StartDate { get; set; }
    [JsonPropertyName("finish_date")] public string FinishDate { get; set; }
}

public class StartSeason {
    [JsonPropertyName("year")] public int Year { get; set; }
    [JsonPropertyName("season")] public string Season { get; set; }
}

public class Broadcast {
    [JsonPropertyName("day_of_the_week")] public string DayOfTheWeek { get; set; }
    [JsonPropertyName("start_time")] public string StartTime { get; set; }
}

public class Studio {
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
}

public class Anime {
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; }
    [JsonPropertyName("main_picture")] public MainPicture MainPicture { get; set; }

    [JsonPropertyName("alternative_titles")]
    public AlternativeTitles AlternativeTitles { get; set; }

    [JsonPropertyName("start_date")] public string StartDate { get; set; }
    [JsonPropertyName("end_date")] public string EndDate { get; set; }
    [JsonPropertyName("synopsis")] public string Synopsis { get; set; }
    [JsonPropertyName("mean")] public double Mean { get; set; }
    [JsonPropertyName("rank")] public int Rank { get; set; }
    [JsonPropertyName("popularity")] public int Popularity { get; set; }
    [JsonPropertyName("num_list_users")] public int NumListUsers { get; set; }

    [JsonPropertyName("num_scoring_users")]
    public int NumScoringUsers { get; set; }

    [JsonPropertyName("nsfw")] public string Nsfw { get; set; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; set; }
    [JsonPropertyName("media_type")] public string MediaType { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; }
    [JsonPropertyName("genres")] public List<Genre> Genres { get; set; }
    [JsonPropertyName("my_list_status")] public MyListStatus MyListStatus { get; set; }
    [JsonPropertyName("num_episodes")] public int NumEpisodes { get; set; }
    [JsonPropertyName("start_season")] public StartSeason StartSeason { get; set; }
    [JsonPropertyName("broadcast")] public Broadcast Broadcast { get; set; }
    [JsonPropertyName("source")] public string Source { get; set; }

    [JsonPropertyName("average_episode_duration")]
    public int AverageEpisodeDuration { get; set; }

    [JsonPropertyName("rating")] public string Rating { get; set; }
    [JsonPropertyName("studios")] public List<Studio> Studios { get; set; }
}

public class AnimeList {
    [JsonPropertyName("node")] public Anime Anime { get; set; }
}

public class SearchAnimeResponse {
    [JsonPropertyName("data")] public List<AnimeList> Data { get; set; }
}