using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Interfaces;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync;

public class SyncProviderFromLocal (
    IUserManager userManager,
    ILibraryManager libraryManager,
    ILoggerFactory loggerFactory,
    IHttpClientFactory httpClientFactory,
    IApplicationPaths applicationPaths,
    IFileSystem fileSystem,
    IMemoryCache memoryCache,
    IAsyncDelayer delayer,
    string userId) {
    private readonly Guid _userId = Guid.Parse(userId);
    private readonly ILogger<SyncProviderFromLocal> _logger = loggerFactory.CreateLogger<SyncProviderFromLocal>();
    private IServerApplicationHost _serverApplicationHost;
    private IHttpContextAccessor _httpContextAccessor;

    public async Task SyncFromLocal() {
        var jellyfinLibrary = SyncHelper.GetUsersJellyfinLibrary(_userId, userManager, libraryManager);
        List<Series> userSeriesList = jellyfinLibrary.OfType<Series>().Select(baseItem => baseItem).ToList();
        await GetSeasonDetails(userSeriesList);
    }

    private async Task GetSeasonDetails(List<Series> userSeriesList) {
        _logger.LogInformation($"(Sync) Starting sync to provider from local process");
        UpdateProviderStatus updateProviderStatus = new UpdateProviderStatus(fileSystem, libraryManager, loggerFactory, _httpContextAccessor, _serverApplicationHost, httpClientFactory, applicationPaths, memoryCache, delayer);

        foreach (Series series in userSeriesList) {
            _logger.LogInformation($"(Sync) Retrieved {series.Name}'s seasons latest watched episode and when it was watched...");
            var toMarkAsCompleted = GetMaxEpisodeAndCompletedTime(series);
            if (toMarkAsCompleted != null) {
                foreach (Episode episodeDateTime in toMarkAsCompleted) {
                    if (episodeDateTime != null) {
                        try {
                            await updateProviderStatus.Update(episodeDateTime, _userId, true);
                        } catch (Exception e) {
                            _logger.LogError($"(Sync) Could not sync item; error: {e.Message}");
                            continue;
                        }

                        _logger.LogInformation("(Sync) Waiting 2 seconds before continuing...");
                        Thread.Sleep(2000);
                    } else {
                        _logger.LogError("(Sync) Could not get users Jellyfin data for this season");
                    }
                }
            } else {
                _logger.LogError($"(Sync) User with ID of {_userId} not found");
            }
        }
    }

    private List<Episode> GetMaxEpisodeAndCompletedTime(Series series) {
        List<Episode> returnDictionary = new List<Episode>();

        _logger.LogInformation($"(Sync) Getting {series.Name} seasons latest watched episode");
        var seasons = series.Children.OfType<Season>().Select(baseItem => baseItem).ToList();
        _logger.LogInformation($"(Sync) Series {series.Name} contains {seasons.Count} seasons");
        User user = userManager.GetUserById(_userId);
        if (user == null) return null;
        foreach (Season season in seasons) {
            _logger.LogInformation($"(Sync) Getting user data for {season.Name} of {series.Name}...");
            if (season.IndexNumber == null) {
                _logger.LogError($"(Sync) Season index number is null. Skipping...");
                continue;
            }
            
            IEnumerable<Episode> episodes = season.GetEpisodes(user, new DtoOptions(), false).OfType<Episode>().ToArray();
            if (!episodes.Any()) {
                _logger.LogInformation($"(Sync) No (user visible) episodes found for {season.Name} of {series.Name}");
            }
            _logger.LogInformation($"(Sync) Season contains {season.Children.OfType<Episode>().Count()} episodes");
            Episode latestWatchedEpisode;

            try {
                latestWatchedEpisode = episodes.Where(episode => episode.UserData.Any(userData => userData.UserId == user.Id && userData.Played)).MaxBy(episode => episode.IndexNumber);
            } catch (Exception e) {
                _logger.LogError($"(Sync) Could not get user episodes watched for {season.Name}; error: {e.Message}");
                continue;
            }

            if (latestWatchedEpisode == null) {
                _logger.LogInformation($"(Sync) No episodes watched for {season.Name}");
                continue;
            }
            _logger.LogInformation($"(Sync) The latest watched episode for this user of this season is {latestWatchedEpisode.IndexNumber}");
            returnDictionary.Add(latestWatchedEpisode);
        }

        _logger.LogInformation($"(Sync) Found {returnDictionary.Count} seasons that contain user data");
        return returnDictionary;
    }
}