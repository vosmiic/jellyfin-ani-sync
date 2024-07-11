using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models.Annict; 

public class AnnictViewer {
    public class AnnictViewerRoot
    {
        [JsonPropertyName("data")]
        public AnnictViewerData AnnictSearchData { get; set; }
    }

    public class AnnictViewerData {
        [JsonPropertyName("viewer")]
        public AnnictViewerDetails Viewer { get; set; }
    }

    public class AnnictViewerDetails {
        [JsonPropertyName("username")]
        public string username { get; set; }
    }
}