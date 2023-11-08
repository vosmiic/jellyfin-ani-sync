#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.JsonConverters;
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

        return await PagedCall<ShikimoriMedia>(url, 50);
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

        HttpResponseMessage? apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Shikimori, AuthApiCall.CallType.GET, url.Build(), requestHeaders: _requestHeaders);
        if (apiCall == null) {
            return null;
        }

        try {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            return JsonSerializer.Deserialize<ShikimoriMedia>(await streamReader.ReadToEndAsync());
        } catch (Exception e) {
            _logger.LogError($"Could not deserialize anime, reason: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get a users anime list.
    /// </summary>
    /// <param name="id">Only retrieve user progress of a single anime by ID.</param>
    /// <returns></returns>
    public async Task<List<ShikimoriUpdate.UserRate>?> GetUserAnimeList(int? id = null, ShikimoriUpdate.UpdateStatus? updateStatus = null) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{_apiBaseUrl}/v2/user_rates"
        };

        string? shikimoriUserId = await GetUserId();
        if (shikimoriUserId == null) return null;
        
        url.Parameters.Add(new KeyValuePair<string, string>("user_id", shikimoriUserId));
        if (id != null) {
            url.Parameters.Add(new KeyValuePair<string, string>("target_id", id.Value.ToString()));
            url.Parameters.Add(new KeyValuePair<string, string>("target_type", "Anime"));
        }

        if (updateStatus != null) {
            url.Parameters.Add(new KeyValuePair<string, string>("status", updateStatus.Value.ToString()));
        }
        
        HttpResponseMessage? apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Shikimori, AuthApiCall.CallType.GET, url.Build(), requestHeaders: _requestHeaders);
        if (apiCall == null) {
            return null;
        }

        try {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            return JsonSerializer.Deserialize<List<ShikimoriUpdate.UserRate>>(await streamReader.ReadToEndAsync());
        } catch (Exception e) {
            _logger.LogError($"Could not deserialize user anime list, reason: {e.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateAnime(int id, ShikimoriUpdate.UpdateStatus updateStatus, int progress, int? numberOfTimesRewatched = null) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{_apiBaseUrl}/v2/user_rates"
        };

        string? shikimoriUserId = await GetUserId();
        if (shikimoriUserId == null) return false;
        
        ShikimoriUpdate.UpdateBody updateBody = new ShikimoriUpdate.UpdateBody {
            UserRate = new ShikimoriUpdate.UserRate {
                AnimeId = id,
                UserId = int.Parse(shikimoriUserId),
                Episodes = progress,
                Status = updateStatus
            }
        };

        if (numberOfTimesRewatched != null) {
            updateBody.UserRate.Rewatches = numberOfTimesRewatched.Value;
        }

        var jsonSerializerOptions = new JsonSerializerOptions {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new IntToStringConverter.IntToStringJsonConverter() }
        };
        
        var stringContent = new StringContent(JsonSerializer.Serialize(updateBody, jsonSerializerOptions), Encoding.UTF8, "application/json");

        // get users current watch status of anime
        var currentAnimeStatus = await GetUserAnimeList(id);
        if (currentAnimeStatus == null) return false;
        AuthApiCall.CallType callType;
        if (currentAnimeStatus.Count == 0 || currentAnimeStatus.FirstOrDefault(ur => ur.Id == id) == null) {
            callType = AuthApiCall.CallType.POST;
        } else {
            callType = AuthApiCall.CallType.PATCH;
        }
        // wait before making next call
        await Task.Delay(1000);
        HttpResponseMessage? response = await _authApiCall.AuthenticatedApiCall(ApiName.Shikimori, callType, url.Build(), stringContent: stringContent, requestHeaders: _requestHeaders);
        if (response != null) {
            return response.IsSuccessStatusCode;
        }

        return false;
    }

    private async Task<List<T>?> PagedCall<T>(UrlBuilder url, int pageSize) {
        int page = 1;
        url.Parameters.Add(new KeyValuePair<string, string>("limit", pageSize.ToString()));
        url.Parameters.Add(new KeyValuePair<string, string>("page", page.ToString()));

        var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Shikimori, AuthApiCall.CallType.GET, url.Build(), requestHeaders: _requestHeaders);
        if (apiCall == null) {
            return null;
        }

        List<T>? result;
        try {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            result = JsonSerializer.Deserialize<List<T>>(await streamReader.ReadToEndAsync());
        } catch (Exception e) {
            _logger.LogError($"Could not deserialize result, reason: {e.Message}");
            return null;
        }

        if (result == null || result.Count == 0) return null;
        while (page < 10) {
            page++;

            url.Parameters.RemoveAll(item => item.Key == "page");
            url.Parameters.Add(new KeyValuePair<string, string>("page", page.ToString()));

            var xd = url.Build();
            HttpResponseMessage? pageApiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Shikimori, AuthApiCall.CallType.GET, xd);
            if (pageApiCall == null) break;
            List<T>? nextPageResult;
            try {
                StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
                nextPageResult = JsonSerializer.Deserialize<List<T>>(await streamReader.ReadToEndAsync());
            } catch (Exception e) {
                _logger.LogWarning($"Could not retrieve next result page, reason: {e.Message}");
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

    private async Task<string?> GetUserId() {
        string? shikimoriUserId = _userConfig?.KeyPairs.FirstOrDefault(keypair => keypair.Key == "ShikimoriUserId")?.Value;
        if (shikimoriUserId == null || string.IsNullOrEmpty(shikimoriUserId))  {
            _logger.LogInformation("No Shikimori user ID stored in the config. Attempting to retrieve...");
            User? user = await GetUserInformation();
            if (user != null) {
                shikimoriUserId = user.Id.ToString();
            } else {
                _logger.LogError("Could not retrieve Shikimori user ID");
                return null;
            }
        }

        return shikimoriUserId;
    }
}