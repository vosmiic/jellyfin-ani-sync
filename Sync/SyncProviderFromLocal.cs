using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Helpers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
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
    private readonly ILogger<SyncProviderFromLocal> _logger;

    public SyncProviderFromLocal(IUserManager userManager,
        ILibraryManager libraryManager,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IApplicationPaths applicationPaths) {
        _userManager = userManager;
        _libraryManager = libraryManager;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _applicationPaths = applicationPaths;
        _logger = loggerFactory.CreateLogger<SyncProviderFromLocal>();
    }

    public async Task<List<SyncHelper.AnimeListAnimeOfflineDatabaseCombo>> SyncFromLocal(string userId) {
        var jellyfinLibrary = SyncHelper.GetUsersJellyfinLibrary(Guid.Parse(userId), _userManager, _libraryManager);

        List<SyncHelper.AnimeListAnimeOfflineDatabaseCombo> listOfSeasonsWithMetadata = new List<SyncHelper.AnimeListAnimeOfflineDatabaseCombo>();
        foreach (BaseItem baseItem in jellyfinLibrary) {
            if (baseItem is Series series) {
                (AnimeOfflineDatabaseHelpers.Source? source, int? providerId) = SyncHelper.GetSeriesProviderId(series);

                if (source == null || providerId == null || providerId == 0) continue;
                var seriesSeasons = await SyncHelper.GetSeasonIdsFromJellyfin(providerId.Value,
                    source.Value,
                    _logger,
                    _loggerFactory,
                    _httpClientFactory,
                    _applicationPaths);

                List<SyncHelper.AnimeListAnimeOfflineDatabaseCombo> seriesSeasonsWithMetadata = new List<SyncHelper.AnimeListAnimeOfflineDatabaseCombo>();
                for (var i = 0; i < seriesSeasons.Count; i++) {
                    var seasonWithMetadata = await GetSeriesSeasonsMetadata(seriesSeasons[i]);
                    if (seasonWithMetadata != null) {
                        seriesSeasonsWithMetadata.Add(seasonWithMetadata);
                    }

                    if (seriesSeasons[i++] != null) {
                        Thread.Sleep(2000);
                    }
                }

                listOfSeasonsWithMetadata = listOfSeasonsWithMetadata.Concat(seriesSeasonsWithMetadata).ToList();
            }
        }
        
        

        return listOfSeasonsWithMetadata;
    }
    
    private async Task<SyncHelper.AnimeListAnimeOfflineDatabaseCombo> GetSeriesSeasonsMetadata(SyncHelper.AnimeListAnimeOfflineDatabaseCombo season) {
        if (season.OfflineDatabaseResponse != null) {
            return season;
        } else if (season.AnimeListAnime != null && !string.IsNullOrEmpty(season.AnimeListAnime.Anidbid) && int.TryParse(season.AnimeListAnime.Anidbid, out int aniDbId)) {
            var seasonMetadata = await AnimeOfflineDatabaseHelpers.GetProviderIdsFromMetadataProvider(_httpClientFactory.CreateClient(NamedClient.Default), aniDbId, AnimeOfflineDatabaseHelpers.Source.Anidb);
            if (seasonMetadata != null) {
                season.OfflineDatabaseResponse = seasonMetadata;
                return season;
            }
        }

        return null;
    }
}