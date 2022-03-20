using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
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

    public async Task<string> GetUserInformation() {
        UrlBuilder url = new UrlBuilder {
            Base = $"{ApiUrl}/users",
            Parameters = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("filter[self]", "true")
            }
        };

        _logger.LogInformation($"(Kitsu) Retrieving user information...");
        var apiCall = await _authApiCall.AuthenticatedApiCall(ApiName.Kitsu, MalApiCalls.CallType.GET, url.Build());
        if (apiCall != null) {
            StreamReader streamReader = new StreamReader(await apiCall.Content.ReadAsStreamAsync());
            var animeList = JsonSerializer.Deserialize<KitsuUser.KitsuUserRoot>(await streamReader.ReadToEndAsync());

            _logger.LogInformation("(Kitsu) Retrieved user information");
            return animeList.KitsuUserList[0].Attributes.Name;
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
}