using System.Collections.Generic;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Kitsu;
using jellyfin_ani_sync.Models.Mal;

namespace jellyfin_ani_sync.Helpers {
    public class ClassConversions {
        public static Anime ConvertAniListAnime(AniListSearch.Media aniListAnime) {
            Anime anime = new Anime {
                Id = aniListAnime.Id,
                NumEpisodes = aniListAnime.Episodes ?? 0,
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

        public static Anime ConvertKitsuAnime(KitsuSearch.KitsuAnime kitsuAnime) {
            Anime anime = new Anime {
                Id = kitsuAnime.Id,
                Title = kitsuAnime.Attributes.Titles.English,
                AlternativeTitles = new AlternativeTitles {
                    En = kitsuAnime.Attributes.Titles.EnJp,
                    Ja = kitsuAnime.Attributes.Titles.Japanese,
                    Synonyms = new List<string> {
                        kitsuAnime.Attributes.Slug,
                        kitsuAnime.Attributes.CanonicalTitle
                    }
                },
            };

            if (kitsuAnime.Attributes.EpisodeCount != null) {
                anime.NumEpisodes = kitsuAnime.Attributes.EpisodeCount.Value;
            }

            anime.AlternativeTitles.Synonyms.AddRange(kitsuAnime.Attributes.AbbreviatedTitles);

            return anime;
        }
    }
}