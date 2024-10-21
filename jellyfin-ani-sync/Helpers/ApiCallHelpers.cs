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
    public class ApiCallHelpers : IApiCallHelpers {
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

                return AniListSearchAnimeConvertedList(animeList, updateNsfw);
            }

            if (_kitsuApiCalls != null) {
                List<KitsuSearch.KitsuAnime> animeList = await _kitsuApiCalls.SearchAnime(query);

                return KitsuSearchAnimeConvertedList(animeList);
            }

            if (_annictApiCalls != null) {
                List<AnnictSearch.AnnictAnime> animeList = await _annictApiCalls.SearchAnime(query);

                return AnnictSearchAnimeConvertedList(animeList);
            }

            if (_shikimoriApiCalls != null) {
                List<ShikimoriAnime> animeList = await _shikimoriApiCalls.SearchAnime(query);

                return ShikimoriSearchAnimeConvertedList(animeList, updateNsfw);
            }

            return null;
        }

        internal static List<Anime> ShikimoriSearchAnimeConvertedList(List<ShikimoriAnime> animeList, bool updateNsfw) {
            List<Anime> convertedList = new List<Anime>();
            if (animeList != null) {
                foreach (ShikimoriAnime shikimoriAnime in animeList) {
                    if (!updateNsfw && shikimoriAnime.IsCensored == true) continue;
                    convertedList.Add(ClassConversions.ConvertShikimoriAnime(shikimoriAnime));
                }
            }

            return convertedList;
        }

        internal static List<Anime> AnnictSearchAnimeConvertedList(List<AnnictSearch.AnnictAnime> animeList) {
            List<Anime> convertedList = new List<Anime>();
            if (animeList != null) {
                foreach (AnnictSearch.AnnictAnime annictAnime in animeList) {
                    convertedList.Add(ClassConversions.ConvertAnnictAnime(annictAnime));
                }
            }

            return convertedList;
        }

        internal static List<Anime> KitsuSearchAnimeConvertedList(List<KitsuSearch.KitsuAnime> animeList) {
            List<Anime> convertedList = new List<Anime>();
            if (animeList != null) {
                foreach (KitsuSearch.KitsuAnime kitsuAnime in animeList) {
                    convertedList.Add(ClassConversions.ConvertKitsuAnime(kitsuAnime));
                }
            }

            return convertedList;
        }

        internal List<Anime> AniListSearchAnimeConvertedList(List<AniListSearch.Media> animeList, bool updateNsfw) {
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

        public async Task<Anime> GetAnime(int id, string? alternativeId = null, bool getRelated = false) {
            if (_malApiCalls != null) {
                return await _malApiCalls.GetAnime(id, new[] { "title", "related_anime", "my_list_status", "num_episodes" });
            }

            if (_aniListApiCalls != null) {
                AniListSearch.Media anime = await _aniListApiCalls.GetAnime(id);
                if (anime == null) return null;

                return ClassConversions.ConvertAniListAnime(anime);
            }

            if (_kitsuApiCalls != null) {
                KitsuGet.KitsuGetAnime anime = await _kitsuApiCalls.GetAnime(id);
                if (anime == null) return null;
                Anime convertedAnime = ClassConversions.ConvertKitsuAnime(anime.KitsuAnimeData);

                convertedAnime.MyListStatus = await GetConvertedKitsuUserList(id);

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

                return ClassConversions.ConvertShikimoriAnime(anime);
            }

            return null;
        }

        /// <summary>
        /// Gets a users list from the Kitsu API and converts it to a <see cref="MyListStatus"/> object instance.
        /// </summary>
        /// <param name="id">The ID of the anime to get the user status of.</param>
        /// <returns>The converted user list status of the anime.</returns>
        internal async Task<MyListStatus> GetConvertedKitsuUserList(int id) {
            int? userId = await _kitsuApiCalls.GetUserId();

            MyListStatus userList = new MyListStatus();

            if (userId != null) {
                KitsuUpdate.KitsuLibraryEntry userAnimeStatus = await _kitsuApiCalls.GetUserAnimeStatus(userId.Value, id);
                if (userAnimeStatus is { Attributes: { } })
                    userList = new MyListStatus {
                        NumEpisodesWatched = userAnimeStatus.Attributes.Progress ?? 0,
                        IsRewatching = userAnimeStatus.Attributes.Reconsuming ?? false,
                        RewatchCount = userAnimeStatus.Attributes.ReconsumeCount ?? 0
                    };

                if (userAnimeStatus is { Attributes: { } })
                    switch (userAnimeStatus.Attributes.Status) {
                        case KitsuUpdate.Status.completed:
                            userList.Status = Status.Completed;
                            break;
                        case KitsuUpdate.Status.current:
                            userList.Status = Status.Watching;
                            break;
                        case KitsuUpdate.Status.dropped:
                            userList.Status = Status.Dropped;
                            break;
                        case KitsuUpdate.Status.on_hold:
                            userList.Status = Status.On_hold;
                            break;
                        case KitsuUpdate.Status.planned:
                            userList.Status = Status.Plan_to_watch;
                            break;
                    }
            }

            return userList;
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
                    case Status.Rewatching:
                        anilistStatus = AniListSearch.MediaListStatus.Repeating;
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