using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Annict; 

public class AnnictMediaList {
    public class AnnictUserMediaList
    {
        [JsonPropertyName("data")]
        public AnnictUserMediaListData AnnictSearchData { get; set; }
    }

    public class AnnictUserMediaListData {
        [JsonPropertyName("viewer")]
        public AnnictUserMediaListViewer Viewer { get; set; }
    }

    public class AnnictUserMediaListViewer {
        [JsonPropertyName("libraryEntries")]
        public AnnictUserMediaListLibraryEntries AnnictUserMediaListLibraryEntries { get; set; }
    }
    
    public class AnnictUserMediaListLibraryEntries
    {
        [JsonPropertyName("nodes")]
        public List<AnnictSearch.AnnictAnime> Nodes { get; set; }
        [JsonPropertyName("pageInfo")]
        public AnnictSearch.PageInfo PageInfo { get; set; }
    }
}