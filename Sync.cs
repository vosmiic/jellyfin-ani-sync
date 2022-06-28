using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api;
using jellyfin_ani_sync.Api.Anilist;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Models.Mal;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync;

public class Sync {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Sync> _logger;
    private readonly IServerApplicationHost _serverApplicationHost;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IApplicationPaths _applicationPaths;
    private readonly IUserDataManager _userDataManager;
    private readonly ApiName _apiName;
    private int _apiTimeOutLength = 2000;

    public Sync(IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IServerApplicationHost serverApplicationHost,
        IHttpContextAccessor httpContextAccessor,
        IUserManager userManager,
        ILibraryManager libraryManager,
        IApplicationPaths applicationPaths,
        IUserDataManager userDataManager,
        ApiName apiName) {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _serverApplicationHost = serverApplicationHost;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _libraryManager = libraryManager;
        _applicationPaths = applicationPaths;
        _userDataManager = userDataManager;
        _apiName = apiName;
        _logger = loggerFactory.CreateLogger<Sync>();
    }

    public async Task SyncFromProvider(string userId) {
        var completedList = await GetAnimeList(Status.Completed, userId);
        if (completedList == null) {
            _logger.LogWarning("(Sync) No anime found by provider; please make sure user is authenticated with this provider and the authenticated users watch list is populated");
            return;
        }

        var metadataIds = await GetMetadataIdsFromAnime(completedList);
        await GetCurrentLibrary(userId, metadataIds);
    }

    private async Task<List<Anime>> GetAnimeList(Status status, string userId) {
        ApiCallHelpers apiCallHelpers;
        switch (_apiName) {
            case ApiName.Mal:
                apiCallHelpers = new ApiCallHelpers(malApiCalls: new MalApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, Plugin.Instance.PluginConfiguration.UserConfig.FirstOrDefault(item => item.UserId == Guid.Parse(userId))));

                return await apiCallHelpers.GetAnimeList(status);
            case ApiName.AniList:
                apiCallHelpers = new ApiCallHelpers(aniListApiCalls: new AniListApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost, _httpContextAccessor, Plugin.Instance.PluginConfiguration.UserConfig.FirstOrDefault(item => item.UserId == Guid.Parse(userId))));
                var user = await apiCallHelpers.GetUser();

                return await apiCallHelpers.GetAnimeList(status, user.Id);
        }

        return null;
    }

    private async Task GetCurrentLibrary(string userId, List<SyncAnimeMetadata> convertedWatchList) {
        var query = new InternalItemsQuery(_userManager.GetUserById(Guid.Parse(userId))) {
            IncludeItemTypes = new[] {
                BaseItemKind.Movie,
                BaseItemKind.Series
            },
            IsVirtualItem = false
        };

        var results = _libraryManager.GetItemList(query);
        foreach (BaseItem baseItem in results) {
            if (baseItem is Series series) {
                if (int.TryParse(series.ProviderIds["AniList"], out int aniListProviderId)) {
                    List<(Season, DateTime?)> seasonsToMarkAsPlayed = await GetSeasons(convertedWatchList, aniListProviderId, series);

                    foreach ((Season season, DateTime? completedAt) seasonsTuple in seasonsToMarkAsPlayed) {
                        if (seasonsTuple.season != null) {
                            var user = _userManager.GetUserById(Guid.Parse(userId));
                            List<UserItemData> userItemDataCollection = new List<UserItemData>();

                            foreach (var seasonChild in seasonsTuple.season.Children) {
                                if (seasonChild is Episode episode) {
                                    Console.WriteLine($"Setting {episode.Series.Name} season {episode.Season.IndexNumber} episode {episode.IndexNumber} for user {userId} as played...");

                                    userItemDataCollection.Add(SetUserData(user, episode, seasonsTuple.completedAt));
                                }
                            }

                            // this could fail because show has ovas, hard to detect. todo warn users
                            userItemDataCollection.Add(SetUserData(user, seasonsTuple.season, seasonsTuple.completedAt));


                            _userDataManager.SaveAllUserData(user.Id, userItemDataCollection.ToArray(), CancellationToken.None);
                            Console.WriteLine("Saved");
                        }
                    }
                }
            }
        }
    }

    private async Task<List<(Season, DateTime?)>> GetSeasons(List<SyncAnimeMetadata> convertedWatchList, int aniListProviderId, Series series) {
        List<(Season, DateTime?)> seasons = new List<(Season, DateTime?)>();

        IEnumerable<AnimeListHelpers.AnimeListAnime> seasonIdsFromJellyfin = await GetSeasonIdsFromJellyfin(aniListProviderId);
        foreach (AnimeListHelpers.AnimeListAnime animeListAnime in seasonIdsFromJellyfin) {
            var found = convertedWatchList.FirstOrDefault(item => int.TryParse(animeListAnime.Anidbid, out int convertedAniDbId) && item.ids.AniDb == convertedAniDbId);
            if (found != null) {
                seasons.Add((series.Children.Select(season => season as Season).FirstOrDefault(season => season.IndexNumber == found.season), found.completedAt));
            }
        }

        return seasons;
    }

    private UserItemData SetUserData(User user, BaseItem itemToBeUpdated, DateTime? completedDate) {
        var userItemData = _userDataManager.GetUserData(user, itemToBeUpdated);
        userItemData.Played = true;
        if (completedDate != null) {
            userItemData.LastPlayedDate = completedDate.Value;
        }

        return userItemData;
    }

    private async Task<List<SyncAnimeMetadata>> GetMetadataIdsFromAnime(List<Anime> animeList) {
        List<SyncAnimeMetadata> animeIdProgress = new List<SyncAnimeMetadata>();
        for (var i = 0; i < animeList.Count; i++) {
            _logger.LogInformation($"(Sync) Fetching IDs for anime with an ID of {animeList[i].Id}...");
            var ids = await AnimeOfflineDatabaseHelpers.GetProviderIdsFromMetadataProvider(_httpClientFactory.CreateClient(NamedClient.Default), animeList[i].Id, AnimeOfflineDatabaseHelpers.MapFromApiName(_apiName));
            if (ids.AniDb == null) {
                _logger.LogError("(Sync) Could not retrieve AniDb ID; skipping item...");
                continue;
            }

            int? season = await AnimeListHelpers.GetAniDbSeasonNumber(_logger, _loggerFactory, _httpClientFactory, _applicationPaths, ids.AniDb.Value);
            if (season == null) {
                _logger.LogError("(Sync) Could not retrieve season number; skipping item...");
                continue;
            }

            var syncAnimeMetadata = new SyncAnimeMetadata {
                ids = ids,
                episodesWatched = animeList[i].MyListStatus != null && animeList[i].MyListStatus.NumEpisodesWatched != 0 ? animeList[i].MyListStatus.NumEpisodesWatched : -1,
                season = season.Value
            };
            if (animeList[i].MyListStatus is { FinishDate: { } }) {
                syncAnimeMetadata.completedAt = DateTime.Parse(animeList[i].MyListStatus.FinishDate);
            }

            animeIdProgress.Add(syncAnimeMetadata);
            _logger.LogInformation("(Sync) Fetched");
            if (i != animeList.Count - 1) {
                _logger.LogInformation("(Sync) Waiting 2 seconds before proceeding...");
                Thread.Sleep(_apiTimeOutLength);
            }
        }

        return animeIdProgress;

        return null;
    }


    private async Task<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse> GetAniDbSeasonIdsFromJellyfin(ApiName provider, int providerId, int seasonNumber, AnimeOfflineDatabaseHelpers.Source? conversion = null) {
        var id = await AnimeOfflineDatabaseHelpers.GetProviderIdsFromMetadataProvider(_httpClientFactory.CreateClient(NamedClient.Default), providerId, AnimeOfflineDatabaseHelpers.Source.Anilist);
        if (id is { AniDb: { } }) {
            var aniDbSeason = await AnimeListHelpers.GetAniDbSeason(_logger, _loggerFactory, _httpClientFactory, _applicationPaths, id.AniDb.Value, seasonNumber);
            var convertedId = await AnimeOfflineDatabaseHelpers.GetProviderIdsFromMetadataProvider(_httpClientFactory.CreateClient(NamedClient.Default), aniDbSeason.Value, AnimeOfflineDatabaseHelpers.Source.Anidb);
            if (convertedId != null) {
                return convertedId;
            }
        }

        return null;
    }

    private async Task<IEnumerable<AnimeListHelpers.AnimeListAnime>> GetSeasonIdsFromJellyfin(int providerId) {
        var id = await AnimeOfflineDatabaseHelpers.GetProviderIdsFromMetadataProvider(_httpClientFactory.CreateClient(NamedClient.Default), providerId, AnimeOfflineDatabaseHelpers.Source.Anilist);
        if (id is { AniDb: { } }) {
            return await AnimeListHelpers.ListAllSeasonOfAniDbSeries(_logger, _loggerFactory, _httpClientFactory, _applicationPaths, id.AniDb.Value);
        }

        return null;
    }

    private class SyncAnimeMetadata {
        public AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse ids { get; set; }
        public int episodesWatched { get; set; }
        public int season { get; set; }
        public DateTime? completedAt { get; set; }
    }
}