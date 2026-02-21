using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Extensions;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Interfaces;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Mal;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api.Anilist {
    public class AniListApiCalls : IApiCallHelpers {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _memoryCache;
        private readonly IAsyncDelayer _delayer;
        private readonly UserConfig _userConfig;
        private static readonly int PageSize = 50;
        private static readonly int ChunkSize = 500;


        public AniListApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, IAsyncDelayer delayer, UserConfig userConfig = null) {
            _httpClientFactory = httpClientFactory;
            _loggerFactory = loggerFactory;
            _serverApplicationHost = serverApplicationHost;
            _httpContextAccessor = httpContextAccessor;
            _httpClient = httpClientFactory.CreateClient(NamedClient.Default);
            _memoryCache = memoryCache;
            _delayer = delayer;
            _userConfig = userConfig;
        }

        /// <summary>
        /// Search for an anime based upon its name.
        /// </summary>
        /// <param name="searchString">The name to search for.</param>
        /// <returns>List of anime.</returns>
        public async Task<List<AniListSearch.Media>> SearchAnime(string searchString) {
            string query = @"query ($search: String!, $type: MediaType, $perPage: Int, $page: Int) {
            Page(perPage: $perPage, page: $page) {
                pageInfo {
                    total
                        perPage
                    currentPage
                        lastPage
                    hasNextPage
                }
                media(search: $search, type: $type) {
                    id
                    title {
                        romaji
                            english
                        native
                            userPreferred
                    },
                    synonyms
                    episodes
                    status
                    isAdult
                }
            }
        }
        ";

            int page = 1;
            Dictionary<string, object> variables = new Dictionary<string, object> {
                { "search", searchString },
                { "type", "ANIME" },
                { "perPage", PageSize.ToString() },
                { "page", page.ToString() }
            };

            AniListSearch.AniListSearchMedia result = await GraphQlHelper.DeserializeRequest<AniListSearch.AniListSearchMedia>(_httpClient, query, variables);

            if (result != null) {
                if (result.Data.Page.PageInfo.HasNextPage) {
                    // impose a hard limit of 10 pages
                    while (page < 10) {
                        page++;
                        variables["page"] = page.ToString();

                        AniListSearch.AniListSearchMedia nextPageResult = await GraphQlHelper.DeserializeRequest<AniListSearch.AniListSearchMedia>(_httpClient, query, variables);

                        result.Data.Page.Media = result.Data.Page.Media.Concat(nextPageResult.Data.Page.Media).ToList();
                        if (!nextPageResult.Data.Page.PageInfo.HasNextPage) {
                            break;
                        }

                        // sleeping thread so we dont hammer the API
                        Thread.Sleep(1000);
                    }
                }

                return result.Data.Page.Media;
            }

            return null;
        }

        public async Task<Anime> GetAnime(int id, string alternativeId = null, bool getRelated = false) {
            AniListSearch.Media anime = await GetAnime(id);
            if (anime == null) return null;

            return ClassConversions.ConvertAniListAnime(anime);
        }

        public async Task<UpdateAnimeStatusResponse> UpdateAnime(int animeId, int numberOfWatchedEpisodes, Status status, bool? isRewatching = null, int? numberOfTimesRewatched = null, DateTime? startDate = null, DateTime? endDate = null, string alternativeId = null, AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse ids = null, bool? isShow = null) {
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

            if (await UpdateAnime(animeId, anilistStatus, numberOfWatchedEpisodes, numberOfTimesRewatched, startDate, endDate)) {
                return new UpdateAnimeStatusResponse();
            }

            return null;
        }

        public async Task<MalApiCalls.User> GetUser() {
            AniListViewer.Viewer user = await GetCurrentUser();
            return ClassConversions.ConvertUser(user.Id, user.Name);
        }

        public async Task<List<Anime>> GetAnimeList(Status status, int? userId = null) {
            var animeList = await GetAnimeList(userId.Value, status.ToAniListStatus());
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

        /// <summary>
        /// Get a singular anime.
        /// </summary>
        /// <param name="id">ID of the anime you want to get.</param>
        /// <returns>The retrieved anime.</returns>
        public async Task<AniListSearch.Media> GetAnime(int id) {
            string query = @"query ($id: Int) {
          Media(id: $id) {
            id
            episodes
            isAdult
            title {
              romaji
              english
              native
              userPreferred
            }
            relations {
              edges {
                relationType
                node {
                  id
                  title {
                    romaji
                    english
                    native
                    userPreferred
                  }
                }
              }
            }
            mediaListEntry {
              status
              progress
              repeat
              startedAt {
                day
                month
                year
              }
              completedAt {
                day
                month
                year
              }
            }
          }
        }";


            Dictionary<string, object> variables = new Dictionary<string, object> {
                { "id", id.ToString() }
            };

            var response = await GraphQlHelper.AuthenticatedRequest(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, _userConfig, query, ApiName.AniList, variables);
            if (response != null) {
                StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
                var result = JsonSerializer.Deserialize<AniListGet.AniListGetMedia>(await streamReader.ReadToEndAsync());

                if (result != null) {
                    return result.Data.Media;
                }
            }

            return null;
        }

        public async Task<AniListViewer.Viewer> GetCurrentUser() {
            string query = @"query {
          Viewer {
            id
            name
          }
        }";

            var response = await GraphQlHelper.AuthenticatedRequest(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, _userConfig, query, ApiName.AniList);
            if (response != null) {
                StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
                var result = JsonSerializer.Deserialize<AniListViewer.AniListGetViewer>(await streamReader.ReadToEndAsync());

                if (result != null) {
                    return result.Data.Viewer;
                }
            }

            return null;
        }

        public async Task<bool> UpdateAnime(int id, AniListSearch.MediaListStatus status, int progress, int? numberOfTimesRewatched = null, DateTime? startDate = null, DateTime? endDate = null) {
            string query = @"mutation ($mediaId: Int, $status: MediaListStatus, $progress: Int" +
                           (numberOfTimesRewatched != null ? ", $repeat: Int" : "") +
                           (startDate != null ? ",$startDay: Int, $startMonth: Int, $startYear: Int" : "") +
                           (endDate != null ? ",$endDay: Int, $endMonth: Int, $endYear: Int" : "") +
                           @") {
          SaveMediaListEntry (mediaId: $mediaId, status: $status, progress: $progress" +
                           (numberOfTimesRewatched != null ? ", repeat: $repeat" : "") +
                           (startDate != null ? @", startedAt: {day: $startDay, month: $startMonth, year: $startYear}" : "") +
                           (endDate != null ? @", completedAt: {day: $endDay, month: $endMonth, year: $endYear}" : "") +
                           @") {
            id
          }
        }";

            Dictionary<string, object> variables = new Dictionary<string, object> {
                { "mediaId", id.ToString() },
                { "status", status.ToString().ToUpper() },
                { "progress", progress.ToString() }
            };

            if (numberOfTimesRewatched != null) {
                variables.Add("repeat", numberOfTimesRewatched.ToString());
            }

            if (startDate != null) {
                variables.Add("startDay", startDate.Value.Day.ToString());
                variables.Add("startMonth", startDate.Value.Month.ToString());
                variables.Add("startYear", startDate.Value.Year.ToString());
            }

            if (endDate != null) {
                variables.Add("endDay", endDate.Value.Day.ToString());
                variables.Add("endMonth", endDate.Value.Month.ToString());
                variables.Add("endYear", endDate.Value.Year.ToString());
            }

            var response = await GraphQlHelper.AuthenticatedRequest(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, _userConfig, query, ApiName.AniList, variables);
            return response != null;
        }

        public async Task<List<AniListMediaList.Entries>> GetAnimeList(int userId, AniListSearch.MediaListStatus status) {
            string query = @"query ($status: MediaListStatus, $userId: Int, $chunk: Int, $chunkSize: Int) {
              MediaListCollection(userId: $userId, status: $status, chunk: $chunk, perChunk: $chunkSize, type: ANIME) {
                hasNextChunk
                lists {
                  entries {
                    media {
                      id
                      siteUrl
                    }
                    completedAt {
                      day
                      month
                      year
                    }
                    progress
                  }
                }
              }
            }";

            int page = 1;
            Dictionary<string, object> variables = new Dictionary<string, object> {
                { "status", status.ToString().ToUpper() },
                { "userId", userId.ToString() },
                { "chunkSize", ChunkSize.ToString() },
                { "chunk", page.ToString() }
            };

            AniListMediaList.AniListUserMediaList result = await GraphQlHelper.DeserializeRequest<AniListMediaList.AniListUserMediaList>(_httpClient, query, variables);

            if (result?.Data?.MediaListCollection?.MediaList != null) {
                var entriesList = result.Data.MediaListCollection.MediaList.SelectMany(entries => entries.Entries);
                if (result.Data.MediaListCollection.HasNextChunk) {
                    // impose a hard limit of 10 pages
                    while (page < 10) {
                        page++;
                        variables["chunk"] = page.ToString();

                        AniListMediaList.AniListUserMediaList nextPageResult = await GraphQlHelper.DeserializeRequest<AniListMediaList.AniListUserMediaList>(_httpClient, query, variables);

                        entriesList = entriesList.Concat(nextPageResult.Data.MediaListCollection.MediaList.SelectMany(entries => entries.Entries));
                        if (!nextPageResult.Data.MediaListCollection.HasNextChunk) {
                            break;
                        }

                        // sleeping thread so we dont hammer the API
                        Thread.Sleep(2000);
                    }
                }

                return entriesList.ToList();
            }

            return null;
        }

        async Task<List<Anime>> IApiCallHelpers.SearchAnime(string query) {
            bool updateNsfw = Plugin.Instance?.PluginConfiguration?.updateNsfw != null && Plugin.Instance.PluginConfiguration.updateNsfw;

            return AniListSearchAnimeConvertedList(await SearchAnime(query), updateNsfw);
        }

        internal static List<Anime> AniListSearchAnimeConvertedList(List<AniListSearch.Media> animeList, bool updateNsfw) {
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
    }
}