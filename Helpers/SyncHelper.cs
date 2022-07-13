using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Helpers; 

public class SyncHelper {

    public static List<BaseItem> GetUsersJellyfinLibrary(Guid userId, IUserManager userManager, ILibraryManager libraryManager) {
        var query = new InternalItemsQuery(userManager.GetUserById(userId)) {
            IncludeItemTypes = new[] {
                BaseItemKind.Movie,
                BaseItemKind.Series
            },
            IsVirtualItem = false
        };

        return libraryManager.GetItemList(query);
    }

    /// <summary>
    /// Get the preferred series provider ID. 
    /// </summary>
    public static (AnimeOfflineDatabaseHelpers.Source? source, int? providerId) GetSeriesProviderId(Series series) {
        if (int.TryParse(series.ProviderIds["AniList"], out int providerId)) {
            return (AnimeOfflineDatabaseHelpers.Source.Anilist, providerId);
        } else if (int.TryParse(series.ProviderIds["AniDB"], out providerId)) {
            return (AnimeOfflineDatabaseHelpers.Source.Anidb, providerId);
        }

        return (null, null);
    }
    
    /// <summary>
    /// Get a list of seasons corresponding with their provider ID.
    /// </summary>
    /// <param name="convertedWatchList">List of metadata to match to seasons.</param>
    /// <param name="providerId">AniList provider ID.</param>
    /// <param name="series">Series to retrieve season of.</param>
    /// <param name="source">Metadata source.</param>
    /// <returns>List of seasons with their completed date and progress.</returns>
    public static async Task<List<(Season, DateTime?, int)>> GetJellyfinSeasons(List<Sync.SyncAnimeMetadata> convertedWatchList,
        int providerId,
        Series series,
        AnimeOfflineDatabaseHelpers.Source source,
        ILogger logger,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IApplicationPaths applicationPaths) {
        List<(Season, DateTime?, int)> seasons = new List<(Season, DateTime?, int)>();

        List<AnimeListAnimeOfflineDatabaseCombo> seasonIdsFromJellyfin = await GetSeasonIdsFromJellyfin(providerId,
            source,
            logger,
            loggerFactory,
            httpClientFactory,
            applicationPaths);
        
        foreach (AnimeListAnimeOfflineDatabaseCombo animeListAnime in seasonIdsFromJellyfin) {
            var found = convertedWatchList.FirstOrDefault(item => int.TryParse(animeListAnime.AnimeListAnime.Anidbid, out int convertedAniDbId) && item.ids.AniDb == convertedAniDbId);
            if (found != null) {
                seasons.Add((series.Children.Select(season => season as Season).FirstOrDefault(season => season.IndexNumber == found.season), found.completedAt, found.episodesWatched));
            }
        }

        return seasons;
    }


    /// <summary>
    /// Get season IDs from Jellyfin library.
    /// </summary>
    /// <param name="providerId">ID of the provider.</param>
    /// <param name="seasonFilter">Optional list of season numbers to filter by.</param>
    /// <returns>List of season IDs.</returns>
    public static async Task<List<AnimeListAnimeOfflineDatabaseCombo>> GetSeasonIdsFromJellyfin(int providerId, AnimeOfflineDatabaseHelpers.Source source,
        ILogger logger,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IApplicationPaths applicationPaths,
        List<int> seasonFilter = null) {
        var id = await AnimeOfflineDatabaseHelpers.GetProviderIdsFromMetadataProvider(httpClientFactory.CreateClient(NamedClient.Default), providerId, source);
        if (id is { AniDb: { } }) {
            var seasons = await AnimeListHelpers.ListAllSeasonOfAniDbSeries(logger, loggerFactory, httpClientFactory, applicationPaths, id.AniDb.Value);
            List<AnimeListHelpers.AnimeListAnime> seasonsList;
            if (seasonFilter != null) {
                seasonsList = seasons.Where(season => int.TryParse(season.Defaulttvdbseason, out int parsedSeasonNumber) && seasonFilter.Contains(parsedSeasonNumber)).ToList();
            } else {
                seasonsList = seasons.ToList();
            }
            
            if (seasonsList.Count() > 1) {
                return seasonsList.Select(animeListAnime => new AnimeListAnimeOfflineDatabaseCombo { AnimeListAnime = animeListAnime }).ToList();
            } else {
                var seasonMetadataCombo = new List<AnimeListAnimeOfflineDatabaseCombo> {
                    new() {
                        AnimeListAnime = seasonsList.First(),
                        OfflineDatabaseResponse = id
                    }
                };
                return seasonMetadataCombo;
            }
        }

        return null;
    }

    public class AnimeListAnimeOfflineDatabaseCombo {
        public AnimeListHelpers.AnimeListAnime AnimeListAnime { get; set; }
        public AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse OfflineDatabaseResponse { get; set; }
    }
}