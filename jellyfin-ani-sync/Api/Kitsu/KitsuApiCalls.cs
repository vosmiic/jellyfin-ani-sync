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
using jellyfin_ani_sync.Models.Kitsu;
using jellyfin_ani_sync.Models.Mal;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api.Kitsu {
    public class KitsuApiCalls : IApiCallHelpers {
        private readonly string ApiUrl = "https://kitsu.io/api/edge";
        private readonly ILogger<KitsuApiCalls> _logger;
        private readonly AuthApiCall _authApiCall;
        private readonly UserConfig _userConfig;

        public KitsuApiCalls(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, IAsyncDelayer delayer, UserConfig userConfig) {
            _logger = loggerFactory.CreateLogger<KitsuApiCalls>();
            _authApiCall = new AuthApiCall(httpClientFactory, serverApplicationHost, httpContextAccessor, loggerFactory, memoryCache, delayer, userConfig: userConfig);
            _userConfig = userConfig;
        }

        public async Task<List<KitsuSearch.KitsuAnime>> SearchAnime(string query) {
            UrlBuilder url = new UrlBuilder {
                Base = $"{ApiUrl}/anime"
            };

            if (query != null) {
                url.Parameters.Add(new KeyValuePair<string, string>("filter[text]", query));
            }

            string builtUrl = url.Build();
            _logger.LogInformation($"(Kitsu) Starting search for anime (GET {builtUrl})...");
            var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Kitsu, AuthApiCall.CallType.GET, builtUrl);
            if (apiCall != null) {
                StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
                var animeList = JsonSerializer.Deserialize<KitsuSearch.KitsuSearchMedia>(await streamReader.ReadToEndAsync());

                _logger.LogInformation("Search complete");
                return animeList.KitsuSearchData;
            }

            return null;
        }

        public async Task<Anime> GetAnime(int id, string alternativeId = null, bool getRelated = false) {
            KitsuGet.KitsuGetAnime anime = await GetAnime(id);
            if (anime == null) return null;
            Anime convertedAnime = ClassConversions.ConvertKitsuAnime(anime.KitsuAnimeData);

            convertedAnime.MyListStatus = await GetConvertedKitsuUserList(id);

            return convertedAnime;
        }

        /// <summary>
        /// Gets a users list from the Kitsu API and converts it to a <see cref="MyListStatus"/> object instance.
        /// </summary>
        /// <param name="id">The ID of the anime to get the user status of.</param>
        /// <returns>The converted user list status of the anime.</returns>
        internal async Task<MyListStatus> GetConvertedKitsuUserList(int id) {
            int? userId = await GetUserId();

            MyListStatus userList = new MyListStatus();

            if (userId != null) {
                KitsuUpdate.KitsuLibraryEntry userAnimeStatus = await GetUserAnimeStatus(userId.Value, id);
                if (userAnimeStatus is { Attributes: { } })
                    userList = new MyListStatus {
                        NumEpisodesWatched = userAnimeStatus.Attributes.Progress ?? 0,
                        IsRewatching = userAnimeStatus.Attributes.Reconsuming ?? false,
                        RewatchCount = userAnimeStatus.Attributes.ReconsumeCount ?? 0
                    };

                if (userAnimeStatus is { Attributes.Status: not null })
                    userList.Status = userAnimeStatus.Attributes.Status.Value.ToMalStatus();
            }

            return userList;
        }

        public async Task<UpdateAnimeStatusResponse> UpdateAnime(int animeId, int numberOfWatchedEpisodes, Status status, bool? isRewatching = null, int? numberOfTimesRewatched = null, DateTime? startDate = null, DateTime? endDate = null, string alternativeId = null, AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse ids = null, bool? isShow = null) {
            KitsuUpdate.Status kitsuStatus;

            switch (status) {
                case Status.Watching:
                    kitsuStatus = KitsuUpdate.Status.current;
                    break;
                case Status.Completed:
                case Status.Rewatching:
                    kitsuStatus = isRewatching == true ? KitsuUpdate.Status.current : KitsuUpdate.Status.completed;
                    break;
                case Status.On_hold:
                    kitsuStatus = KitsuUpdate.Status.on_hold;
                    break;
                case Status.Dropped:
                    kitsuStatus = KitsuUpdate.Status.dropped;
                    break;
                case Status.Plan_to_watch:
                    kitsuStatus = KitsuUpdate.Status.planned;
                    break;
                default:
                    kitsuStatus = KitsuUpdate.Status.current;
                    break;
            }

            if (await UpdateAnimeStatus(animeId, numberOfWatchedEpisodes, kitsuStatus, isRewatching, numberOfTimesRewatched, startDate, endDate)) {
                return new UpdateAnimeStatusResponse();
            }

            return null;
        }

        public async Task<MalApiCalls.User> GetUser() {
            int? user = await GetUserId();
            if (user != null)
                return new MalApiCalls.User {
                    Id = user.Value
                };

            return null;
        }

        public async Task<List<Anime>> GetAnimeList(Status status, int? userId = null) {
            KitsuUpdate.KitsuLibraryEntryListRoot animeList = await GetUserAnimeList(userId.Value, status: status.ToKitsuStatus());
            if (animeList != null) {
                List<Anime> convertedList = new List<Anime>();
                foreach (KitsuUpdate.KitsuLibraryEntry kitsuLibraryEntry in animeList.Data) {
                    Anime toAddAnime = new Anime();
                    if (kitsuLibraryEntry.Relationships != null &&
                        kitsuLibraryEntry.Relationships.AnimeData != null &&
                        kitsuLibraryEntry.Relationships.AnimeData.Anime != null) {
                        toAddAnime.Id = kitsuLibraryEntry.Relationships.AnimeData.Anime.Id;
                    }

                    toAddAnime.MyListStatus = new MyListStatus();
                    if (kitsuLibraryEntry.Attributes != null) {
                        toAddAnime.MyListStatus.FinishDate = kitsuLibraryEntry.Attributes.FinishedAt.ToString();
                        if (kitsuLibraryEntry.Attributes.Progress != null) {
                            toAddAnime.MyListStatus.NumEpisodesWatched = kitsuLibraryEntry.Attributes.Progress.Value;
                        }
                    }

                    convertedList.Add(toAddAnime);
                }

                return convertedList;
            }

            return null;
        }

        public async Task<int?> GetUserId() {
            var userIdKeyPair = _userConfig.KeyPairs.FirstOrDefault(item => item.Key == "KitsuUserId")?.Value;
            int? userId = null;
            if (userIdKeyPair != null) {
                return int.Parse(userIdKeyPair);
            } else {
                var userInformation = await GetUserInformation();
                if (userInformation != null) return userInformation.Id;
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
            var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Kitsu, AuthApiCall.CallType.GET, url.Build());
            if (apiCall != null) {
                var xd = await apiCall.Content.ReadAsStringAsync();
                StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
                var animeList = JsonSerializer.Deserialize<KitsuGetUser.KitsuUserRoot>(await streamReader.ReadToEndAsync());

                _logger.LogInformation("(Kitsu) Retrieved user information");
                return new MalApiCalls.User { Id = animeList.KitsuUserList[0].Id, Name = animeList.KitsuUserList[0].KitsuUser.Name };
            }

            return null;
        }

        public async Task<KitsuGet.KitsuGetAnime> GetAnime(int animeId) {
            UrlBuilder url = new UrlBuilder {
                Base = $"{ApiUrl}/anime/{animeId}",
            };

            string builtUrl = url.Build();
            _logger.LogInformation($"(Kitsu) Retrieving an anime from Kitsu (GET {builtUrl})...");
            try {
                var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Kitsu, AuthApiCall.CallType.GET, builtUrl);
                if (apiCall != null) {
                    StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
                    var anime = JsonSerializer.Deserialize<KitsuGet.KitsuGetAnime>(await streamReader.ReadToEndAsync());
                    _logger.LogInformation("(Kitsu) Anime retrieval complete");
                    List<KitsuSearch.KitsuAnime> relatedAnime = await GetRelatedAnime(animeId);
                    if (relatedAnime != null && anime != null) {
                        anime.KitsuAnimeData.RelatedAnime = relatedAnime;
                    }

                    return anime;
                }
            } catch (Exception e) {
                _logger.LogError(e.Message);
            }

            return null;
        }

        public async Task<List<KitsuSearch.KitsuAnime>> GetRelatedAnime(int animeId) {
            UrlBuilder url = new UrlBuilder {
                Base = $"{ApiUrl}/anime/{animeId}/media-relationships"
            };

            url.Parameters.Add(new KeyValuePair<string, string>("include", "destination"));

            string builtUrl = url.Build();
            try {
                var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Kitsu, AuthApiCall.CallType.GET, builtUrl);
                if (apiCall != null) {
                    StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
                    var mediaRelationships = JsonSerializer.Deserialize<KitsuMediaRelationship.MediaRelationship>(await streamReader.ReadToEndAsync());

                    List<int> listOfRelatedAnimeIds = new List<int>();
                    if (mediaRelationships != null) {
                        foreach (KitsuMediaRelationship.RelationshipData relationshipData in mediaRelationships.Data) {
                            if (relationshipData.Relationships.Destination.RelationshipData.Type == "anime") {
                                listOfRelatedAnimeIds.Add(int.Parse(relationshipData.Relationships.Destination.RelationshipData.Id));
                            }
                        }

                        for (var i = 0; i < mediaRelationships.Included.Count; i++) {
                            mediaRelationships.Included[i].RelationType = mediaRelationships.Data[i].Attributes.RelationType;
                        }

                        return mediaRelationships.Included.Where(anime => listOfRelatedAnimeIds.Any(ids => anime.Id == ids)).ToList();
                    }
                }
            } catch (Exception e) {
                _logger.LogError(e.Message);
            }

            return null;
        }

        public async Task<bool> UpdateAnimeStatus(int animeId, int numberOfWatchedEpisodes, KitsuUpdate.Status status,
            bool? isRewatching = null, int? numberOfTimesRewatched = null, DateTime? startDate = null, DateTime? endDate = null) {
            _logger.LogInformation($"(Kitsu) Preparing to update anime {animeId} status...");
            int? userId = await GetUserId();

            if (userId != null) {
                var libraryStatus = await GetUserAnimeStatus(userId.Value, animeId);
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
                                    Id = userId.Value
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
                HttpResponseMessage? apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Kitsu, libraryStatus != null ? AuthApiCall.CallType.PATCH : AuthApiCall.CallType.POST, url.Build(), stringContent: stringContent);

                if (apiCall != null) {
                    return apiCall.IsSuccessStatusCode;
                }
            }

            return false;
        }

        public async Task<KitsuUpdate.KitsuLibraryEntry> GetUserAnimeStatus(int userId, int animeId) {
            KitsuUpdate.KitsuLibraryEntryListRoot animeList = await LibraryEntriesCall(new List<KeyValuePair<string, string>> {
                new ("filter[animeId]", animeId.ToString()),
                new ("filter[userId]", userId.ToString())
            });

            if (animeList.Data is { Count: > 0 }) {
                return animeList.Data[0];
            }

            return null;
        }

        private async Task<KitsuUpdate.KitsuLibraryEntryListRoot> LibraryEntriesCall(List<KeyValuePair<string, string>> parameters) {
            UrlBuilder url = new UrlBuilder {
                Base = $"{ApiUrl}/library-entries",
                Parameters = parameters
            };

            _logger.LogInformation("(Kitsu) Fetching current user anime list status...");
            try {
                var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Kitsu, AuthApiCall.CallType.GET, url.Build());

                if (apiCall != null) {
                    StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
                    var library = JsonSerializer.Deserialize<KitsuUpdate.KitsuLibraryEntryListRoot>(await streamReader.ReadToEndAsync());
                    _logger.LogInformation("(Kitsu) Fetched user anime list");
                    return library;
                }
            } catch (Exception e) {
                _logger.LogError(e.Message);
            }

            return null;
        }

        public async Task<KitsuUpdate.KitsuLibraryEntryListRoot> GetUserAnimeList(int userId, KitsuUpdate.Status status) {
            KitsuUpdate.KitsuLibraryEntryListRoot animeList = await LibraryEntriesCall(new List<KeyValuePair<string, string>> {
                new("filter[userId]", userId.ToString()),
                new("filter[kind]", "anime"),
                new("filter[status]", status.ToString()),
                new("include", "anime"),
            });

            return animeList;
        }

        async Task<List<Anime>> IApiCallHelpers.SearchAnime(string query) {
            return ApiCallHelpers.KitsuSearchAnimeConvertedList(await SearchAnime(query));
        }
    }
}