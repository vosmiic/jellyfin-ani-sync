#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Shikimori; 

public class ShikimoriMedia {
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("episodes")]
    public int Episodes { get; set; }
    [JsonPropertyName("episodes_aired")]
    public int EpisodesAired { get; set; }
    [JsonPropertyName("synonyms")]
    public List<string>? Synonyms { get; set; }
    [JsonPropertyName("english")]
    public List<string>? English { get; set; }
    [JsonPropertyName("japanese")]
    public List<string>? Japanese { get; set; }
}
