using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Mal;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api {
    public class MalApiCalls {
        private readonly ILogger<MalApiCalls> _logger;
        private readonly AuthApiCall _authApiCall;
        private readonly string _refreshTokenUrl = "https://myanimelist.net/v1/oauth2/token";
        private readonly string _apiBaseUrl = "https://api.myanimelist.net/";
        private readonly int _apiVersion = 2;

        private string ApiUrl => _apiBaseUrl + "v" + _apiVersion;

        public MalApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, UserConfig userConfig = null) {
            _logger = loggerFactory.CreateLogger<MalApiCalls>();
            _authApiCall = new AuthApiCall(ApiName.Mal, httpClientFactory, serverApplicationHost, httpContextAccessor, loggerFactory, userConfig: userConfig);
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
            var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Mal, AuthApiCall.CallType.GET, url.Build());
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
        public async Task<List<Anime>> SearchAnime(string? query, string[]? fields, bool updateNsfw = false) {
            UrlBuilder url = new UrlBuilder {
                Base = $"{ApiUrl}/anime"
            };
            if (query != null) {
                // must truncate query to 64 characters. MAL API returns an error otherwise
                query = StringFormatter.RemoveSpaces(query);
                if (query.Length > 64) {
                    query = query.Substring(0, 64);
                }

                url.Parameters.Add(new KeyValuePair<string, string>("q", query));
                if (updateNsfw) {
                    url.Parameters.Add(new KeyValuePair<string, string>("nsfw", "true"));
                }
            }

            if (fields != null) {
                url.Parameters.Add(new KeyValuePair<string, string>("fields", String.Join(",", fields)));
            }

            string builtUrl = url.Build();
            _logger.LogInformation($"Starting search for anime (GET {builtUrl})...");
            var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Mal, AuthApiCall.CallType.GET, builtUrl);
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
                var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Mal, AuthApiCall.CallType.GET, builtUrl);
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
                var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Mal, AuthApiCall.CallType.GET, builtUrl);
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

                        if (userAnimeListPage.Paging.Next != null) {
                            builtUrl = userAnimeListPage.Paging.Next;
                            _logger.LogInformation($"Additional pages found; waiting 2 seconds before calling again...");
                            Thread.Sleep(2000);
                        } else {
                            builtUrl = null;
                        }
                    } else {
                        builtUrl = null;
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

            if (isRewatching != null && isRewatching.Value) {
                body.Add(new KeyValuePair<string, string>("is_rewatching", true.ToString()));
            } else {
                body.Add(new KeyValuePair<string, string>("is_rewatching", false.ToString().ToLower()));
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
                var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Mal, AuthApiCall.CallType.PUT, builtUrl, new FormUrlEncodedContent(body.ToArray()));

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
    }
}
