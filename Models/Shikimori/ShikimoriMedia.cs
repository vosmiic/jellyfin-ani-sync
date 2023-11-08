#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Shikimori; 

public class ShikimoriMedia {
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("episodes")]
    public int Episodes { get; set; }
    [JsonPropertyName("episodes_aired")]
    public int EpisodesAired { get; set; }
    [JsonPropertyName("synonyms")] public List<string> RelatedAnime { get; set; } = new();
    [JsonPropertyName("english")]
    public string? English { get; set; }
    [JsonPropertyName("japanese")]
    public string? Japanese { get; set; }
}