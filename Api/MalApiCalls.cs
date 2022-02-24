using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Models;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api {
    public class MalApiCalls {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MalApiCalls> _logger;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public UserConfig UserConfig { get; set; }
        private readonly string _refreshTokenUrl = "https://myanimelist.net/v1/oauth2/token";
        private readonly string _apiBaseUrl = "https://api.myanimelist.net/";
        private readonly int _apiVersion = 2;

        private string ApiUrl => _apiBaseUrl + "v" + _apiVersion;

        public MalApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor) {
            _httpClientFactory = httpClientFactory;
            _logger = loggerFactory.CreateLogger<MalApiCalls>();
            _serverApplicationHost = serverApplicationHost;
            _httpContextAccessor = httpContextAccessor;
        }

        public class User {
            [JsonPropertyName("id")] public int Id { get; set; }
            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("location")] public string Location { get; set; }
            [JsonPropertyName("joined_at")] public DateTime JoinedAt { get; set; }
            [JsonPropertyName("picture")] public string Picture { get; set; }
        }

        /// <summary>
        /// Get a users information.
        /// </summary>
        public async Task<User> GetUserInformation() {
            UrlBuilder url = new UrlBuilder {
                Base = $"{ApiUrl}/users/@me"
            };
            var apiCall = await MalApiCall(CallType.GET, url.Build());
            if (apiCall != null) {
                StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
                string streamText = await streamReader.ReadToEndAsync();

                return JsonSerializer.Deserialize<User>(streamText);
            } else {
                return null;
            }
        }

        /// <summary>
        /// Search the MAL database for anime.
        /// </summary>
        /// <param name="query">Search by title.</param>
        /// <param name="fields">The fields you would like returned.</param>
        /// <returns>List of anime.</returns>
        public async Task<List<Anime>> SearchAnime(string? query, string[]? fields) {
            UrlBuilder url = new UrlBuilder {
                Base = $"{ApiUrl}/anime"
            };
            if (query != null) {
                // must truncate query to 64 characters. MAL API returns an error otherwise
                query = StringFormatter.RemoveSpecialCharacters(query);
                if (query.Length > 64) {
                    query = query.Substring(0, 64);
                }
                url.Parameters.Add(new KeyValuePair<string, string>("q", query));
            }

            if (fields != null) {
                url.Parameters.Add(new KeyValuePair<string, string>("fields", String.Join(",", fields)));
            }

            string builtUrl = url.Build();
            _logger.LogInformation($"Starting search for anime (GET {builtUrl})...");
            var apiCall = await MalApiCall(CallType.GET, builtUrl);
            if (apiCall != null) {
                StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
                var animeList = JsonSerializer.Deserialize<SearchAnimeResponse>(await streamReader.ReadToEndAsync());

                _logger.LogInformation("Search complete");
                return animeList.Data.Select(list => list.Anime).ToList();
            } else {
                return null;
            }
        }

        /// <summary>
        /// Get an anime from the MAL database.
        /// </summary>
        /// <returns></returns>
        public async Task<Anime> GetAnime(int animeId, string[]? fields = null) {
            UrlBuilder url = new UrlBuilder {
                Base = $"{ApiUrl}/anime/{animeId}"
            };

            if (fields != null) {
                url.Parameters.Add(new KeyValuePair<string, string>("fields", String.Join(",", fields)));
            }

            string builtUrl = url.Build();
            _logger.LogInformation($"Retrieving an anime from MAL (GET {builtUrl})...");
            try {
                var apiCall = await MalApiCall(CallType.GET, builtUrl);
                if (apiCall != null) {
                    StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new JsonStringEnumConverter());
                    var anime = JsonSerializer.Deserialize<Anime>(await streamReader.ReadToEndAsync(), options);
                    _logger.LogInformation("Anime retrieval complete");
                    return anime;
                } else {
                    return null;
                }
            } catch (Exception e) {
                _logger.LogError(e.Message);
                return null;
            }
        }

        public enum Sort {
            List_score,
            List_updated_at,
            Anime_title,
            Anime_start_date,
            Anime_id
        }

        public async Task<List<UserAnimeListData>> GetUserAnimeList(Status? status = null, Sort? sort = null, int? idSearch = null) {
            UrlBuilder url = new UrlBuilder {
                Base = $"{ApiUrl}/users/@me/animelist"
            };

            url.Parameters.Add(new KeyValuePair<string, string>("fields", "list_status,num_episodes"));

            if (status != null) {
                url.Parameters.Add(new KeyValuePair<string, string>("status", status.Value.ToString().ToLower()));
            }

            if (sort != null) {
                url.Parameters.Add(new KeyValuePair<string, string>("sort", sort.Value.ToString().ToLower()));
            }

            string builtUrl = url.Build();
            UserAnimeList userAnimeList = new UserAnimeList { Data = new List<UserAnimeListData>() };
            while (builtUrl != null) {
                _logger.LogInformation($"Getting user anime list (GET {builtUrl})...");
                var apiCall = await MalApiCall(CallType.GET, builtUrl);
                if (apiCall != null) {
                    StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new JsonStringEnumConverter());
                    UserAnimeList userAnimeListPage = JsonSerializer.Deserialize<UserAnimeList>(await streamReader.ReadToEndAsync(), options);

                    if (userAnimeListPage?.Data != null && userAnimeListPage.Data.Count > 0) {
                        if (idSearch != null) {
                            var foundAnime = userAnimeListPage.Data.FirstOrDefault(anime => anime.Anime.Id == idSearch);
                            if (foundAnime != null) {
                                return new List<UserAnimeListData> { foundAnime };
                            }
                        } else {
                            userAnimeList.Data = userAnimeList.Data.Concat(userAnimeListPage.Data).ToList();
                        }

                        builtUrl = userAnimeListPage.Paging.Next;
                    }
                } else {
                    return null;
                }
            }

            _logger.LogInformation("Got user anime list");
            return userAnimeList.Data.ToList();
        }

        public async Task<UpdateAnimeStatusResponse> UpdateAnimeStatus(int animeId, int numberOfWatchedEpisodes, Status? status = null,
            bool? isRewatching = null, int? numberOfTimesRewatched = null, DateTime? startDate = null, DateTime? endDate = null) {
            UrlBuilder url = new UrlBuilder {
                Base = $"{ApiUrl}/anime/{animeId}/my_list_status"
            };

            List<KeyValuePair<string, string>> body = new List<KeyValuePair<string, string>> {
                new("num_watched_episodes", numberOfWatchedEpisodes.ToString())
            };

            if (status != null) {
                body.Add(new KeyValuePair<string, string>("status", status.Value.ToString().ToLower()));
            }

            if (isRewatching != null) {
                body.Add(new KeyValuePair<string, string>("is_rewatching", isRewatching.Value.ToString()));
            }

            if (numberOfTimesRewatched != null) {
                body.Add(new KeyValuePair<string, string>("num_times_rewatched", numberOfTimesRewatched.Value.ToString()));
            }

            if (startDate != null) {
                body.Add(new KeyValuePair<string, string>("start_date", startDate.Value.ToString("yyyy-MM-dd")));
            }

            if (endDate != null) {
                body.Add(new KeyValuePair<string, string>("finish_date", endDate.Value.ToString("yyyy-MM-dd")));
            }

            var builtUrl = url.Build();

            UpdateAnimeStatusResponse updateResponse;
            try {
                var apiCall = await MalApiCall(CallType.PUT, builtUrl, new FormUrlEncodedContent(body.ToArray()));

                if (apiCall != null) {
                    StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new JsonStringEnumConverter());
                    _logger.LogInformation($"Updating anime status (PUT {builtUrl})...");
                    updateResponse = JsonSerializer.Deserialize<UpdateAnimeStatusResponse>(await streamReader.ReadToEndAsync(), options);
                    _logger.LogInformation("Update complete");
                } else {
                    updateResponse = null;
                }
            } catch (Exception e) {
                _logger.LogError(e.Message);
                updateResponse = null;
            }

            return updateResponse;
        }

        public enum CallType {
            GET,
            POST,
            PATCH,
            PUT,
            DELETE
        }

        /// <summary>
        /// Make an MAL API call.
        /// </summary>
        /// <param name="callType">The type of call to make.</param>
        /// <param name="url">The URL that you want to make the request to.</param>
        /// <param name="formUrlEncodedContent">The form data to be posted.</param>
        /// <returns>API call response.</returns>
        /// <exception cref="NullReferenceException">Authentication details not found.</exception>
        /// <exception cref="Exception">Non-200 response.</exception>
        /// <exception cref="AuthenticationException">Could not authenticate with the MAL API.</exception>
        private async Task<HttpResponseMessage> MalApiCall(CallType callType, string url, FormUrlEncodedContent formUrlEncodedContent = null) {
            int attempts = 0;
            UserApiAuth auth;
            try {
                auth = UserConfig.UserApiAuth.FirstOrDefault(item => item.Name == ApiName.Mal);
            } catch (NullReferenceException) {
                _logger.LogError("Could not find authentication details, please authenticate the plugin first");
                throw;
            }

            while (attempts < 2) {
                var client = _httpClientFactory.CreateClient(NamedClient.Default);


                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
                HttpResponseMessage responseMessage = new HttpResponseMessage();
                try {
                    switch (callType) {
                        case CallType.GET:
                            responseMessage = await client.GetAsync(url);
                            break;
                        case CallType.POST:
                            responseMessage = await client.PostAsync(url, formUrlEncodedContent);
                            break;
                        case CallType.PATCH:
                            responseMessage = await client.PatchAsync(url, formUrlEncodedContent);
                            break;
                        case CallType.PUT:
                            responseMessage = await client.PutAsync(url, formUrlEncodedContent);
                            break;
                        case CallType.DELETE:
                            responseMessage = await client.DeleteAsync(url);
                            break;
                        default:
                            responseMessage = await client.GetAsync(url);
                            break;
                    }
                } catch (Exception e) {
                    _logger.LogError(e.Message);
                }


                if (responseMessage.IsSuccessStatusCode) {
                    return responseMessage;
                } else {
                    if (responseMessage.StatusCode == HttpStatusCode.Unauthorized) {
                        // token has probably expired; try refreshing it
                        UserApiAuth newAuth;
                        try {
                            newAuth = new MalApiAuthentication(_httpClientFactory, _serverApplicationHost, _httpContextAccessor).GetMalToken(UserConfig.UserId, refreshToken: auth.RefreshToken);
                        } catch (Exception) {
                            _logger.LogError("Could not re-authenticate. Please manually re-authenticate the user via the AniSync configuration page");
                            return null;
                        }

                        // and then make the call again, using the new auth details
                        auth = newAuth;
                        attempts++;
                    } else {
                        _logger.LogError($"Unable to complete MAL API call ({callType.ToString()} {url}), reason: {responseMessage.StatusCode}; {responseMessage.ReasonPhrase}");
                        return null;
                    }
                }
            }

            _logger.LogError("Unable to authenticate the MAL API call, re-authenticate the plugin");
            return null;
        }
    }
}