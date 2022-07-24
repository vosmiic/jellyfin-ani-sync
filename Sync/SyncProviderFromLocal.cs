using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Helpers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync;

public class SyncProviderFromLocal {
    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApplicationPaths _applicationPaths;
    private readonly IUserDataManager _userDataManager;
    private readonly IFileSystem _fileSystem;
    private readonly Guid _userId;
    private readonly ILogger<SyncProviderFromLocal> _logger;
    private IServerApplicationHost _serverApplicationHost;
    private IHttpContextAccessor _httpContextAccessor;

    public SyncProviderFromLocal(IUserManager userManager,
        ILibraryManager libraryManager,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IApplicationPaths applicationPaths,
        IUserDataManager userDataManager,
        IFileSystem fileSystem,
        string userId) {
        _userManager = userManager;
        _libraryManager = libraryManager;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _applicationPaths = applicationPaths;
        _userDataManager = userDataManager;
        _fileSystem = fileSystem;
        _userId = Guid.Parse(userId);
        _logger = loggerFactory.CreateLogger<SyncProviderFromLocal>();
    }

    public async Task SyncFromLocal() {
        var jellyfinLibrary = SyncHelper.GetUsersJellyfinLibrary(_userId, _userManager, _libraryManager);
        List<Series> userSeriesList = jellyfinLibrary.OfType<Series>().Select(baseItem => baseItem).ToList();
        await GetSeasonDetails(userSeriesList);
    }

    private async Task GetSeasonDetails(List<Series> userSeriesList) {
        _logger.LogInformation($"(Sync) Starting sync to provider from local process");
        UpdateProviderStatus updateProviderStatus = new UpdateProviderStatus(_fileSystem, _libraryManager, _loggerFactory, _httpContextAccessor, _serverApplicationHost, _httpClientFactory, _applicationPaths);

        foreach (Series series in userSeriesList) {
            var toMarkAsCompleted = GetMaxEpisodeAndCompletedTime(series);
            _logger.LogInformation($"(Sync) Retrieved {series.Name}'s seasons latest watched episode and when it was watched...");
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
            }
        }
    }

    private List<Episode> GetMaxEpisodeAndCompletedTime(Series series) {
        List<Episode> returnDictionary = new List<Episode>();
        
        var seasons = series.Children.OfType<Season>().Select(baseItem => baseItem).ToList();
        foreach (Season season in seasons) {
            List<Episode> episodes = season.Children.OfType<Episode>().Select(baseItem => baseItem).ToList();
            var episodesWatched = episodes.Where(item => _userDataManager.GetUserData(_userId, item).Played).ToList();
            Episode highestEpisodeWatched;
            if (episodesWatched.Any()) {
                highestEpisodeWatched = episodesWatched.OrderByDescending(item => item.IndexNumber).First();
            } else {
                continue;
            }
            returnDictionary.Add(highestEpisodeWatched/*, _userDataManager.GetUserData(_userId, highestEpisodeWatched).LastPlayedDate ?? DateTime.UtcNow*/);
        }
        
        return returnDictionary;
    }
}