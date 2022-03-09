using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
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
        string query = @"query ($search: String!) {
            Page(perPage: 100, page: 1) {
                pageInfo {
                    total
                        perPage
                    currentPage
                        lastPage
                    hasNextPage
                }
                media(search: $search) {
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

        Dictionary<string, string> variables = new Dictionary<string, string> {
            { "search", searchString }
        };

        var response = await GraphQlHelper.Request(_httpClient, query, variables);
        if (response != null) {
            StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
            var result = JsonSerializer.Deserialize<AniListSearch.AniListSearchMedia>(await streamReader.ReadToEndAsync());

            if (result != null) {
                return result.Data.Page.Media;
            }
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