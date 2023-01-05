using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Annict;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api.Annict {
    public class AnnictApiCalls {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly HttpClient _httpClient;
        private readonly UserConfig _userConfig;
        public static readonly int PageSize = 100;


        public AnnictApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, UserConfig userConfig = null) {
            _httpClientFactory = httpClientFactory;
            _loggerFactory = loggerFactory;
            _serverApplicationHost = serverApplicationHost;
            _httpContextAccessor = httpContextAccessor;
            _httpClient = httpClientFactory.CreateClient(NamedClient.Default);
            _userConfig = userConfig;
        }

        /// <summary>
        /// Search for an anime based upon its name.
        /// </summary>
        /// <param name="searchString">The name to search for.</param>
        /// <returns>List of anime.</returns>
        public async Task<List<AnnictSearch.AnnictAnime>> SearchAnime(string searchString) {
            string query = @"query (title: String!, $perPage: Int, $after: Int) {
            searchWorks(titles: [$title],first: $perPage,after: $after) {
                nodes {
                    id
                    titleEn
                    malAnimeId
                },
                pageInfo {
                  hasNextPage
                }
            }
        }
        ";

            int after = 0;
            Dictionary<string, string> variables = new Dictionary<string, string> {
                { "title", searchString },
                { "perPage", PageSize.ToString() },
                { "after", after.ToString() }
            };

            AnnictSearch.AnnictSearchMedia result = await GraphQlHelper.DeserializeRequest<AnnictSearch.AnnictSearchMedia>(_httpClient, query, variables);

            if (result != null) {
                if (result.AnnictSearchData.SearchWorks.PageInfo.HasNextPage) {
                    // impose a hard limit of 10 pages
                    while (after < 900) {
                        after += 100;
                        AnnictSearch.AnnictSearchMedia nextPageResult = await GraphQlHelper.DeserializeRequest<AnnictSearch.AnnictSearchMedia>(_httpClient, query, variables);

                        result.AnnictSearchData.SearchWorks.Nodes = result.AnnictSearchData.SearchWorks.Nodes.Concat(nextPageResult.AnnictSearchData.SearchWorks.Nodes).ToList();
                        if (!nextPageResult.AnnictSearchData.SearchWorks.PageInfo.HasNextPage) {
                            break;
                        }

                        // sleeping thread so we dont hammer the API
                        Thread.Sleep(1000);
                    }
                }

                return result.AnnictSearchData.SearchWorks.Nodes;
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
            string query = @"mutation ($workId: String!, $state: StatusState" + 
                           @") {
          updateStatus (input: {workId:$workId, state:$status" +
                           @") {
            clientMutationId
          }
        }";

            Dictionary<string, string> variables = new Dictionary<string, string> {
                { "workId", id },
                { "status", status.ToString().ToUpper() },
            };

            var response = await GraphQlHelper.AuthenticatedRequest(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _userConfig, query, variables);
            return response != null;
        }

        /// <summary>
        /// Get the users anime list.
        /// </summary>
        /// <param name="status">Status to filter by.</param>
        /// <returns>List of anime in the users list.</returns>
        public async Task<List<AnnictSearch.AnnictAnime>> GetAnimeList(AnnictSearch.AnnictMediaStatus status) {
            string query = @"query($state: StatusState, $perPage: Int, $after: Int) {
            viewer {
                libraryEntries(states:$state, first: $perPage,after: $after) {
                    nodes {
                        id
                        titleEn
                        malAnimeId
                        viewStatusState
                    },
                    pageInfo {
                      hasNextPage
                    }
                }
            }
        }
        ";

            int after = 0;
            Dictionary<string, string> variables = new Dictionary<string, string> {
                { "state", status.ToString().ToUpper() },
                { "perPage", PageSize.ToString() },
                { "after", after.ToString() }
            };

            AnnictMediaList.AnnictUserMediaList result = await GraphQlHelper.DeserializeRequest<AnnictMediaList.AnnictUserMediaList>(_httpClient, query, variables);

            if (result != null) {
                if (result.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.PageInfo.HasNextPage) {
                    // impose a hard limit of 10 pages
                    while (after < 900) {
                        after += 100;
                        AnnictMediaList.AnnictUserMediaList nextPageResult = await GraphQlHelper.DeserializeRequest<AnnictMediaList.AnnictUserMediaList>(_httpClient, query, variables);

                        result.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.Nodes = result.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.Nodes.Concat(nextPageResult.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.Nodes).ToList();
                        if (!nextPageResult.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.PageInfo.HasNextPage) {
                            break;
                        }

                        // sleeping thread so we dont hammer the API
                        Thread.Sleep(1000);
                    }
                }

                return result.AnnictSearchData.Viewer.AnnictUserMediaListLibraryEntries.Nodes;
            }

            return null;
        }
    }
}