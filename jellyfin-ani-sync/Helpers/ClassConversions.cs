using System.Collections.Generic;
using System.Linq;
using jellyfin_ani_sync.Api;
using jellyfin_ani_sync.Extensions;
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

            if (aniListAnime.MediaListEntry != null) {
                anime.MyListStatus.RewatchCount = aniListAnime.MediaListEntry.RepeatCount;
                anime.MyListStatus.Status = aniListAnime.MediaListEntry.MediaListStatus.ToMalStatus();
            }

            anime.RelatedAnime = new List<RelatedAnime>();
            if (aniListAnime.Relations != null)
                foreach (AniListSearch.MediaEdge relation in aniListAnime.Relations.Media) {
                    RelatedAnime relatedAnime = new RelatedAnime {
                        Anime = ConvertAniListAnime(relation.Media)
                    };

                    switch (relation.RelationType) {
                        case AniListSearch.MediaRelation.Sequel:
                            relatedAnime.RelationType = RelationType.Sequel;
                            break;
                        case AniListSearch.MediaRelation.Side_Story:
                            relatedAnime.RelationType = RelationType.Side_Story;
                            break;
                        case AniListSearch.MediaRelation.Alternative:
                            relatedAnime.RelationType = RelationType.Alternative_Setting;
                            break;
                    }

                    anime.RelatedAnime.Add(relatedAnime);
                }

            return anime;
        }

        public static Anime ConvertKitsuAnime(KitsuSearch.KitsuAnime kitsuAnime) {
            Anime anime = new Anime {
                Id = kitsuAnime.Id,
                Title = kitsuAnime.Attributes.Titles.English,
                NumEpisodes = kitsuAnime.Attributes.EpisodeCount ?? 0,
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

            if (kitsuAnime.RelatedAnime != null && kitsuAnime.RelatedAnime.Count > 0) {
                foreach (KitsuSearch.KitsuAnime relatedAnime in kitsuAnime.RelatedAnime.Where(relatedAnime => relatedAnime.RelationType != null)) {
                    RelatedAnime convertedRelatedAnime = new RelatedAnime {
                        Anime = ConvertKitsuAnime(relatedAnime),
                        RelationType = relatedAnime.RelationType.Value.ToMalRelationType()
                    };

                    if (anime.RelatedAnime == null) {
                        anime.RelatedAnime = new List<RelatedAnime> { convertedRelatedAnime };
                    } else {
                        anime.RelatedAnime.Add(convertedRelatedAnime);
                    }
                }
            }

            return anime;
        }

        public static Anime ConvertAnnictAnime(AnnictSearch.AnnictAnime annictAnime) {
            Anime anime = new Anime {
                AlternativeId = annictAnime.Id,
                Id = int.TryParse(annictAnime.MalAnimeId, out int id) ? id : -1,
                Title = annictAnime.TitleEn,
                NumEpisodes = annictAnime.NumberOfEpisodes,
                MyListStatus = annictAnime.ViewerStatusState == AnnictSearch.AnnictMediaStatus.No_state ? null : new MyListStatus {
                    Status = annictAnime.ViewerStatusState.ToMalStatus()
                }
            };
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
                    Status = userRate.Status.ToMalStatus()
                };
            }

            if (shikimoriAnime.Related != null) {
                anime.RelatedAnime = new List<RelatedAnime>();
                foreach (ShikimoriRelated shikimoriRelated in shikimoriAnime.Related.Where(related => related.Anime != null)) {
                    RelationType? convertedAnimeRelationType = null;
                    switch (shikimoriRelated.RelationEnum) {
                        case ShikimoriRelation.Sequel:
                            convertedAnimeRelationType = RelationType.Sequel;
                            break;
                        case ShikimoriRelation.Prequel:
                            convertedAnimeRelationType = RelationType.Prequel;
                            break;
                        case ShikimoriRelation.Sidestory:
                            convertedAnimeRelationType = RelationType.Side_Story;
                            break;
                        case ShikimoriRelation.Alternativeversion:
                            convertedAnimeRelationType = RelationType.Alternative_Version;
                            break;
                    }

                    RelatedAnime relatedAnime = new RelatedAnime {
                        Anime = ConvertShikimoriAnime(shikimoriRelated.Anime),
                    };
                    if (convertedAnimeRelationType != null) {
                        relatedAnime.RelationType = convertedAnimeRelationType.Value;
                    }

                    anime.RelatedAnime.Add(relatedAnime);
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
                    RewatchCount = 0,
                    Status = simklUserEntry.Status.ToMalStatus()
                };
            }

            return anime;
        }
    }
}