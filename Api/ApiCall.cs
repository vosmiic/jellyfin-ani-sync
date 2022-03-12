using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api.Anilist;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Models;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Api;

public class ApiCall {
    private ApiName _provider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServerApplicationHost _serverApplicationHost;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ApiCall> _logger;
    public UserConfig UserConfig { get; set; }
    private MalApiCalls _malApiCalls;
    private AniListApiCalls _aniListApiCalls;

    public ApiCall(ApiName provider, IHttpClientFactory httpClientFactory, IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor, ILoggerFactory loggerFactory, UserConfig userConfig, MalApiCalls malApiCalls = null, AniListApiCalls aniListApiCalls = null) {
        _provider = provider;
        _httpClientFactory = httpClientFactory;
        _serverApplicationHost = serverApplicationHost;
        _httpContextAccessor = httpContextAccessor;
        _logger = loggerFactory.CreateLogger<ApiCall>();
        UserConfig = userConfig;
        _malApiCalls = malApiCalls;
        _aniListApiCalls = aniListApiCalls;
    }

    /// <summary>
    /// Make an authenticated API call.
    /// </summary>
    /// <param name="callType">The type of call to make.</param>
    /// <param name="url">The URL that you want to make the request to.</param>
    /// <param name="formUrlEncodedContent">The form data to be posted.</param>
    /// <returns>API call response.</returns>
    /// <exception cref="NullReferenceException">Authentication details not found.</exception>
    /// <exception cref="Exception">Non-200 response.</exception>
    /// <exception cref="AuthenticationException">Could not authenticate with the API.</exception>
    public async Task<HttpResponseMessage> AuthenticatedApiCall(ApiName provider, MalApiCalls.CallType callType, string url, FormUrlEncodedContent formUrlEncodedContent = null, StringContent stringContent = null) {
        int attempts = 0;
        UserApiAuth auth;
        try {
            auth = UserConfig.UserApiAuth.FirstOrDefault(item => item.Name == provider);
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
                    case MalApiCalls.CallType.GET:
                        responseMessage = await client.GetAsync(url);
                        break;
                    case MalApiCalls.CallType.POST:
                        responseMessage = await client.PostAsync(url, formUrlEncodedContent != null ? formUrlEncodedContent : stringContent);
                        break;
                    case MalApiCalls.CallType.PATCH:
                        responseMessage = await client.PatchAsync(url, formUrlEncodedContent);
                        break;
                    case MalApiCalls.CallType.PUT:
                        responseMessage = await client.PutAsync(url, formUrlEncodedContent);
                        break;
                    case MalApiCalls.CallType.DELETE:
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
                        newAuth = new MalApiAuthentication(provider, _httpClientFactory, _serverApplicationHost, _httpContextAccessor).GetToken(UserConfig.UserId, refreshToken: auth.RefreshToken);
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

        _logger.LogError("Unable to authenticate the API call, re-authenticate the plugin");
        return null;
    }

    public async Task<List<Anime>> SearchAnime(string query) {
        if (_malApiCalls != null) {
            return await _malApiCalls.SearchAnime(query, new[] { "id", "title", "alternative_titles" });
        }

        if (_aniListApiCalls != null) {
            List<AniListSearch.Media> animeList = await _aniListApiCalls.SearchAnime(query);
            List<Anime> convertedList = new List<Anime>();
            foreach (AniListSearch.Media media in animeList) {
                convertedList.Add(new Anime {
                    Id = media.Id,
                    Title = media.Title.English,
                    AlternativeTitles = new AlternativeTitles {
                        En = media.Title.English,
                        Ja = media.Title.Native,
                        Synonyms = new List<string> {
                            { media.Title.Romaji },
                            { media.Title.UserPreferred }
                        }
                    }
                });
            }

            return convertedList;
        }

        return null;
    }

    public async Task<Anime> GetAnime(int id) {
        if (_malApiCalls != null) {
            return await _malApiCalls.GetAnime(id, new[] { "title", "related_anime", "my_list_status" });
        }

        if (_aniListApiCalls != null) {
            AniListSearch.Media anime = await _aniListApiCalls.GetAnime(id);
            Anime convertedAnime = ClassConversions.ConvertAnime(anime);

            if (anime.MediaListEntry != null) {
                switch (anime.MediaListEntry.MediaListStatus) {
                    case AniListSearch.MediaListStatus.Current:
                        convertedAnime.MyListStatus.Status = Status.Plan_to_watch;
                        break;
                    case AniListSearch.MediaListStatus.Completed:
                    case AniListSearch.MediaListStatus.Repeating:
                        convertedAnime.MyListStatus.Status = Status.Completed;
                        break;
                    case AniListSearch.MediaListStatus.Dropped:
                        convertedAnime.MyListStatus.Status = Status.Dropped;
                        break;
                    case AniListSearch.MediaListStatus.Paused:
                        convertedAnime.MyListStatus.Status = Status.On_hold;
                        break;
                    case AniListSearch.MediaListStatus.Planning:
                        convertedAnime.MyListStatus.Status = Status.Plan_to_watch;
                        break;
                }
            }

            convertedAnime.RelatedAnime = new List<RelatedAnime>();
            foreach (AniListSearch.MediaEdge relation in anime.Relations.Media) {
                RelatedAnime relatedAnime = new RelatedAnime {
                    Anime = ClassConversions.ConvertAnime(relation.Media)
                };

                switch (relation.RelationType) {
                    case AniListSearch.MediaRelation.Sequel:
                        relatedAnime.RelationType = RelationType.Sequel;
                        break;
                    case AniListSearch.MediaRelation.Side_Story:
                        relatedAnime.RelationType = RelationType.Side_Story;
                        break;
                    case AniListSearch.MediaRelation.Alternative:
                        relatedAnime.RelationType = RelationType.Alternative_Setting;
                        break;
                }

                convertedAnime.RelatedAnime.Add(relatedAnime);
            }

            return convertedAnime;
        }

        return null;
    }

    public async Task<UpdateAnimeStatusResponse> UpdateAnime(int animeId, int numberOfWatchedEpisodes, Status status,
        bool? isRewatching = null, int? numberOfTimesRewatched = null, DateTime? startDate = null, DateTime? endDate = null) {
        if (_malApiCalls != null) {
            return await _malApiCalls.UpdateAnimeStatus(animeId, numberOfWatchedEpisodes, status, isRewatching, numberOfTimesRewatched, startDate, endDate);
        }

        if (_aniListApiCalls != null) {
            AniListSearch.MediaListStatus anilistStatus;
            switch (status) {
                case Status.Watching:
                    anilistStatus = AniListSearch.MediaListStatus.Current;
                    break;
                case Status.Completed:
                    anilistStatus = isRewatching != null && isRewatching.Value ? AniListSearch.MediaListStatus.Repeating : AniListSearch.MediaListStatus.Completed;
                    break;
                case Status.On_hold:
                    anilistStatus = AniListSearch.MediaListStatus.Paused;
                    break;
                case Status.Dropped:
                    anilistStatus = AniListSearch.MediaListStatus.Dropped;
                    break;
                case Status.Plan_to_watch:
                    anilistStatus = AniListSearch.MediaListStatus.Planning;
                    break;
                default:
                    anilistStatus = AniListSearch.MediaListStatus.Current;
                    break;
            }

            if (await _aniListApiCalls.UpdateAnime(animeId, anilistStatus, numberOfWatchedEpisodes, numberOfTimesRewatched, startDate, endDate)) {
                return new UpdateAnimeStatusResponse();
            }
        }

        return null;
    }
}