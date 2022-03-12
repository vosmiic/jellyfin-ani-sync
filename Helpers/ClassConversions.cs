using System.Collections.Generic;
using jellyfin_ani_sync.Models;

namespace jellyfin_ani_sync.Helpers; 

public class ClassConversions {
    public static Anime ConvertAnime(AniListSearch.Media aniListAnime) {
        Anime anime = new Anime {
            Id = aniListAnime.Id,
            Title = aniListAnime.Title.English,
            AlternativeTitles = new AlternativeTitles {
                En = aniListAnime.Title.English,
                Ja = aniListAnime.Title.Native,
                Synonyms = new List<string> {
                    { aniListAnime.Title.Romaji },
                    { aniListAnime.Title.UserPreferred }
                }
            },

        };

        if (aniListAnime.MediaListEntry != null) {
            anime.MyListStatus = new MyListStatus {
                NumEpisodesWatched = aniListAnime.MediaListEntry.Progress,
                IsRewatching = aniListAnime.MediaListEntry.MediaListStatus == AniListSearch.MediaListStatus.Repeating
            };
        }

        return anime;
    } 
}