using System.Net.Mime;
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
}