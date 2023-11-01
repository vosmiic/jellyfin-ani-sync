#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Shikimori;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api.Shikimori; 

public class ShikimoriApiCalls {
    private readonly ILogger<ShikimoriApiCalls> _logger;
    private readonly AuthApiCall _authApiCall;
    private readonly string _refreshTokenUrl = "https://shikimori.one/oauth/token";
    private readonly string _apiBaseUrl = "https://shikimori.one/api";
    private readonly UserConfig? _userConfig;
    private readonly Dictionary<string, string>? _requestHeaders;

    public ShikimoriApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, Dictionary<string, string>? requestHeaders, UserConfig? userConfig = null) {
        _userConfig = userConfig;
        _requestHeaders = requestHeaders;
        _logger = loggerFactory.CreateLogger<ShikimoriApiCalls>();
        _authApiCall = new AuthApiCall(ApiName.Shikimori, httpClientFactory, serverApplicationHost, httpContextAccessor, loggerFactory, userConfig: userConfig);
    }
    
    public class User {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("nickname")] public string Name { get; set; }
    }
    
    /// <summary>
    /// Get a users information.
    /// </summary>
    public async Task<User?> GetUserInformation() {
        UrlBuilder url = new UrlBuilder {
            Base = $"{_apiBaseUrl}/users/whoami"
        };
        var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Shikimori, AuthApiCall.CallType.GET, url.Build(), requestHeaders: _requestHeaders);
        if (apiCall != null) {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            string streamText = await streamReader.ReadToEndAsync();

            try {
                User? user = JsonSerializer.Deserialize<User>(streamText);

                if (user != null) {
                    _userConfig?.KeyPairs.Add(new KeyPairs {
                        Key = "ShikimoriUserId",
                        Value = user.Id.ToString()
                    });

                    Plugin.Instance?.SaveConfiguration();
                }

                return user;
            } catch (Exception) {
                return null;
            }
        } else {
            return null;
        }
    }

    /// <summary>
    /// Search for an anime based upon its name.
    /// </summary>
    /// <param name="searchString">The name to search for.</param>
    /// <returns></returns>
    public async Task<List<ShikimoriMedia>?> SearchAnime(string searchString) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{_apiBaseUrl}/animes"
        };
        url.Parameters.Add(new KeyValuePair<string, string>("search", searchString));
        
        return await PagedCall<ShikimoriMedia>(url, AuthApiCall.CallType.GET);
    }
    
        private async Task<List<T>?> PagedCall<T>(UrlBuilder url, AuthApiCall.CallType callType) {
        int page = 1;
        url.Parameters.Add(new KeyValuePair<string, string>("limit", "50"));
        url.Parameters.Add(new KeyValuePair<string, string>("page", page.ToString()));

        var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Shikimori, callType, url.Build(), requestHeaders: _requestHeaders);
        if (apiCall == null) {
            return null;
        }
        List<T>? result;
        try {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            result = JsonSerializer.Deserialize<List<T>>(await streamReader.ReadToEndAsync());
        } catch (Exception e) {
            _logger.LogError($"Could not deserialize anime, reason: {e.Message}");
            return null;
        }

        if (result == null || result.Count == 0) return null;
        while (page < 10) {
            page++;

            url.Parameters.RemoveAll(item => item.Key == "page");
            url.Parameters.Add(new KeyValuePair<string, string>("page", page.ToString()));
            
            HttpResponseMessage? pageApiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Shikimori, callType, url.Build());
            if (pageApiCall == null) break;
            List<T>? nextPageResult;
            try {
                StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
                nextPageResult = JsonSerializer.Deserialize<List<T>>(await streamReader.ReadToEndAsync());
            } catch (Exception e) {
                _logger.LogError($"Could not retrieve next page of anime, reason: {e.Message}");
                break;
            }

            if (nextPageResult != null) {
                result = result.Concat(nextPageResult).ToList();
            }

            // sleeping task so we dont hammer the API
            await Task.Delay(1000);
        }

        return result;
    }

    /// <summary>
    /// Get an anime.
    /// </summary>
    /// <param name="id">ID of the anime you want to get.</param>
    /// <returns></returns>
    public async Task<ShikimoriMedia?> GetAnime(int id) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{_apiBaseUrl}/animes/{id}"
        };
        
        var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Shikimori, AuthApiCall.CallType.GET, url.Build(), requestHeaders: _requestHeaders);
        if (apiCall == null) {
            return null;
        }

        try {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            return JsonSerializer.Deserialize<ShikimoriMedia>(await streamReader.ReadToEndAsync());
        } catch (Exception e) {
            _logger.LogError($"Could not deserialize anime, reason: {e.Message}");
            throw;
        }
    }
}