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
using jellyfin_ani_sync.Extensions;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Interfaces;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Mal;
using jellyfin_ani_sync.Models.Shikimori;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api.Shikimori;

public class ShikimoriApiCalls (IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, IAsyncDelayer delayer, Dictionary<string, string>? requestHeaders, UserConfig userConfig)
    : IApiCallHelpers {
    private readonly ILogger<ShikimoriApiCalls> _logger = loggerFactory.CreateLogger<ShikimoriApiCalls>();
    private readonly AuthApiCall _authApiCall = new (httpClientFactory, serverApplicationHost, httpContextAccessor, loggerFactory, memoryCache, delayer, userConfig: userConfig);
    private const string ApiBaseUrl = "https://shikimori.one/api";
    private const int PageLimit = 50;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()  {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private  const string GraphqlQuery = """

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
                                              
                                          """;

    public class User {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("nickname")] public string Name { get; set; }
    }

    /// <summary>
    /// Get a users information.
    /// </summary>
    public async Task<User?> GetUserInformation() {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiBaseUrl}/users/whoami"
        };
        var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Shikimori, AuthApiCall.CallType.GET, url.Build(), requestHeaders: requestHeaders);
        if (apiCall == null) return null;
        StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
        string streamText = await streamReader.ReadToEndAsync();

        try {
            User? user = JsonSerializer.Deserialize<User>(streamText);

            if (user == null) return user;
            userConfig?.KeyPairs.Add(new KeyPairs {
                Key = "ShikimoriUserId",
                Value = user.Id.ToString()
            });

            Plugin.Instance?.SaveConfiguration();

            return user;
        } catch (Exception) {
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

        List<ShikimoriAnime>? result = null;
        for (int page = 1; page < maxPages; page++) {
            GraphqlRequest request = new GraphqlRequest {
                Query = GraphqlQuery,
                OperationName = "SearchAnime",
                Variables = new Dictionary<string, object>() {
                    { "search", searchString },
                    { "limit", PageLimit },
                    { "page", page },
                },
            };

            Dictionary<string, List<ShikimoriAnime>>? data = await GraphqlApiCall<Dictionary<string, List<ShikimoriAnime>>>(request);
            if (data == null) {
                break;
            }

            if (!data.TryGetValue("animes", out var anime)) {
                _logger.LogWarning("GraphQL response does not contain animes query");
                break;
            }

            if (result != null) {
                result.AddRange(anime);
            } else {
                result = anime;
            }

            if (anime.Count < PageLimit) {
                break;
            }
        }
        
        return result;
    }

    public async Task<Anime?> GetAnime(int? id, string? alternativeId = null, bool getRelated = false) {
        if (alternativeId == null) return null;
        ShikimoriAnime? anime = await GetAnime(alternativeId, getRelated);
        if (anime == null) return null;

        return ClassConversions.ConvertShikimoriAnime(anime);
    }

    public async Task<UpdateAnimeStatusResponse?> UpdateAnime(int animeId, int numberOfWatchedEpisodes, Status status, bool? isRewatching = null, int? numberOfTimesRewatched = null, DateTime? startDate = null, DateTime? endDate = null, string alternativeId = null, AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse ids = null, bool? isShow = null) {
        if (await UpdateAnime(alternativeId, status.ToShikimoriStatus(), numberOfWatchedEpisodes, numberOfTimesRewatched)) {
            return new UpdateAnimeStatusResponse();
        }

        return null;
    }

    public async Task<MalApiCalls.User?> GetUser() {
        User? user = await GetUserInformation();
        if (user != null) {
            return new MalApiCalls.User {
                Id = user.Id,
                Name = user.Name
            };
        }

        return null;
    }

    public async Task<List<Anime>?> GetAnimeList(Status status, int? userId = null) {
        List<ShikimoriAnime>? animeList = await GetUserAnimeList(status.ToShikimoriStatus());
        if (animeList == null) return null;
        List<Anime> convertedList = new List<Anime>();
        foreach (ShikimoriAnime shikimoriAnime in animeList) {
            convertedList.Add(ClassConversions.ConvertShikimoriAnime(shikimoriAnime));
        }

        return convertedList;
    }

    /// <summary>
    /// Get an anime.
    /// </summary>
    /// <param name="id">ID of the anime you want to get.</param>
    /// <param name="getRelated">True to request related anime from the API.</param>
    /// <returns>Nullable <see cref="ShikimoriAnime"/></returns>
    public async Task<ShikimoriAnime?> GetAnime(string id, bool getRelated = false) {
        GraphqlRequest request = new GraphqlRequest {
            Query = GraphqlQuery,
            OperationName = "GetAnime",
            Variables = new Dictionary<string, object>() {
                { "id", id },
                { "getRelated", getRelated },
            },
        };

        Dictionary<string, List<ShikimoriAnime>>? data = await GraphqlApiCall<Dictionary<string, List<ShikimoriAnime>>>(request);
        if (data == null) {
            return null;
        }

        if (!data.TryGetValue("animes", out var anime)) {
            _logger.LogWarning("GraphQL response does not contain animes query");
            return null;
        }

        if (anime.Count == 0) {
            return null;
        }

        return anime[0];
    }

    /// <summary>
    /// Get anime users rates.
    /// </summary>
    /// <param name="status">Only retrieve user rate with a given status.</param>
    /// <returns>Nullable list of <see cref="ShikimoriAnime"/></returns>
    public async Task<List<ShikimoriAnime>?> GetUserAnimeList(ShikimoriUserRate.StatusEnum? status = null) {
        string myList;
        if (status == null) {
            myList = string.Join(",", Enum.GetValues<ShikimoriUserRate.StatusEnum>().Select(x => x.ToString()));
        } else {
            myList = status.Value.ToString();
        }

        List<ShikimoriAnime>? result = null;
        for (int page = 1; ; page++) {
            GraphqlRequest request = new GraphqlRequest {
                Query = GraphqlQuery,
                OperationName = "GetUserAnimeList",
                Variables = new Dictionary<string, object>() {
                    { "page", page },
                    { "limit", PageLimit },
                    { "mylist", myList },
                },
            };

            var data = await GraphqlApiCall<Dictionary<string, List<ShikimoriAnime>>>(request);
            if (data == null) {
                return null;
            }

            if (!data.TryGetValue("animes", out var anime)) {
                _logger.LogWarning("GraphQL response does not contain animes query");
                return null;
            }

            if (result != null) {
                result.AddRange(anime);
            } else {
                result = anime;
            }

            if (anime.Count < PageLimit) {
                break;
            }
        }
        return result;
    }

    /// <summary>
    /// Update anime in the users list.
    /// </summary>
    /// <param name="id">ID of the anime to update.</param>
    /// <param name="updateStatus">Status to update anime to.</param>
    /// <param name="progress">Progress to update anime to.</param>
    /// <param name="numberOfTimesRewatched">Number of times rewatched to update anime to.</param>
    /// <returns>True if successful, false if not.</returns>
    public async Task<bool> UpdateAnime(string id, ShikimoriUserRate.StatusEnum updateStatus, int progress, int? numberOfTimesRewatched = null) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiBaseUrl}/v2/user_rates"
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
        
        StringContent stringContent = new StringContent(JsonSerializer.Serialize(updateBody, _jsonSerializerOptions), Encoding.UTF8, "application/json");
        HttpResponseMessage? apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Shikimori, AuthApiCall.CallType.POST, url.Build(), stringContent: stringContent, requestHeaders: requestHeaders);
        if (apiCall != null) {
            return apiCall.IsSuccessStatusCode;
        }

        return false;
    }

    private async Task<string?> GetUserId() {
        string? shikimoriUserId = userConfig?.KeyPairs.FirstOrDefault(keypair => keypair.Key == "ShikimoriUserId")?.Value;
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
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiBaseUrl}/graphql"
        };

        // See also https://graphql.org/learn/serving-over-http
        StringContent stringContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonSerializerOptions),
            Encoding.UTF8, "application/json");
        HttpResponseMessage? apiCall = await _authApiCall.AuthenticatedApiCall(
            ApiName.Shikimori, AuthApiCall.CallType.POST, url.Build(),
            stringContent: stringContent,
            requestHeaders: requestHeaders);
        if (apiCall == null) {
            return null;
        }

        GraphqlResponse<T>? response;
        try {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            response = JsonSerializer.Deserialize<GraphqlResponse<T>>(await streamReader.ReadToEndAsync());
        } catch (Exception e) {
            _logger.LogError("Could not deserialize GraphQL response, reason: {EMessage}", e.Message);
            return null;
        }

        if (response?.Errors != null && response.Errors.Any()) {
            foreach (GraphqlError error in response.Errors) {
                _logger.LogError("GraphQL error: {ErrorMessage}", error.Message);
            }
            return null;
        }

        if (response?.Data == null) {
            _logger.LogError("GraphQL returned empty data");
            return null;
        }

        return response.Data;
    }

    async Task<List<Anime>> IApiCallHelpers.SearchAnime(string query) {
        bool updateNsfw = Plugin.Instance?.PluginConfiguration?.updateNsfw != null && Plugin.Instance.PluginConfiguration.updateNsfw;
        List<ShikimoriAnime>? animeList = await SearchAnime(query);

        return ShikimoriSearchAnimeConvertedList(animeList, updateNsfw);
    }

    internal static List<Anime> ShikimoriSearchAnimeConvertedList(List<ShikimoriAnime>? animeList, bool updateNsfw) {
        List<Anime> convertedList = new List<Anime>();
        if (animeList == null) return convertedList;
        foreach (ShikimoriAnime shikimoriAnime in animeList) {
            if (!updateNsfw && shikimoriAnime.IsCensored == true) continue;
            convertedList.Add(ClassConversions.ConvertShikimoriAnime(shikimoriAnime));
        }

        return convertedList;
    }
}
