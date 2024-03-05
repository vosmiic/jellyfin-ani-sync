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
    private readonly int _sleepDelay = 1000;
    private readonly UserConfig? _userConfig;
    private readonly Dictionary<string, string>? _requestHeaders;

    private readonly string _graphqlQuery = @"
      query SearchAnime($search: String!, $page: Int!, $limit: Int!) {
        animes(search: $search, page: $page, limit: $limit) {
          ...AnimeFields
        }
      }

      query GetAnime($id: String!, $getRelated: Boolean!) {
        animes(ids: $id) {
          ...AnimeFields

          userRate {
            ...UserRateFields
          }

          related @include(if: $getRelated) {
            anime {
              ...AnimeFields
            }
            relationEn
          }
        }
      }

      query GetUserAnimeList($mylist: MylistString!, $page: Int!, $limit: Int!) {
        animes(mylist: $mylist, page: $page, limit: $limit) {
          ...AnimeFields
          userRate {
            ...UserRateFields
          }
        }
      }

      fragment UserRateFields on UserRate {
         status
         rewatches
         episodes
      }

      fragment AnimeFields on Anime {
        id
        malId
        name
        episodes
        synonyms
        russian
        english
        japanese
        isCensored
      }
    ";

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
    public async Task<List<ShikimoriAnime>?> SearchAnime(string searchString) {
        const int maxPages = 10;
        const int limit = 50;

        List<ShikimoriAnime>? result = null;
        for (int page = 1; page < maxPages; page++) {
            var request = new GraphqlRequest {
                Query = _graphqlQuery,
                OperationName = "SearchAnime",
                Variables = new Dictionary<string, object>() {
                    { "search", searchString },
                    { "limit", limit },
                    { "page", page },
                },
            };

            var data = await GraphqlApiCall<Dictionary<string, List<ShikimoriAnime>>>(request);
            if (data == null) {
                break;
            }

            List<ShikimoriAnime> animes;
            if (!data.TryGetValue("animes", out animes)) {
                _logger.LogWarning("GraphQL response does not contain animes query");
                break;
            }

            if (result != null) {
                result.AddRange(animes);
            } else {
                result = animes;
            }

            if (animes.Count < limit) {
                break;
            }

            // sleeping task so we dont hammer the API
            await Task.Delay(_sleepDelay);
        }
        return result;
    }

    /// <summary>
    /// Get an anime.
    /// </summary>
    /// <param name="id">ID of the anime you want to get.</param>
    /// <returns></returns>
    public async Task<ShikimoriAnime?> GetAnime(string id, bool getRelated = false) {
        var request = new GraphqlRequest {
            Query = _graphqlQuery,
            OperationName = "GetAnime",
            Variables = new Dictionary<string, object>() {
                { "id", id },
                { "getRelated", getRelated },
            },
        };

        var data = await GraphqlApiCall<Dictionary<string, List<ShikimoriAnime>>>(request);
        if (data == null) {
            return null;
        }

        List<ShikimoriAnime> animes;
        if (!data.TryGetValue("animes", out animes)) {
            _logger.LogWarning("GraphQL response does not contain animes query");
            return null;
        }

        if (!animes.Any()) {
            return null;
        }

        return animes[0];
    }

    /// <summary>
    /// Get a anime users rates.
    /// </summary>
    /// <param name="status">Only retrieve user rate with a given status.</param>
    /// <returns></returns>
    public async Task<List<ShikimoriAnime>?> GetUserAnimeList(ShikimoriUserRate.StatusEnum? status = null) {
        const int limit = 50;

        string mylist;
        if (status == null) {
            mylist = String.Join(
                ",",
                Enum.GetValues(typeof(ShikimoriUserRate.StatusEnum))
                    .Cast<ShikimoriUserRate.StatusEnum>()
                    .Select(x => x.ToString()));
        } else {
            mylist = status.Value.ToString();
        }

        List<ShikimoriAnime>? result = null;
        for (int page = 1; ; page++) {
            var request = new GraphqlRequest {
                Query = _graphqlQuery,
                OperationName = "GetUserAnimeList",
                Variables = new Dictionary<string, object>() {
                    { "page", page },
                    { "limit", limit },
                    { "mylist", mylist },
                },
            };

            var data = await GraphqlApiCall<Dictionary<string, List<ShikimoriAnime>>>(request);
            if (data == null) {
                return null;
            }

            List<ShikimoriAnime> animes;
            if (!data.TryGetValue("animes", out animes)) {
                _logger.LogWarning("GraphQL response does not contain animes query");
                return null;
            }

            if (result != null) {
                result.AddRange(animes);
            } else {
                result = animes;
            }

            if (animes.Count < limit) {
                break;
            }

            await Task.Delay(_sleepDelay);
        }
        return result;
    }

    public async Task<bool> UpdateAnime(string id, ShikimoriUserRate.StatusEnum updateStatus, int progress, int? numberOfTimesRewatched = null) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{_apiBaseUrl}/v2/user_rates"
        };

        string? shikimoriUserId = await GetUserId();
        if (shikimoriUserId == null) return false;

        ShikimoriUpdate.UpdateBody updateBody = new ShikimoriUpdate.UpdateBody {
            UserRate = new ShikimoriUpdate.UserRate {
                AnimeId = id,
                UserId = shikimoriUserId,
                Episodes = progress,
                Status = updateStatus,
            }
        };

        if (numberOfTimesRewatched != null) {
            updateBody.UserRate.Rewatches = numberOfTimesRewatched.Value;
        }

        var jsonSerializerOptions = new JsonSerializerOptions {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        var stringContent = new StringContent(JsonSerializer.Serialize(updateBody, jsonSerializerOptions), Encoding.UTF8, "application/json");
        var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Shikimori, AuthApiCall.CallType.POST, url.Build(), stringContent: stringContent, requestHeaders: _requestHeaders);
        if (apiCall != null) {
            return apiCall.IsSuccessStatusCode;
        }

        return false;
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

    private class GraphqlRequest {
        [JsonPropertyName("query")]
        public string Query { get; set; }
        [JsonPropertyName("operationName")]
        public string? OperationName { get; set; }
        [JsonPropertyName("variables")]
        public Dictionary<string, object>? Variables { get; set; }
    }

    public class GraphqlResponse<DataType> {
        [JsonPropertyName("data")]
        public DataType? Data { get; set; }
        [JsonPropertyName("errors")]
        public List<GraphqlError>? Errors { get; set; }
    }

    public class GraphqlError {
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

    private async Task<T?> GraphqlApiCall<T>(GraphqlRequest request) where T: class {
        var url = new UrlBuilder {
            Base = $"{_apiBaseUrl}/graphql"
        };

        var jsonSerializerOptions = new JsonSerializerOptions {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // See also https://graphql.org/learn/serving-over-http
        var stringContent = new StringContent(
            JsonSerializer.Serialize(request, jsonSerializerOptions),
            Encoding.UTF8, "application/json");
        var apiCall = await _authApiCall.AuthenticatedApiCall(
            ApiName.Shikimori, AuthApiCall.CallType.POST, url.Build(),
            stringContent: stringContent,
            requestHeaders: _requestHeaders);
        if (apiCall == null) {
            return null;
        }

        GraphqlResponse<T> response;
        try {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            response = JsonSerializer.Deserialize<GraphqlResponse<T>>(await streamReader.ReadToEndAsync());
        } catch (Exception e) {
            _logger.LogError($"Could not deserialize GraphQL response, reason: {e.Message}");
            return null;
        }

        if (response.Errors != null && response.Errors.Any()) {
            foreach (GraphqlError error in response.Errors) {
                _logger.LogError($"GraphQL error: {error.Message}");
            }
            return null;
        }

        if (response.Data == null) {
            _logger.LogError("GraphQL returned empty data");
            return null;
        }

        return response.Data;
    }
}
