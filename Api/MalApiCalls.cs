using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Models;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Authentication;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api;

public class MalApiCalls {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MalApiCalls> _logger;
    private readonly string _refreshTokenUrl = "https://myanimelist.net/v1/oauth2/token";
    private readonly string _apiBaseUrl = "https://api.myanimelist.net/";
    private readonly int _apiVersion = 2;

    private string ApiUrl => _apiBaseUrl + "v" + _apiVersion;

    public MalApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory) {
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger<MalApiCalls>();
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
    public User GetUserInformation() {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiUrl}/users/@me"
        };
        StreamReader streamReader = new StreamReader(MalApiCall(CallType.GET, url.Build()).Content.ReadAsStream());
        string streamText = streamReader.ReadToEnd();

        return JsonSerializer.Deserialize<User>(streamText);
    }

    /// <summary>
    /// Search the MAL database for anime.
    /// </summary>
    /// <param name="query">Search by title.</param>
    /// <param name="fields">The fields you would like returned.</param>
    /// <returns>List of anime.</returns>
    public List<Anime> SearchAnime(string? query, string[]? fields) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiUrl}/anime"
        };
        if (query != null) {
            url.Parameters.Add(new KeyValuePair<string, string>("q", query));
        }

        if (fields != null) {
            url.Parameters.Add(new KeyValuePair<string, string>("fields", String.Join(",", fields)));
        }

        string builtUrl = url.Build();
        _logger.LogInformation($"Starting search for anime ({builtUrl})...");
        StreamReader streamReader = new StreamReader(MalApiCall(CallType.GET, builtUrl).Content.ReadAsStream());
        var animeList = JsonSerializer.Deserialize<SearchAnimeResponse>(streamReader.ReadToEnd());

        return animeList.Data.Select(list => list.Anime).ToList();
    }

    public enum Status {
        Watching,
        Completed,
        On_hold,
        Dropped,
        Plan_to_watch
    }

    public enum Sort {
        List_score,
        List_updated_at,
        Anime_title,
        Anime_start_date,
        Anime_id
    }

    public List<Anime> GetUserAnimeList(Status? status = null, Sort? sort = null, int? idSearch = null) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiUrl}/users/@me/animelist"
        };

        if (status != null) {
            url.Parameters.Add(new KeyValuePair<string, string>("status", status.Value.ToString().ToLower()));
        }

        if (sort != null) {
            url.Parameters.Add(new KeyValuePair<string, string>("sort", sort.Value.ToString().ToLower()));
        }

        string builtUrl = url.Build();
        UserAnimeList userAnimeList = new UserAnimeList {Data = new List<AnimeList>()};
        while (builtUrl != null) {
            _logger.LogInformation($"Getting user anime list ({builtUrl})...");
            StreamReader streamReader = new StreamReader(MalApiCall(CallType.GET, builtUrl).Content.ReadAsStream());
            UserAnimeList userAnimeListPage = JsonSerializer.Deserialize<UserAnimeList>(streamReader.ReadToEnd());

            if (userAnimeListPage?.Data != null && userAnimeListPage.Data.Count > 0) {
                if (idSearch != null) {
                    var foundAnime = userAnimeListPage.Data.FirstOrDefault(anime => anime.Anime.Id == idSearch);
                    if (foundAnime != null) {
                        return new List<Anime> { foundAnime.Anime };
                    }
                } else {
                    userAnimeList.Data = userAnimeList.Data.Concat(userAnimeListPage.Data).ToList();
                }

                builtUrl = userAnimeListPage.Paging.Next;
            }
        }

        return userAnimeList.Data.Select(list => list.Anime).ToList();
    }

    public enum CallType {
        GET,
        POST,
        PATCH,
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
    private HttpResponseMessage MalApiCall(CallType callType, string url, FormUrlEncodedContent formUrlEncodedContent = null) {
        int attempts = 0;
        var auth = Plugin.Instance.PluginConfiguration.ApiAuth.FirstOrDefault(item => item.Name == ApiName.Mal);
        while (attempts < 2) {
            var client = _httpClientFactory.CreateClient(NamedClient.Default);

            if (auth == null) {
                _logger.LogError("Could not find authentication details, please authenticate the plugin first");
                throw new NullReferenceException();
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            HttpResponseMessage responseMessage;
            switch (callType) {
                case CallType.GET:
                    responseMessage = client.GetAsync(url).Result;
                    break;
                case CallType.POST:
                    responseMessage = client.PostAsync(url, formUrlEncodedContent).Result;
                    break;
                case CallType.PATCH:
                    responseMessage = client.PatchAsync(url, formUrlEncodedContent).Result;
                    break;
                case CallType.DELETE:
                    responseMessage = client.DeleteAsync(url).Result;
                    break;
                default:
                    responseMessage = client.GetAsync(url).Result;
                    break;
            }

            if (responseMessage.IsSuccessStatusCode) {
                return responseMessage;
            } else {
                if (responseMessage.StatusCode == HttpStatusCode.Unauthorized) {
                    // token has probably expired; try refreshing it
                    var newAuth = new MalApiAuthentication(_httpClientFactory).GetMalToken(refreshToken: auth.RefreshToken);
                    // and then make the call again, using the new auth details
                    auth = newAuth;
                    attempts++;
                } else {
                    _logger.LogError($"Unable to complete MAL API call, reason: {responseMessage.StatusCode}; {responseMessage.ReasonPhrase}");
                    throw new Exception();
                }
            }
        }

        _logger.LogError("Unable to authenticate the MAL API call, re-authenticate the plugin");
        throw new AuthenticationException();
    }
}