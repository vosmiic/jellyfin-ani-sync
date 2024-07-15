using System.Text.Json.Serialization;

namespace jellyfin_ani_sync.Models {
    public class AniListViewer {
        public class Viewer {
            [JsonPropertyName("id")] public int Id { get; set; }
            [JsonPropertyName("name")] public string Name { get; set; }
        }

        public class AniListViewerData {
            [JsonPropertyName("Viewer")] public Viewer Viewer { get; set; }
        }

        public class AniListGetViewer {
            [JsonPropertyName("data")] public AniListViewerData Data { get; set; }
        }
    }
}