using System.Collections.Generic;
using System.Linq;
using jellyfin_ani_sync.Api;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Annict;
using jellyfin_ani_sync.Models.Kitsu;
using jellyfin_ani_sync.Models.Mal;
using jellyfin_ani_sync.Models.Shikimori;
using jellyfin_ani_sync.Models.Simkl;

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

        public static Anime ConvertAnnictAnime(AnnictSearch.AnnictAnime annictAnime) {
            Anime anime = new Anime {
                AlternativeId = annictAnime.Id,
                Id = int.TryParse(annictAnime.MalAnimeId, out int id) ? id : -1,
                Title = annictAnime.TitleEn,
                NumEpisodes = annictAnime.NumberOfEpisodes,
                MyListStatus = annictAnime.ViewerStatusState == AnnictSearch.AnnictMediaStatus.No_state ? null : new MyListStatus()
            };
            switch (annictAnime.ViewerStatusState) {
                case AnnictSearch.AnnictMediaStatus.Watching:
                    anime.MyListStatus.Status = Status.Watching;
                    break;
                case AnnictSearch.AnnictMediaStatus.Watched:
                    anime.MyListStatus.Status = Status.Completed;
                    break;
                case AnnictSearch.AnnictMediaStatus.On_hold:
                    anime.MyListStatus.Status = Status.On_hold;
                    break;
                case AnnictSearch.AnnictMediaStatus.Stop_watching:
                    anime.MyListStatus.Status = Status.Dropped;
                    break;
                case AnnictSearch.AnnictMediaStatus.Wanna_watch:
                    anime.MyListStatus.Status = Status.Plan_to_watch;
                    break;
            }

            return anime;
        }

        public static MalApiCalls.User ConvertUser(int id, string name) {
            return new MalApiCalls.User {
                Id = id,
                Name = name
            };
        }

        public static Anime ConvertShikimoriAnime(ShikimoriAnime shikimoriAnime) {
            Anime anime = new Anime {
                AlternativeId = shikimoriAnime.Id,
                Id = int.TryParse(shikimoriAnime.MalId ?? "", out int id) ? id : -1,
                Title = shikimoriAnime.Name,
                NumEpisodes = shikimoriAnime.Episodes,
                AlternativeTitles = new AlternativeTitles {
                    En = shikimoriAnime.English,
                    Ja = shikimoriAnime.Japanese,
                    Synonyms = shikimoriAnime.Synonyms.ToList(),
                },
            };

            if (shikimoriAnime.Russian != null) {
                anime.AlternativeTitles.Synonyms.Add(shikimoriAnime.Russian);
            }

            if (shikimoriAnime.UserRate != null) {
                var userRate = shikimoriAnime.UserRate;
                anime.MyListStatus = new MyListStatus {
                    NumEpisodesWatched = userRate.Episodes,
                    IsRewatching = userRate.Status is ShikimoriUserRate.StatusEnum.rewatching,
                    RewatchCount = userRate.Rewatches,
                };
                switch (userRate.Status) {
                    case ShikimoriUserRate.StatusEnum.completed:
                        anime.MyListStatus.Status = Status.Completed;
                        break;
                    case ShikimoriUserRate.StatusEnum.watching:
                        anime.MyListStatus.Status = Status.Watching;
                        break;
                    case ShikimoriUserRate.StatusEnum.rewatching:
                        anime.MyListStatus.Status = Status.Rewatching;
                        break;
                    case ShikimoriUserRate.StatusEnum.dropped:
                        anime.MyListStatus.Status = Status.Dropped;
                        break;
                    case ShikimoriUserRate.StatusEnum.on_hold:
                        anime.MyListStatus.Status = Status.On_hold;
                        break;
                    case ShikimoriUserRate.StatusEnum.planned:
                        anime.MyListStatus.Status = Status.Plan_to_watch;
                        break;
                }
            }

            return anime;
        }

        public static Anime ConvertSimklAnime(SimklExtendedMedia simklExtendedMedia, SimklUserEntry simklUserEntry = null) {
            Anime anime = new Anime {
                Title = simklExtendedMedia.Title,
                AlternativeTitles = new AlternativeTitles {
                    En = simklExtendedMedia.EnTitle,
                    Synonyms = simklExtendedMedia.AllTitles.Select(altTitle => altTitle.Name).ToList()
                },
            };

            if (simklExtendedMedia.EnTitle != null) {
                anime.AlternativeTitles.En = simklExtendedMedia.EnTitle;
            }
            
            if (simklExtendedMedia.TotalEpisodes != null) {
                anime.NumEpisodes = simklExtendedMedia.TotalEpisodes.Value;
            }

            if (simklUserEntry != null) {
                anime.MyListStatus = new MyListStatus {
                    NumEpisodesWatched = simklUserEntry.WatchedEpisodesCount,
                    IsRewatching = false, // simkl does not support rewatching at the moment
                    RewatchCount = 0
                };
                    
                switch (simklUserEntry.Status) {
                    case SimklStatus.watching:
                        anime.MyListStatus.Status = Status.Watching;
                        break;
                    case SimklStatus.plantowatch:
                        anime.MyListStatus.Status = Status.Plan_to_watch;
                        break;
                    case SimklStatus.hold:
                        anime.MyListStatus.Status = Status.On_hold;
                        break;
                    case SimklStatus.completed:
                        anime.MyListStatus.Status = Status.Completed;
                        break;
                    case SimklStatus.dropped:
                        anime.MyListStatus.Status = Status.Dropped;
                        break;
                }
            }

            return anime;
        }
    }
}
