#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;
using jellyfin_ani_sync.Models.Shikimori;

namespace jellyfin_ani_sync.Models.Shikimori;

public class ShikimoriAnime {
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("malId")]
    public string? MalId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("episodes")]
    public int Episodes { get; set; }

    [JsonPropertyName("synonyms")]
    public List<string> Synonyms { get; set; }

    [JsonPropertyName("russian")]
    public string? Russian { get; set; }

    [JsonPropertyName("english")]
    public string? English { get; set; }

    [JsonPropertyName("japanese")]
    public string? Japanese { get; set; }

    [JsonPropertyName("isCensored")]
    public bool? IsCensored { get; set; }

    [JsonPropertyName("userRate")]
    public ShikimoriUserRate? UserRate { get; set; }

    [JsonPropertyName("related")]
    public List<ShikimoriRelated>? Related { get; set; }
}
