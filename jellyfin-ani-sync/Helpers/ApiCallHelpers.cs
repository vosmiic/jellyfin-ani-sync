using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api;
using jellyfin_ani_sync.Api.Anilist;
using jellyfin_ani_sync.Api.Annict;
using jellyfin_ani_sync.Api.Kitsu;
using jellyfin_ani_sync.Api.Shikimori;
using jellyfin_ani_sync.Api.Simkl;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Annict;
using jellyfin_ani_sync.Models.Kitsu;
using jellyfin_ani_sync.Models.Mal;
using jellyfin_ani_sync.Models.Shikimori;
using jellyfin_ani_sync.Models.Simkl;

namespace jellyfin_ani_sync.Helpers {
    public class ApiCallHelpers {
        private MalApiCalls _malApiCalls;
        private AniListApiCalls _aniListApiCalls;
        private KitsuApiCalls _kitsuApiCalls;
        private AnnictApiCalls _annictApiCalls;
        private ShikimoriApiCalls _shikimoriApiCalls;
        private SimklApiCalls _simklApiCalls;

        /// <summary>
        /// This class attempts to combine the different APIs into a single form.
        /// </summary>
        /// <param name="malApiCalls"></param>
        /// <param name="aniListApiCalls"></param>
        /// <param name="kitsuApiCalls"></param>
        /// <param name="annictApiCalls"></param>
        /// <param name="shikimoriApiCalls"></param>
        /// <param name="simklApiCalls"></param>
        public ApiCallHelpers(MalApiCalls malApiCalls = null,
            AniListApiCalls aniListApiCalls = null,
            KitsuApiCalls kitsuApiCalls = null,
            AnnictApiCalls annictApiCalls = null,
            ShikimoriApiCalls shikimoriApiCalls = null,
            SimklApiCalls simklApiCalls = null) {
            _annictApiCalls = annictApiCalls;
            _shikimoriApiCalls = shikimoriApiCalls;
            _malApiCalls = malApiCalls;
            _aniListApiCalls = aniListApiCalls;
            _kitsuApiCalls = kitsuApiCalls;
            _simklApiCalls = simklApiCalls;
        }

        public async Task<List<Anime>> SearchAnime(string query) {
            bool updateNsfw = Plugin.Instance?.PluginConfiguration?.updateNsfw != null && Plugin.Instance.PluginConfiguration.updateNsfw;
            if (_malApiCalls != null) {
                return await _malApiCalls.SearchAnime(query, new[] { "id", "title", "alternative_titles", "num_episodes", "status" }, updateNsfw);
            }

            if (_aniListApiCalls != null) {
                List<AniListSearch.Media> animeList = await _aniListApiCalls.SearchAnime(query);
                List<Anime> convertedList = new List<Anime>();
                if (animeList != null) {
                    foreach (AniListSearch.Media media in animeList) {
                        if (!updateNsfw && media.IsAdult) continue; // Skip NSFW anime if the user doesn't want to update them
                        var synonyms = new List<string> {
                            { media.Title.Romaji },
                            { media.Title.UserPreferred }
                        };
                        synonyms.AddRange(media.Synonyms);
                        var anime = new Anime {
                            Id = media.Id,
                            Title = media.Title.English,
                            AlternativeTitles = new AlternativeTitles {
                                En = media.Title.English,
                                Ja = media.Title.Native,
                                Synonyms = synonyms
                            },
                            NumEpisodes = media.Episodes ?? 0,
                        };

                        switch (media.Status) {
                            case AniListSearch.AiringStatus.FINISHED:
                                anime.Status = AiringStatus.finished_airing;
                                break;
                            case AniListSearch.AiringStatus.RELEASING:
                                anime.Status = AiringStatus.currently_airing;
                                break;
                            case AniListSearch.AiringStatus.NOT_YET_RELEASED:
                            case AniListSearch.AiringStatus.CANCELLED:
                            case AniListSearch.AiringStatus.HIATUS:
                                anime.Status = AiringStatus.not_yet_aired;
                                break;
                        }

                        convertedList.Add(anime);
                    }
                }

                return convertedList;
            }

            if (_kitsuApiCalls != null) {
                List<KitsuSearch.KitsuAnime> animeList = await _kitsuApiCalls.SearchAnime(query);
                List<Anime> convertedList = new List<Anime>();
                if (animeList != null) {
                    foreach (KitsuSearch.KitsuAnime kitsuAnime in animeList) {
                        convertedList.Add(ClassConversions.ConvertKitsuAnime(kitsuAnime));
                    }
                }

                return convertedList;
            }

            if (_annictApiCalls != null) {
                List<AnnictSearch.AnnictAnime> animeList = await _annictApiCalls.SearchAnime(query);
                List<Anime> convertedList = new List<Anime>();
                if (animeList != null) {
                    foreach (AnnictSearch.AnnictAnime annictAnime in animeList) {
                        convertedList.Add(ClassConversions.ConvertAnnictAnime(annictAnime));
                    }
                }

                return convertedList;
            }

            if (_shikimoriApiCalls != null) {
                List<ShikimoriAnime> animeList = await _shikimoriApiCalls.SearchAnime(query);
                List<Anime> convertedList = new List<Anime>();
                if (animeList != null) {
                    foreach (ShikimoriAnime shikimoriAnime in animeList) {
                        if (!updateNsfw && shikimoriAnime.IsCensored == true) continue;
                        convertedList.Add(ClassConversions.ConvertShikimoriAnime(shikimoriAnime));
                    }
                }

                return convertedList;
            }

            return null;
        }

        public async Task<Anime> GetAnime(int id, string? alternativeId = null, bool getRelated = false) {
            if (_malApiCalls != null) {
                return await _malApiCalls.GetAnime(id, new[] { "title", "related_anime", "my_list_status", "num_episodes" });
            }

            if (_aniListApiCalls != null) {
                AniListSearch.Media anime = await _aniListApiCalls.GetAnime(id);
                if (anime == null) return null;
                Anime convertedAnime = ClassConversions.ConvertAniListAnime(anime);

                if (anime.MediaListEntry != null) {
                    convertedAnime.MyListStatus.RewatchCount = anime.MediaListEntry.RepeatCount;

                    switch (anime.MediaListEntry.MediaListStatus) {
                        case AniListSearch.MediaListStatus.Current:
                            convertedAnime.MyListStatus.Status = Status.Plan_to_watch;
                            break;
                        case AniListSearch.MediaListStatus.Completed:
                            convertedAnime.MyListStatus.Status = Status.Completed;
                            break;
                        case AniListSearch.MediaListStatus.Repeating:
                            convertedAnime.MyListStatus.Status = Status.Rewatching;
                            break;
                        case AniListSearch.MediaListStatus.Dropped:
                            convertedAnime.MyListStatus.Status = Status.Dropped;
                            break;
                        case AniListSearch.MediaListStatus.Paused:
                            convertedAnime.MyListStatus.Status = Status.On_hold;
                            break;
                        case AniListSearch.MediaListStatus.Planning:
                            convertedAnime.MyListStatus.Status = Status.Plan_to_watch;
                            break;
                    }
                }

                convertedAnime.RelatedAnime = new List<RelatedAnime>();
                foreach (AniListSearch.MediaEdge relation in anime.Relations.Media) {
                    RelatedAnime relatedAnime = new RelatedAnime {
                        Anime = ClassConversions.ConvertAniListAnime(relation.Media)
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

                    convertedAnime.RelatedAnime.Add(relatedAnime);
                }

                return convertedAnime;
            }

            if (_kitsuApiCalls != null) {
                KitsuGet.KitsuGetAnime anime = await _kitsuApiCalls.GetAnime(id);
                if (anime == null) return null;
                Anime convertedAnime = ClassConversions.ConvertKitsuAnime(anime.KitsuAnimeData);

                int? userId = await _kitsuApiCalls.GetUserId();
                if (userId != null) {
                    KitsuUpdate.KitsuLibraryEntry userAnimeStatus = await _kitsuApiCalls.GetUserAnimeStatus(userId.Value, id);
                    if (userAnimeStatus is { Attributes: { } })
                        convertedAnime.MyListStatus = new MyListStatus {
                            NumEpisodesWatched = userAnimeStatus.Attributes.Progress ?? 0,
                            IsRewatching = userAnimeStatus.Attributes.Reconsuming ?? false,
                            RewatchCount = userAnimeStatus.Attributes.ReconsumeCount ?? 0
                        };

                    if (userAnimeStatus is { Attributes: { } })
                        switch (userAnimeStatus.Attributes.Status) {
                            case KitsuUpdate.Status.completed:
                                convertedAnime.MyListStatus.Status = Status.Completed;
                                break;
                            case KitsuUpdate.Status.current:
                                convertedAnime.MyListStatus.Status = Status.Watching;
                                break;
                            case KitsuUpdate.Status.dropped:
                                convertedAnime.MyListStatus.Status = Status.Dropped;
                                break;
                            case KitsuUpdate.Status.on_hold:
                                convertedAnime.MyListStatus.Status = Status.On_hold;
                                break;
                            case KitsuUpdate.Status.planned:
                                convertedAnime.MyListStatus.Status = Status.Plan_to_watch;
                                break;
                        }

                    convertedAnime.RelatedAnime = new List<RelatedAnime>();
                    foreach (KitsuSearch.KitsuAnime relatedAnime in anime.KitsuAnimeData.RelatedAnime) {
                        RelatedAnime convertedRelatedAnime = new RelatedAnime {
                            Anime = ClassConversions.ConvertKitsuAnime(relatedAnime)
                        };

                        switch (relatedAnime.RelationType) {
                            case KitsuMediaRelationship.RelationType.sequel:
                                convertedRelatedAnime.RelationType = RelationType.Sequel;
                                break;
                            case KitsuMediaRelationship.RelationType.side_story:
                            case KitsuMediaRelationship.RelationType.full_story:
                            case KitsuMediaRelationship.RelationType.parent_story:
                                convertedRelatedAnime.RelationType = RelationType.Side_Story;
                                break;
                            case KitsuMediaRelationship.RelationType.alternative_setting:
                            case KitsuMediaRelationship.RelationType.alternative_version:
                                convertedRelatedAnime.RelationType = RelationType.Alternative_Setting;
                                break;
                            case KitsuMediaRelationship.RelationType.spinoff:
                            case KitsuMediaRelationship.RelationType.adaptation:
                                convertedRelatedAnime.RelationType = RelationType.Spin_Off;
                                break;
                            default:
                                convertedRelatedAnime.RelationType = RelationType.Other;
                                break;
                        }

                        convertedAnime.RelatedAnime.Add(convertedRelatedAnime);
                    }
                }

                return convertedAnime;
            }

            if (_annictApiCalls != null && alternativeId != null) {
                var anime = await _annictApiCalls.GetAnime(alternativeId);
                if (anime == null) return null;
                return ClassConversions.ConvertAnnictAnime(anime);
            }

            if (_shikimoriApiCalls != null && alternativeId != null) {
                var anime = await _shikimoriApiCalls.GetAnime(alternativeId, getRelated);
                if (anime == null) return null;
                Anime convertedAnime = ClassConversions.ConvertShikimoriAnime(anime);

                if (anime.Related != null) {
                    convertedAnime.RelatedAnime = new List<RelatedAnime>();
                    foreach (ShikimoriRelated shikimoriRelated in anime.Related.Where(related => related.Anime != null)) {
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
                            Anime = ClassConversions.ConvertShikimoriAnime(shikimoriRelated.Anime),
                        };
                        if (convertedAnimeRelationType != null) {
                            relatedAnime.RelationType = convertedAnimeRelationType.Value;
                        }
                        convertedAnime.RelatedAnime.Add(relatedAnime);
                    }
                }

                return convertedAnime;
            }

            return null;
        }

        public async Task<Anime> GetAnime(AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse ids, string title, bool getRelated = false) {
            if (_simklApiCalls != null) {
                SimklIdLookupMedia idLookupResult = await _simklApiCalls.GetAnimeByIdLookup(ids, title);
                if (idLookupResult != null && (idLookupResult.Ids?.Simkl != null && idLookupResult.Ids.Simkl != 0)) {
                    var detailedAnime = await _simklApiCalls.GetAnime(idLookupResult.Ids.Simkl);
                    var userList = await _simklApiCalls.GetUserAnimeList();
                    if (detailedAnime != null) {
                        return ClassConversions.ConvertSimklAnime(detailedAnime, userList?.FirstOrDefault(userEntry => userEntry.Show.Ids.Simkl == detailedAnime.Ids.Simkl));
                    }
                }
            }

            return null;
        }

        public async Task<UpdateAnimeStatusResponse> UpdateAnime(int animeId, int numberOfWatchedEpisodes, Status status,
            bool? isRewatching = null, int? numberOfTimesRewatched = null, DateTime? startDate = null, DateTime? endDate = null, string alternativeId = null, AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse ids = null, bool? isShow = null) {
            if (_malApiCalls != null) {
                return await _malApiCalls.UpdateAnimeStatus(animeId, numberOfWatchedEpisodes, status, isRewatching, numberOfTimesRewatched, startDate, endDate);
            }

            if (_aniListApiCalls != null) {
                AniListSearch.MediaListStatus anilistStatus;
                switch (status) {
                    case Status.Watching:
                        anilistStatus = AniListSearch.MediaListStatus.Current;
                        break;
                    case Status.Completed:
                        anilistStatus = isRewatching != null && isRewatching.Value ? AniListSearch.MediaListStatus.Repeating : AniListSearch.MediaListStatus.Completed;
                        break;
                    case Status.On_hold:
                        anilistStatus = AniListSearch.MediaListStatus.Paused;
                        break;
                    case Status.Dropped:
                        anilistStatus = AniListSearch.MediaListStatus.Dropped;
                        break;
                    case Status.Plan_to_watch:
                        anilistStatus = AniListSearch.MediaListStatus.Planning;
                        break;
                    default:
                        anilistStatus = AniListSearch.MediaListStatus.Current;
                        break;
                }

                if (await _aniListApiCalls.UpdateAnime(animeId, anilistStatus, numberOfWatchedEpisodes, numberOfTimesRewatched, startDate, endDate)) {
                    return new UpdateAnimeStatusResponse();
                }
            }

            if (_kitsuApiCalls != null) {
                KitsuUpdate.Status kitsuStatus;

                switch (status) {
                    case Status.Watching:
                        kitsuStatus = KitsuUpdate.Status.current;
                        break;
                    case Status.Completed:
                    case Status.Rewatching:
                        kitsuStatus = isRewatching != null && isRewatching.Value ? KitsuUpdate.Status.current : KitsuUpdate.Status.completed;
                        break;
                    case Status.On_hold:
                        kitsuStatus = KitsuUpdate.Status.on_hold;
                        break;
                    case Status.Dropped:
                        kitsuStatus = KitsuUpdate.Status.dropped;
                        break;
                    case Status.Plan_to_watch:
                        kitsuStatus = KitsuUpdate.Status.planned;
                        break;
                    default:
                        kitsuStatus = KitsuUpdate.Status.current;
                        break;
                }

                if (await _kitsuApiCalls.UpdateAnimeStatus(animeId, numberOfWatchedEpisodes, kitsuStatus, isRewatching, numberOfTimesRewatched, startDate, endDate)) {
                    return new UpdateAnimeStatusResponse();
                }
            }

            if (_annictApiCalls != null && alternativeId != null) {
                AnnictSearch.AnnictMediaStatus annictMediaStatus;

                switch (status) {
                    case Status.Watching:
                        annictMediaStatus = AnnictSearch.AnnictMediaStatus.Watching;
                        break;
                    case Status.Completed:
                    case Status.Rewatching:
                        annictMediaStatus = AnnictSearch.AnnictMediaStatus.Watched;
                        break;
                    case Status.On_hold:
                        annictMediaStatus = AnnictSearch.AnnictMediaStatus.On_hold;
                        break;
                    case Status.Dropped:
                        annictMediaStatus = AnnictSearch.AnnictMediaStatus.Stop_watching;
                        break;
                    case Status.Plan_to_watch:
                        annictMediaStatus = AnnictSearch.AnnictMediaStatus.Wanna_watch;
                        break;
                    default:
                        annictMediaStatus = AnnictSearch.AnnictMediaStatus.No_state;
                        break;
                }

                if (await _annictApiCalls.UpdateAnime(alternativeId, annictMediaStatus))
                    return new UpdateAnimeStatusResponse();
            }

            if (_shikimoriApiCalls != null && alternativeId != null) {
                ShikimoriUserRate.StatusEnum shikimoriUpdateStatus;

                switch (status) {
                    case Status.Watching:
                        shikimoriUpdateStatus = ShikimoriUserRate.StatusEnum.watching;
                        break;
                    case Status.Completed:
                        shikimoriUpdateStatus = ShikimoriUserRate.StatusEnum.completed;
                        break;
                    case Status.Rewatching:
                        shikimoriUpdateStatus = ShikimoriUserRate.StatusEnum.rewatching;
                        break;
                    case Status.On_hold:
                        shikimoriUpdateStatus = ShikimoriUserRate.StatusEnum.on_hold;
                        break;
                    case Status.Dropped:
                        shikimoriUpdateStatus = ShikimoriUserRate.StatusEnum.dropped;
                        break;
                    case Status.Plan_to_watch:
                        shikimoriUpdateStatus = ShikimoriUserRate.StatusEnum.planned;
                        break;
                    default:
                        shikimoriUpdateStatus = ShikimoriUserRate.StatusEnum.watching;
                        break;
                }

                if (await _shikimoriApiCalls.UpdateAnime(alternativeId, shikimoriUpdateStatus, numberOfWatchedEpisodes, numberOfTimesRewatched)) {
                    return new UpdateAnimeStatusResponse();
                }
            }

            if (_simklApiCalls != null && isShow != null && ids != null) {
                SimklStatus simklStatus;

                switch (status) {
                    case Status.Completed:
                        simklStatus = SimklStatus.completed;
                        break;
                    case Status.Dropped:
                        simklStatus = SimklStatus.dropped;
                        break;
                    case Status.On_hold:
                        simklStatus = SimklStatus.hold;
                        break;
                    case Status.Plan_to_watch:
                        simklStatus = SimklStatus.plantowatch;
                        break;
                    default:
                        simklStatus = SimklStatus.watching;
                        break;
                }

                if (await _simklApiCalls.UpdateAnime(animeId, simklStatus, isShow.Value, ids, numberOfWatchedEpisodes)) {
                    return new UpdateAnimeStatusResponse();
                }
            }

            return null;
        }

        public async Task<MalApiCalls.User> GetUser() {
            if (_malApiCalls != null) {
                return await _malApiCalls.GetUserInformation();
            }

            if (_aniListApiCalls != null) {
                AniListViewer.Viewer user = await _aniListApiCalls.GetCurrentUser();
                return ClassConversions.ConvertUser(user.Id, user.Name);
            }

            if (_kitsuApiCalls != null) {
                var user = await _kitsuApiCalls.GetUserId();
                if (user != null)
                    return new MalApiCalls.User {
                        Id = user.Value
                    };
            }

            if (_annictApiCalls != null) {
                AnnictViewer.AnnictViewerRoot user = await _annictApiCalls.GetCurrentUser();
                if (user != null)
                    return new MalApiCalls.User {
                        Name = user.AnnictSearchData.Viewer.username
                    };
            }

            if (_shikimoriApiCalls != null) {
                ShikimoriApiCalls.User user = await _shikimoriApiCalls.GetUserInformation();
                if (user != null) {
                    return new MalApiCalls.User {
                        Id = user.Id,
                        Name = user.Name
                    };
                }
            }

            return null;
        }

        public async Task<List<Anime>> GetAnimeList(Status status, int? userId = null) {
            if (_malApiCalls != null) {
                var malAnimeList = await _malApiCalls.GetUserAnimeList(status);
                return malAnimeList?.Select(animeList => animeList.Anime).ToList();
            }

            if (_aniListApiCalls != null && userId != null) {
                AniListSearch.MediaListStatus anilistStatus;
                switch (status) {
                    case Status.Watching:
                        anilistStatus = AniListSearch.MediaListStatus.Current;
                        break;
                    case Status.Completed:
                        anilistStatus = AniListSearch.MediaListStatus.Completed;
                        break;
                    case Status.Rewatching:
                        anilistStatus = AniListSearch.MediaListStatus.Repeating;
                        break;
                    case Status.On_hold:
                        anilistStatus = AniListSearch.MediaListStatus.Paused;
                        break;
                    case Status.Dropped:
                        anilistStatus = AniListSearch.MediaListStatus.Dropped;
                        break;
                    case Status.Plan_to_watch:
                        anilistStatus = AniListSearch.MediaListStatus.Planning;
                        break;
                    default:
                        anilistStatus = AniListSearch.MediaListStatus.Current;
                        break;
                }

                var animeList = await _aniListApiCalls.GetAnimeList(userId.Value, anilistStatus);
                List<Anime> convertedList = new List<Anime>();
                if (animeList != null) {
                    foreach (var media in animeList) {
                        int lastIndex = media.Media.SiteUrl.LastIndexOf("/", StringComparison.CurrentCulture);
                        if (lastIndex != -1) {
                            DateTime finishDate = new DateTime();
                            if (media.CompletedAt is { Year: { }, Month: { }, Day: { } }) {
                                finishDate = new DateTime(media.CompletedAt.Year.Value, media.CompletedAt.Month.Value, media.CompletedAt.Day.Value);
                            }

                            convertedList.Add(new Anime {
                                Id = media.Media.Id,
                                MyListStatus = new MyListStatus {
                                    FinishDate = finishDate.ToShortDateString(),
                                    NumEpisodesWatched = media.Progress ?? -1
                                }
                            });
                        }
                    }
                }

                return convertedList;
            }

            if (_kitsuApiCalls != null && userId != null) {
                KitsuUpdate.Status kitsuStatus;
                switch (status) {
                    case Status.Watching:
                    case Status.Rewatching:
                        kitsuStatus = KitsuUpdate.Status.current;
                        break;
                    case Status.Completed:
                        kitsuStatus = KitsuUpdate.Status.completed;
                        break;
                    case Status.On_hold:
                        kitsuStatus = KitsuUpdate.Status.on_hold;
                        break;
                    case Status.Dropped:
                        kitsuStatus = KitsuUpdate.Status.dropped;
                        break;
                    case Status.Plan_to_watch:
                        kitsuStatus = KitsuUpdate.Status.planned;
                        break;
                    default:
                        kitsuStatus = KitsuUpdate.Status.current;
                        break;
                }

                KitsuUpdate.KitsuLibraryEntryListRoot animeList = await _kitsuApiCalls.GetUserAnimeList(userId.Value, status: kitsuStatus);
                if (animeList != null) {
                    List<Anime> convertedList = new List<Anime>();
                    foreach (KitsuUpdate.KitsuLibraryEntry kitsuLibraryEntry in animeList.Data) {
                        Anime toAddAnime = new Anime();
                        if (kitsuLibraryEntry.Relationships != null &&
                            kitsuLibraryEntry.Relationships.AnimeData != null &&
                            kitsuLibraryEntry.Relationships.AnimeData.Anime != null) {
                            toAddAnime.Id = kitsuLibraryEntry.Relationships.AnimeData.Anime.Id;
                        }

                        toAddAnime.MyListStatus = new MyListStatus();
                        if (kitsuLibraryEntry.Attributes != null) {
                            toAddAnime.MyListStatus.FinishDate = kitsuLibraryEntry.Attributes.FinishedAt.ToString();
                            if (kitsuLibraryEntry.Attributes.Progress != null) {
                                toAddAnime.MyListStatus.NumEpisodesWatched = kitsuLibraryEntry.Attributes.Progress.Value;
                            }
                        }

                        convertedList.Add(toAddAnime);
                    }

                    return convertedList;
                }
            }

            if (_annictApiCalls != null) {
                AnnictSearch.AnnictMediaStatus annictMediaStatus;
                switch (status) {
                    case Status.Watching:
                    case Status.Rewatching:
                        annictMediaStatus = AnnictSearch.AnnictMediaStatus.Watching;
                        break;
                    case Status.Completed:
                        annictMediaStatus = AnnictSearch.AnnictMediaStatus.Watched;
                        break;
                    case Status.On_hold:
                        annictMediaStatus = AnnictSearch.AnnictMediaStatus.On_hold;
                        break;
                    case Status.Dropped:
                        annictMediaStatus = AnnictSearch.AnnictMediaStatus.Stop_watching;
                        break;
                    case Status.Plan_to_watch:
                        annictMediaStatus = AnnictSearch.AnnictMediaStatus.Wanna_watch;
                        break;
                    default:
                        annictMediaStatus = AnnictSearch.AnnictMediaStatus.No_state;
                        break;
                }

                List<AnnictSearch.AnnictAnime> animeList = await _annictApiCalls.GetAnimeList(annictMediaStatus);
                if (animeList != null) {
                    List<Anime> convertedList = new List<Anime>();
                    foreach (AnnictSearch.AnnictAnime annictAnime in animeList) {
                        convertedList.Add(ClassConversions.ConvertAnnictAnime(annictAnime));
                    }

                    return convertedList;
                }
            }

            if (_shikimoriApiCalls != null) {
                ShikimoriUserRate.StatusEnum shikimoriUpdateStatus;

                switch (status) {
                    case Status.Watching:
                        shikimoriUpdateStatus = ShikimoriUserRate.StatusEnum.watching;
                        break;
                    case Status.Rewatching:
                        shikimoriUpdateStatus = ShikimoriUserRate.StatusEnum.rewatching;
                        break;
                    case Status.Completed:
                        shikimoriUpdateStatus = ShikimoriUserRate.StatusEnum.completed;
                        break;
                    case Status.On_hold:
                        shikimoriUpdateStatus = ShikimoriUserRate.StatusEnum.on_hold;
                        break;
                    case Status.Dropped:
                        shikimoriUpdateStatus = ShikimoriUserRate.StatusEnum.dropped;
                        break;
                    case Status.Plan_to_watch:
                        shikimoriUpdateStatus = ShikimoriUserRate.StatusEnum.planned;
                        break;
                    default:
                        shikimoriUpdateStatus = ShikimoriUserRate.StatusEnum.watching;
                        break;
                }

                var animeList = await _shikimoriApiCalls.GetUserAnimeList(status: shikimoriUpdateStatus);
                if (animeList != null) {
                    List<Anime> convertedList = new List<Anime>();
                    foreach (ShikimoriAnime shikimoriAnime in animeList) {
                        convertedList.Add(ClassConversions.ConvertShikimoriAnime(shikimoriAnime));
                    }

                    return convertedList;
                }
            }

            return null;
        }
    }
}
