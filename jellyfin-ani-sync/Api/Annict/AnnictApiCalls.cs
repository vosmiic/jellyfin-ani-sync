using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Interfaces;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Annict;
using jellyfin_ani_sync.Models.Mal;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api.Annict {
    public class AnnictApiCalls : IApiCallHelpers {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMemoryCache _memoryCache;
        private readonly IAsyncDelayer _delayer;
        private readonly UserConfig _userConfig;
        public static readonly int PageSize = 1000;


        public AnnictApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, IAsyncDelayer delayer, UserConfig userConfig = null) {
            _httpClientFactory = httpClientFactory;
            _loggerFactory = loggerFactory;
            _serverApplicationHost = serverApplicationHost;
            _httpContextAccessor = httpContextAccessor;
            httpClientFactory.CreateClient(NamedClient.Default);
            _userConfig = userConfig;
            _memoryCache = memoryCache;
            _delayer = delayer;
        }

        /// <summary>
        /// Search for an anime based upon its name.
        /// </summary>
        /// <param name="searchString">The name to search for.</param>
        /// <returns>List of anime.</returns>
        public async Task<List<AnnictSearch.AnnictAnime>> SearchAnime(string searchString) {
            string query = @"query ($title: String!) {
            searchWorks(titles: [$title]) {
                nodes {
                    id
                    titleEn
                    malAnimeId
                    viewerStatusState
                    episodesCount
                }
            }
        }
        ";

            Dictionary<string, object> variables = new Dictionary<string, object> {
                { "title", searchString }
            };

            AnnictSearch.AnnictSearchMedia searchMedia = new AnnictSearch.AnnictSearchMedia();

            var response = await GraphQlHelper.AuthenticatedRequest(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, _userConfig, query, ApiName.Annict, variables);
            if (response != null) {
                StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
                var result = JsonSerializer.Deserialize<AnnictSearch.AnnictSearchMedia>(await streamReader.ReadToEndAsync());

                return result.AnnictSearchData.SearchWorks.Nodes;
            }

            return null;
        }

        public async Task<Anime> GetAnime(int id, string alternativeId = null, bool getRelated = false) {
            var anime = await GetAnime(alternativeId);
            if (anime == null) return null;
            return ClassConversions.ConvertAnnictAnime(anime);
        }

        public async Task<UpdateAnimeStatusResponse> UpdateAnime(int animeId, int numberOfWatchedEpisodes, Status status, bool? isRewatching = null, int? numberOfTimesRewatched = null, DateTime? startDate = null, DateTime? endDate = null, string alternativeId = null, AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse ids = null, bool? isShow = null) {
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
            
            if (await UpdateAnime(alternativeId, annictMediaStatus))
                return new UpdateAnimeStatusResponse();

            return null;
        }

        public async Task<MalApiCalls.User> GetUser() {
            AnnictViewer.AnnictViewerRoot user = await GetCurrentUser();
            if (user != null)
                return new MalApiCalls.User {
                    Name = user.AnnictSearchData.Viewer.username
                };

            return null;
        }

        public async Task<List<Anime>> GetAnimeList(Status status, int? userId = null) {
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
            
            List<AnnictSearch.AnnictAnime> animeList = await GetAnimeList(annictMediaStatus);
            if (animeList != null) {
                List<Anime> convertedList = new List<Anime>();
                foreach (AnnictSearch.AnnictAnime annictAnime in animeList) {
                    convertedList.Add(ClassConversions.ConvertAnnictAnime(annictAnime));
                }
            
                return convertedList;
            }

            return null;
        }

        /// <summary>
        /// Update an anime status.
        /// </summary>
        /// <param name="id">ID of the anime in the provider.</param>
        /// <param name="status">Status to set the anime as.</param>
        /// <returns>True if the anime has been updated.</returns>
        public async Task<bool> UpdateAnime(string id, AnnictSearch.AnnictMediaStatus status) {
            string query = @"mutation ($workId: ID!, $state: StatusState!" +
                           @") {
          updateStatus (input: {workId:$workId, state:$state}" +
                           @") {
            clientMutationId
          }
        }";

            Dictionary<string, object> variables = new Dictionary<string, object> {
                { "workId", id },
                { "state", status.ToString().ToUpper() },
            };

            var response = await GraphQlHelper.AuthenticatedRequest(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, _userConfig, query, ApiName.Annict, variables);
            return response != null;
        }

        /// <summary>
        /// Get the users anime list.
        /// </summary>
        /// <param name="status">Status to filter by.</param>
        /// <returns>List of anime in the users list.</returns>
        public async Task<List<AnnictSearch.AnnictAnime>> GetAnimeList(AnnictSearch.AnnictMediaStatus status) {
            string query = @"query($state: [StatusState!], $perPage: Int, $after: String!) {
            viewer {
                libraryEntries(states:$state, first: $perPage,after: $after) {
                    nodes {
                        work {
                            id
                            titleEn
                            malAnimeId
                            viewerStatusState
                            episodesCount
                        },
                    },
                    pageInfo {
                      hasNextPage,
                      startCursor
                    }
                }
            }
        }
        ";

            Dictionary<string, object> variables = new Dictionary<string, object> {
                { "state", status.ToString().ToUpper() },
                { "perPage", PageSize },
                { "after", string.Empty }
            };

            AnnictMediaList.AnnictUserMediaList result = new AnnictMediaList.AnnictUserMediaList();

            var response = await GraphQlHelper.AuthenticatedRequest(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, _userConfig, query, ApiName.Annict, variables);
            if (response != null) {
                StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
                result = JsonSerializer.Deserialize<AnnictMediaList.AnnictUserMediaList>(await streamReader.ReadToEndAsync());
            }

            if (result != null) {
                while (result.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.PageInfo.HasNextPage) {
                    variables["after"] = result.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.PageInfo.StartCursor;
                    var paginatedResponse = await GraphQlHelper.AuthenticatedRequest(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, _userConfig, query, ApiName.Annict, variables);
                    var paginatedResult = new AnnictMediaList.AnnictUserMediaList();
                    if (paginatedResponse != null) {
                        StreamReader streamReader = new StreamReader(await paginatedResponse.Content.ReadAsStreamAsync());
                        paginatedResult = JsonSerializer.Deserialize<AnnictMediaList.AnnictUserMediaList>(await streamReader.ReadToEndAsync());
                    }

                    result.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.Nodes = result.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.Nodes.Concat(paginatedResult.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.Nodes).ToList();
                    result.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.PageInfo = paginatedResult.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.PageInfo;
                    if (!paginatedResult.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.PageInfo.HasNextPage) {
                        break;
                    }


                    // sleeping thread so we dont hammer the API
                    Thread.Sleep(1000);
                }

                return result.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.Nodes;
            }

            return null;
        }

        /// <summary>
        /// Get a singular anime.
        /// </summary>
        /// <param name="id">ID of the anime you want to get.</param>
        /// <returns>The retrieved anime.</returns>
        public async Task<AnnictSearch.AnnictAnime> GetAnime(string id) {
            string query = @"query ($id: ID!) {
          node(id: $id) {
            ... on Work {
                id
                titleEn
                malAnimeId
                viewerStatusState
                episodesCount
            }
          }
        }";


            Dictionary<string, object> variables = new Dictionary<string, object> {
                { "id", id }
            };

            var response = await GraphQlHelper.AuthenticatedRequest(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, _userConfig, query, ApiName.Annict, variables);
            if (response != null) {
                StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
                var result = JsonSerializer.Deserialize<AnnictGetMedia.AnnictGetMediaRoot>(await streamReader.ReadToEndAsync());

                if (result != null) {
                    return result.AnnictGetMediaData.Node;
                }
            }

            return null;
        }

        public async Task<AnnictViewer.AnnictViewerRoot> GetCurrentUser() {
            string query = @"query {
          viewer {
            username
          }
        }";

            var response = await GraphQlHelper.AuthenticatedRequest(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _memoryCache, _delayer, _userConfig, query, ApiName.Annict, new Dictionary<string, object>());
            if (response != null) {
                StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
                var result = JsonSerializer.Deserialize<AnnictViewer.AnnictViewerRoot>(await streamReader.ReadToEndAsync());

                if (result != null) {
                    return result;
                }
            }

            return null;
        }

        async Task<List<Anime>> IApiCallHelpers.SearchAnime(string query) {
            return AnnictSearchAnimeConvertedList(await SearchAnime(query));
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
    }
}