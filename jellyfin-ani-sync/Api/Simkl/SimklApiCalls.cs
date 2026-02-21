#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Extensions;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Interfaces;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Mal;
using jellyfin_ani_sync.Models.Simkl;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api.Simkl;

public class SimklApiCalls : IApiCallHelpers {
    private readonly Dictionary<string, string> _requestHeaders;
    private readonly ILogger<SimklApiCalls> _logger;
    private readonly AuthApiCall _authApiCall;
    public static readonly string ApiBaseUrl = "https://api.simkl.com";

    public SimklApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, IAsyncDelayer delayer, Dictionary<string, string>? requestHeaders, UserConfig? userConfig = null) {
        _requestHeaders = requestHeaders;
        _logger = loggerFactory.CreateLogger<SimklApiCalls>();
        _authApiCall = new AuthApiCall(httpClientFactory, serverApplicationHost, httpContextAccessor, loggerFactory, memoryCache, delayer, userConfig: userConfig);
    }

    /// <summary>
    /// Get the users last activity. While this does not return the activity because this is only used to validate the token, it can later be adjusted to return the actual data if needed.
    /// </summary>
    /// <returns></returns>
    public async Task<bool> GetLastActivity() {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiBaseUrl}/sync/activities"
        };

        HttpResponseMessage? apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Simkl, AuthApiCall.CallType.GET, url.Build(), requestHeaders: _requestHeaders);
        return apiCall is { IsSuccessStatusCode: true };
    }

    public async Task<List<SimklMedia>?> SearchAnime(string searchString) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiBaseUrl}/search/anime"
        };

        url.Parameters.Add(new KeyValuePair<string, string>("q", searchString));
        url.Parameters.Add(new KeyValuePair<string, string>("extended", "full"));

        if (_requestHeaders.TryGetValue("simkl-api-key", out string? clientId)) {
            url.Parameters.Add(new KeyValuePair<string, string>("client_id", clientId));
        } else {
            return null;
        }

        int page = 1;
        int pageLimit = 50;
        url.Parameters.Add(new KeyValuePair<string, string>("limit", pageLimit.ToString()));
        url.Parameters.Add(new KeyValuePair<string, string>("page", page.ToString()));

        var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Simkl, AuthApiCall.CallType.GET, url.Build(), requestHeaders: _requestHeaders);
        if (apiCall == null) {
            return null;
        }

        List<SimklMedia>? result;
        try {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            result = JsonSerializer.Deserialize<List<SimklMedia>>(await streamReader.ReadToEndAsync());
        } catch (Exception e) {
            _logger.LogError($"Could not deserialize result, reason: {e.Message}");
            return null;
        }

        if (result == null || result.Count == 0) return null;
        while (page < 10) {
            page++;

            url.Parameters.RemoveAll(item => item.Key == "page");
            url.Parameters.Add(new KeyValuePair<string, string>("page", page.ToString()));
            if (apiCall.Headers.TryGetValues("X-Pagination-Limit", out IEnumerable<string>? paginationLimitResults)) {
                if (int.TryParse(paginationLimitResults.First(), out int parsedPaginationLimit) && parsedPaginationLimit != pageLimit) {
                    url.Parameters.RemoveAll(item => item.Key == "limit");
                    pageLimit = parsedPaginationLimit;
                    url.Parameters.Add(new KeyValuePair<string, string> ("limit", pageLimit.ToString()));
                }
            }

            HttpResponseMessage? pageApiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Simkl, AuthApiCall.CallType.GET, url.Build());
            if (pageApiCall == null) break;
            List<SimklMedia>? nextPageResult;
            try {
                StreamReader streamReader = new StreamReader(await pageApiCall.Content.ReadAsStreamAsync());
                nextPageResult = JsonSerializer.Deserialize<List<SimklMedia>>(await streamReader.ReadToEndAsync());
            } catch (Exception e) {
                _logger.LogWarning($"Could not retrieve next result page, reason: {e.Message}");
                break;
            }

            if (nextPageResult != null) {
                result = result.Concat(nextPageResult).ToList();

                if (nextPageResult.Count < pageLimit) {
                    // presume we have hit the limit; stop paging
                    break;
                }
            }
        }

        return result;
    }

    public Task<Anime>? GetAnime(int id, string alternativeId = null, bool getRelated = false) {
        return null;
    }

    public async Task<UpdateAnimeStatusResponse?> UpdateAnime(int animeId, int numberOfWatchedEpisodes, Status status, bool? isRewatching = null, int? numberOfTimesRewatched = null, DateTime? startDate = null, DateTime? endDate = null, string alternativeId = null, AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse ids = null, bool? isShow = null) {
        if (await UpdateAnime(animeId, status.ToSimklStatus(), isShow.Value, ids, numberOfWatchedEpisodes)) {
            return new UpdateAnimeStatusResponse();
        }

        return null;
    }

    public Task<MalApiCalls.User>? GetUser() {
        return null;
    }

    public Task<List<Anime>>? GetAnimeList(Status status, int? userId = null) {
        return null;
    }

    public async Task<SimklExtendedMedia?> GetAnime(int id) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiBaseUrl}/anime/{id}",
            Parameters = new List<KeyValuePair<string, string>> { new ("extended", "full") }
        };

        if (_requestHeaders.TryGetValue("simkl-api-key", out string? clientId)) {
            url.Parameters.Add(new KeyValuePair<string, string>("client_id", clientId));
        } else {
            return null;
        }

        HttpResponseMessage? apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Simkl, AuthApiCall.CallType.GET, url.Build(), requestHeaders: _requestHeaders);
        if (apiCall == null) {
            return null;
        }

        try {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            return JsonSerializer.Deserialize<SimklExtendedMedia>(await streamReader.ReadToEndAsync());
        } catch (Exception e) {
            _logger.LogError($"Could not deserialize anime, reason: {e.Message}");
            return null;
        }
    }


    public async Task<Anime?> GetAnime(AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse ids, string title, bool getRelated = false) {
        SimklIdLookupMedia idLookupResult = await GetAnimeByIdLookup(ids, title);
        if (idLookupResult != null && (idLookupResult.Ids?.Simkl != null && idLookupResult.Ids.Simkl != 0)) {
            var detailedAnime = await GetAnime(idLookupResult.Ids.Simkl);
            var userList = await GetUserAnimeList();
            if (detailedAnime != null) {
                return ClassConversions.ConvertSimklAnime(detailedAnime, userList?.FirstOrDefault(userEntry => userEntry.Show.Ids.Simkl == detailedAnime.Ids.Simkl));
            }
        }

        return null;
    }

    public async Task<SimklIdLookupMedia?> GetAnimeByIdLookup(AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse ids, string title) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiBaseUrl}/search/id"
        };

        if (_requestHeaders.TryGetValue("simkl-api-key", out string? clientId)) {
            url.Parameters.Add(new KeyValuePair<string, string>("client_id", clientId));
        } else {
            return null;
        }

        if (ids.Anilist != null) {
            url.Parameters.Add(new KeyValuePair<string, string>("anilist", ids.Anilist.Value.ToString()));
        }

        if (ids.Kitsu != null) {
            url.Parameters.Add(new KeyValuePair<string, string>("kitsu", ids.Kitsu.Value.ToString()));
        }

        if (ids.AniDb != null) {
            url.Parameters.Add(new KeyValuePair<string, string>("anidb", ids.AniDb.Value.ToString()));
        }

        if (ids.MyAnimeList != null) {
            url.Parameters.Add(new KeyValuePair<string, string>("mal", ids.MyAnimeList.Value.ToString()));
        }

        HttpResponseMessage? apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Simkl, AuthApiCall.CallType.GET, url.Build(), requestHeaders: _requestHeaders);
        if (apiCall == null) {
            return null;
        }

        try {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            List<SimklIdLookupMedia>? results = JsonSerializer.Deserialize<List<SimklIdLookupMedia>?>(await streamReader.ReadToEndAsync());
            if (results != null && results.Count > 0) {
                // attempt to match to title
                var detectedAnime = results.FirstOrDefault(anime => string.Equals(anime.Title, title, StringComparison.CurrentCultureIgnoreCase));
                if (detectedAnime != null) {
                    return detectedAnime;
                } else {
                    // might not be a perfect match; just return the first result (should only ever be a single result anyway)
                    return results[0];
                }
            }
        } catch (Exception e) {
            _logger.LogError($"Could not deserialize anime list, reason: {e.Message}");
            return null;
        }

        return null;
    }

    public async Task<List<SimklUserEntry>?> GetUserAnimeList(SimklStatus? status = null) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiBaseUrl}/sync/all-items/anime"
        };
        if (status != null) {
            url.Base += $"/{status}";
        }

        url.Parameters.Add(new KeyValuePair<string, string>("extended", "full"));

        HttpResponseMessage? apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Simkl, AuthApiCall.CallType.GET, url.Build(), requestHeaders: _requestHeaders);
        if (apiCall == null) {
            return null;
        }

        try {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            SimklUserList? deserialized = JsonSerializer.Deserialize<SimklUserList>(await streamReader.ReadToEndAsync());
            if (deserialized != null) {
                return deserialized.Entry;
            } else {
                return null;
            }
        } catch (Exception e) {
            _logger.LogError($"Could not deserialize user anime list, reason: {e.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateAnime(int animeId, SimklStatus updateStatus, bool isShow, AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse ids, int numberOfWatchedEpisodes) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiBaseUrl}/sync/history"
        };

        if (_requestHeaders.TryGetValue("simkl-api-key", out string? clientId)) {
            url.Parameters.Add(new KeyValuePair<string, string>("client_id", clientId));
        } else {
            return false;
        }

        SimklExtendedIds convertedIds = new SimklExtendedIds {
            Simkl = animeId
        };

        if (ids.Anilist != null) {
            convertedIds.Anilist = ids.Anilist;
        }

        if (ids.Kitsu != null) {
            convertedIds.Kitsu = ids.Kitsu;
        }

        if (ids.MyAnimeList != null) {
            convertedIds.Mal = ids.MyAnimeList;
        }

        if (ids.AniDb != null) {
            convertedIds.Anidb = ids.AniDb;
        }

        SimklUpdateBody updateBody = new SimklUpdateBody();
        if (isShow) {
            var updateBodyShow = new UpdateBodyShow {
                Ids = convertedIds
            };

            bool simklUpdateAll = ConfigHelper.GetSimklUpdateAll();
            updateBodyShow.Episodes = new List<UpdateEpisode>();

            if (simklUpdateAll) {
                for (int i = 1; i <= numberOfWatchedEpisodes; i++) {
                    updateBodyShow.Episodes.Add(new UpdateEpisode {
                        EpisodeNumber = i
                    });
                }
            } else {
                updateBodyShow.Episodes.Add(new UpdateEpisode {
                    EpisodeNumber = numberOfWatchedEpisodes
                });
            }

            updateBody.Shows = new List<UpdateBodyShow> {
                updateBodyShow
            };
        } else {
            updateBody.Movies = new List<UpdateBodyShow> {
                new UpdateBodyShow {
                    Ids = convertedIds
                }
            };
        }

        var stringContent = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");

        HttpResponseMessage? response = await _authApiCall.AuthenticatedApiCall(ApiName.Simkl, AuthApiCall.CallType.POST, url.Build(), stringContent: stringContent, requestHeaders: _requestHeaders);
        if (response != null) {
            return response.IsSuccessStatusCode;
        }

        return false;
    }

    Task<List<Anime>> IApiCallHelpers.SearchAnime(string query) {
        return Task.FromResult(new List<Anime>());
    }
}