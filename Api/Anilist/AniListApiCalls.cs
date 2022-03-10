using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Models;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api.Anilist;

public class AniListApiCalls {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServerApplicationHost _serverApplicationHost;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HttpClient _httpClient;
    private readonly UserConfig _userConfig;
    public static readonly int PageSize = 50;


    public AniListApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, UserConfig userConfig = null) {
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
                    }
                }
            }
        }
        ";

        int page = 1;
        Dictionary<string, string> variables = new Dictionary<string, string> {
            { "search", searchString },
            { "type", "ANIME" },
            { "perPage", PageSize.ToString() },
            { "page", page.ToString() }
        };

        AniListSearch.AniListSearchMedia result = await SearchRequest(query, variables);

        if (result != null) {
            if (result.Data.Page.PageInfo.HasNextPage) {
                // impose a hard limit of 10 pages
                while (page < 10) {
                    page++;
                    AniListSearch.AniListSearchMedia nextPageResult = await SearchRequest(query, variables);

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

    private async Task<AniListSearch.AniListSearchMedia> SearchRequest(string query, Dictionary<string, string> variables) {
        var response = await GraphQlHelper.Request(_httpClient, query, variables);
        if (response != null) {
            StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
            return JsonSerializer.Deserialize<AniListSearch.AniListSearchMedia>(await streamReader.ReadToEndAsync());
        }

        return null;
    }

    /// <summary>
    /// Get a singular anime.
    /// </summary>
    /// <param name="id">ID of the anime you want to get.</param>
    /// <returns>The retrieved anime.</returns>
    public async Task<AniListSearch.Media> GetAnime(int id) {
        string query = @"query ($id: Int) {
          Media(id: $id) {
            episodes
            relations {
              edges {
                relationType
                node {
                  id
                }
              }
            }
          }
        }";


        Dictionary<string, string> variables = new Dictionary<string, string> {
            { "id", id.ToString() }
        };

        var response = await GraphQlHelper.Request(_httpClient, query, variables);
        if (response != null) {
            StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
            var result = JsonSerializer.Deserialize<AniListGet.AniListGetMedia>(await streamReader.ReadToEndAsync());

            if (result != null) {
                return result.Data.Media;
            }
        }

        return null;
    }

    public async Task<string> GetCurrentUser() {
        string query = @"query {
          Viewer {
            name
          }
        }";

        var response = await GraphQlHelper.AuthenticatedRequest(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _userConfig, query);
        if (response != null) {
            StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
            var result = JsonSerializer.Deserialize<AniListViewer.AniListGetViewer>(await streamReader.ReadToEndAsync());

            if (result != null) {
                return result.Data.Viewer.Name;
            }
        }

        return null;
    }

    public async Task<bool> UpdateAnime(int id, Status status, int progress) {
        string query = @"mutation ($mediaId: Int, $status: MediaListStatus, $progress: Int) {
          SaveMediaListEntry (mediaId: $mediaId, status: $status, progress: $progress) {
            id
          }
        }";

        Dictionary<string, string> variables = new Dictionary<string, string> {
            { "mediaId", id.ToString() },
            { "status", status.ToString().ToUpper() },
            { "progress", progress.ToString() }
        };

        var response = await GraphQlHelper.AuthenticatedRequest(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, _userConfig, query, variables);
        return response != null;
    }

    public enum Status {
        Current,
        Planning,
        Completed,
        Dropped,
        Paused,
        Repeating
    }
}