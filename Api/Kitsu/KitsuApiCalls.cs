using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Kitsu;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api.Kitsu;

public class KitsuApiCalls {
    private readonly string ApiUrl = "https://kitsu.io/api/edge";
    private readonly ILogger<KitsuApiCalls> _logger;
    private readonly AuthApiCall _authApiCall;

    public KitsuApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, UserConfig userConfig = null) {
        _logger = loggerFactory.CreateLogger<KitsuApiCalls>();
        _authApiCall = new AuthApiCall(ApiName.Kitsu, httpClientFactory, serverApplicationHost, httpContextAccessor, loggerFactory, userConfig: userConfig);
    }

    public async Task<List<KitsuSearch.KitsuAnime>> SearchAnime(string query) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiUrl}/anime"
        };

        if (query != null) {
            query = StringFormatter.RemoveSpaces(query);

            url.Parameters.Add(new KeyValuePair<string, string>("filter[text]", query));
        }

        string builtUrl = url.Build();
        _logger.LogInformation($"(Kitsu) Starting search for anime (GET {builtUrl})...");
        var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Kitsu, MalApiCalls.CallType.GET, builtUrl);
        if (apiCall != null) {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            var animeList = JsonSerializer.Deserialize<KitsuSearch.KitsuSearchMedia>(await streamReader.ReadToEndAsync());

            _logger.LogInformation("Search complete");
            return animeList.KitsuSearchData;
        }

        return null;
    }

    public async Task<MalApiCalls.User> GetUserInformation() {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiUrl}/users",
            Parameters = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("filter[self]", "true")
            }
        };

        _logger.LogInformation($"(Kitsu) Retrieving user information...");
        var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Kitsu, MalApiCalls.CallType.GET, url.Build());
        if (apiCall != null) {
            var xd = await apiCall.Content.ReadAsStringAsync();
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            var animeList = JsonSerializer.Deserialize<KitsuGetUser.KitsuUserRoot>(await streamReader.ReadToEndAsync());

            _logger.LogInformation("(Kitsu) Retrieved user information");
            return new MalApiCalls.User{ Id = animeList.KitsuUserList[0].Id, Name = animeList.KitsuUserList[0].KitsuUser.Name};
        }

        return null;
    }

    public async Task<KitsuSearch.KitsuAnime> GetAnime(int animeId) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiUrl}/anime/{animeId}",
        };

        string builtUrl = url.Build();
        _logger.LogInformation($"(Kitsu) Retrieving an anime from Kitsu (GET {builtUrl})...");
        try {
            var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Kitsu, MalApiCalls.CallType.GET, builtUrl);
            if (apiCall != null) {
                StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
                var anime = JsonSerializer.Deserialize<KitsuGet.KitsuGetAnime>(await streamReader.ReadToEndAsync());
                _logger.LogInformation("(Kitsu) Anime retrieval complete");
                return anime.KitsuAnimeData;
            }
        } catch (Exception e) {
            _logger.LogError(e.Message);
        }

        return null;
    }

    public async Task<bool> UpdateAnimeStatus(int animeId, int numberOfWatchedEpisodes, KitsuUpdate.Status status,
        bool? isRewatching = null, int? numberOfTimesRewatched = null, DateTime? startDate = null, DateTime? endDate = null) {
        _logger.LogInformation($"(Kitsu) Preparing to update anime {animeId} status...");

        MalApiCalls.User user = await GetUserInformation();
        if (user != null) {
            var libraryStatus = await GetUserAnimeStatus(user.Id, animeId);
            // if this is populated, it means there is already a record of the anime in the users anime list. therefore we update the record instead of create a new one
            UrlBuilder url = new UrlBuilder {
                Base = $"{ApiUrl}/library-entries{(libraryStatus != null ? $"/{libraryStatus.Id}" : "")}"
            };

            var payload = new KitsuUpdate.KitsuLibraryEntryPostPatchRoot {
                Data = new KitsuUpdate.KitsuLibraryEntry {
                    Type = "libraryEntries",
                    Attributes = new KitsuUpdate.Attributes {
                        Status = status,
                        Progress = numberOfWatchedEpisodes
                    },
                    Relationships = new KitsuUpdate.Relationships {
                        AnimeData = new KitsuUpdate.AnimeData {
                            Anime = new KitsuSearch.KitsuAnime {
                                Type = "anime",
                                Id = animeId
                            }
                        },
                        UserData = new KitsuUpdate.UserData {
                            User = new KitsuGetUser.KitsuUser {
                                Type = "users",
                                Id = user.Id
                            }
                        }
                    }
                }
            };

            if (libraryStatus != null) {
                payload.Data.Id = libraryStatus.Id;
            }

            if (isRewatching != null) {
                payload.Data.Attributes.Reconsuming = isRewatching.Value;
            }

            if (numberOfTimesRewatched != null) {
                payload.Data.Attributes.ReconsumeCount = numberOfTimesRewatched.Value;
            }

            if (startDate != null) {
                payload.Data.Attributes.StartedAt = startDate.Value;
            }

            if (endDate != null) {
                payload.Data.Attributes.FinishedAt = endDate.Value;
            }

            var jsonSerializerOptions = new JsonSerializerOptions {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var stringContent = new StringContent(JsonSerializer.Serialize(payload, jsonSerializerOptions), Encoding.UTF8, "application/vnd.api+json");
            var xd = new StringContent(JsonSerializer.Serialize(payload, jsonSerializerOptions), Encoding.UTF8, "application/vnd.api+json").ReadAsStringAsync().Result;
            var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Kitsu, libraryStatus != null ? MalApiCalls.CallType.PATCH : MalApiCalls.CallType.POST, url.Build(), stringContent: stringContent);

            return apiCall.IsSuccessStatusCode;
        }

        return false;
    }

    public async Task<KitsuUpdate.KitsuLibraryEntry> GetUserAnimeStatus(int userId, int animeId) {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiUrl}/library-entries",
            Parameters = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("filter[animeId]", animeId.ToString()),
                new KeyValuePair<string, string>("filter[userId]", userId.ToString())
            }
        };

        _logger.LogInformation("(Kitsu) Fetching current user anime list status...");
        try {
            var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Kitsu, MalApiCalls.CallType.GET, url.Build());

            if (apiCall != null) {
                StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
                var library = JsonSerializer.Deserialize<KitsuUpdate.KitsuLibraryEntryListRoot>(await streamReader.ReadToEndAsync());
                _logger.LogInformation("(Kitsu) Fetched user anime list");
                if (library is { Data: { } } && library.Data[0] != null) {
                    return library.Data[0];
                }
            }
        } catch (Exception e) {
            _logger.LogError(e.Message);
        }

        return null;
    }
}