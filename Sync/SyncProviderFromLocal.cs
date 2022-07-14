using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Helpers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync;

public class SyncProviderFromLocal {
    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApplicationPaths _applicationPaths;
    private readonly IUserDataManager _userDataManager;
    private readonly Guid _userId;
    private readonly ILogger<SyncProviderFromLocal> _logger;

    public SyncProviderFromLocal(IUserManager userManager,
        ILibraryManager libraryManager,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IApplicationPaths applicationPaths,
        IUserDataManager userDataManager,
        string userId) {
        _userManager = userManager;
        _libraryManager = libraryManager;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _applicationPaths = applicationPaths;
        _userDataManager = userDataManager;
        _userId = Guid.Parse(userId);
        _logger = loggerFactory.CreateLogger<SyncProviderFromLocal>();
    }

    public async Task<List<SeasonDetails>> SyncFromLocal(string userId, SyncHelper.Status status) {
        var jellyfinLibrary = SyncHelper.GetUsersJellyfinLibrary(_userId, _userManager, _libraryManager);
        List<Series> userSeriesList = jellyfinLibrary.OfType<Series>().Select(baseItem => baseItem).ToList();
        List<SeasonDetails> seasonDetailsList = await GetSeasonDetails(userSeriesList);


        return seasonDetailsList;
    }

    private async Task<SyncHelper.AnimeListAnimeOfflineDatabaseCombo> GetSeriesSeasonsMetadata(SyncHelper.AnimeListAnimeOfflineDatabaseCombo season) {
        if (season.OfflineDatabaseResponse != null) {
            _logger.LogInformation("(Sync) Season already has provider metadata");
            return season;
        }

        if (season.AnimeListAnime != null && !string.IsNullOrEmpty(season.AnimeListAnime.Anidbid) && int.TryParse(season.AnimeListAnime.Anidbid, out int aniDbId)) {
            var seasonMetadata = await AnimeOfflineDatabaseHelpers.GetProviderIdsFromMetadataProvider(_httpClientFactory.CreateClient(NamedClient.Default), aniDbId, AnimeOfflineDatabaseHelpers.Source.Anidb);
            if (seasonMetadata != null) {
                season.OfflineDatabaseResponse = seasonMetadata;
                _logger.LogInformation("(Sync) Retrieved season provider metadata");
                return season;
            }
        } else {
            _logger.LogError("(Sync) Could not parse AniDB ID from season metadata, skipping...");
        }

        return null;
    }

    private async Task<List<SeasonDetails>> GetSeasonDetails(List<Series> userSeriesList) {
        List<SeasonDetails> listOfSeasonsWithMetadata = new List<SeasonDetails>();

        foreach (Series series in userSeriesList) {
            Dictionary<int, (int episodesWatched, DateTime watchedDate)> userProgressSeasonList = SyncHelper.FilterSeriesByUserProgress(_userId, series, _userDataManager);
            if (userProgressSeasonList.Count == 0 || userProgressSeasonList.All(item => item.Value.episodesWatched == 0)) {
                _logger.LogInformation($"(Sync) User has not watched any episodes of {series.Name}. Skipping...");
                continue;
            }

            (AnimeOfflineDatabaseHelpers.Source? source, int? providerId) = SyncHelper.GetSeriesProviderId(series);

            if (source == null || providerId == null || providerId == 0) continue;
            List<SyncHelper.AnimeListAnimeOfflineDatabaseCombo> seriesSeasonsWithMetadata = await SyncHelper.GetSeasonIdsFromJellyfin(providerId.Value,
                source.Value,
                _logger,
                _loggerFactory,
                _httpClientFactory,
                _applicationPaths,
                userProgressSeasonList.Select(item => item.Key).ToList());
            _logger.LogInformation("(Sync) Retrieved season metadata");

            List<SeasonDetails> seriesSeasonsWithProviderMetadata = new List<SeasonDetails>();
            for (var i = 0; i < seriesSeasonsWithMetadata.Count; i++) {
                _logger.LogInformation($"(Sync) Attempting to retrieve {seriesSeasonsWithMetadata[i].AnimeListAnime.Name} provider metadata...");
                SyncHelper.AnimeListAnimeOfflineDatabaseCombo seasonWithMetadata = await GetSeriesSeasonsMetadata(seriesSeasonsWithMetadata[i]);
                var matchedUserSeasonDetails = userProgressSeasonList.FirstOrDefault(season => season.Key == int.Parse(seriesSeasonsWithMetadata[i].AnimeListAnime.Defaulttvdbseason));
                if (seasonWithMetadata != null) {
                    seriesSeasonsWithProviderMetadata.Add(new SeasonDetails {
                        AnimeListAnime = seasonWithMetadata.AnimeListAnime,
                        OfflineDatabaseResponse = seasonWithMetadata.OfflineDatabaseResponse,
                        Progress = matchedUserSeasonDetails.Value.episodesWatched,
                        Completed = matchedUserSeasonDetails.Value.watchedDate
                    });
                }

                if (seriesSeasonsWithMetadata[i++] != null) {
                    _logger.LogInformation(("(Sync) Waiting 2 seconds before continuing..."));
                    Thread.Sleep(2000);
                }
            }

            listOfSeasonsWithMetadata = listOfSeasonsWithMetadata.Concat(seriesSeasonsWithProviderMetadata).ToList();
        }

        return listOfSeasonsWithMetadata;
    }

    public class SeasonDetails : SyncHelper.AnimeListAnimeOfflineDatabaseCombo {
        public int Progress { get; set; }
        public DateTime Completed { get; set; }
    }
}