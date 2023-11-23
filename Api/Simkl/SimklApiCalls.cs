#nullable enable
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
using jellyfin_ani_sync.Models.Simkl;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api.Simkl;

public class SimklApiCalls {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServerApplicationHost _serverApplicationHost;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Dictionary<string, string> _requestHeaders;
    private readonly UserConfig _userConfig;
    private readonly ILogger<SimklApiCalls> _logger;
    private readonly AuthApiCall _authApiCall;
    public static readonly string ApiBaseUrl = "https://api.simkl.com";

    public SimklApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, Dictionary<string, string>? requestHeaders, UserConfig? userConfig = null) {
        _httpClientFactory = httpClientFactory;
        _serverApplicationHost = serverApplicationHost;
        _httpContextAccessor = httpContextAccessor;
        _requestHeaders = requestHeaders;
        _userConfig = userConfig;
        _logger = loggerFactory.CreateLogger<SimklApiCalls>();
        _authApiCall = new AuthApiCall(ApiName.Simkl, httpClientFactory, serverApplicationHost, httpContextAccessor, loggerFactory, userConfig: userConfig);
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

            // sleeping task so we dont hammer the API
            await Task.Delay(1000);
        }

        return result;
    }

    public async Task<SimklExtendedMedia?> GetAnime(int id) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiBaseUrl}/anime/{id}"
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
            throw;
        }
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
            throw;
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
}