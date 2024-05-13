using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Helpers;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
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
            }
        }
    }

    private List<Episode> GetMaxEpisodeAndCompletedTime(Series series) {
        List<Episode> returnDictionary = new List<Episode>();
        
        _logger.LogInformation($"(Sync) Getting {series.Name} seasons latest watched episode");
        var seasons = series.Children.OfType<Season>().Select(baseItem => baseItem).ToList();
        _logger.LogInformation($"(Sync) Series {series.Name} contains {seasons.Count} seasons");
        foreach (Season season in seasons) {
            _logger.LogInformation($"(Sync) Getting user data for {season.Name} of {series.Name}...");
            if (season.IndexNumber == null) {
                _logger.LogError($"(Sync) Season index number is null. Skipping...");
                continue;
            }
            
            var query = new InternalItemsQuery(_userManager.GetUserById(_userId)) {
                MediaTypes = [ MediaType.Video ],
                ParentId = season.ParentId,
                ParentIndexNumber = season.IndexNumber,
                Recursive = true
            };
            
            var itemList = _libraryManager.GetItemList(query);

            List<Episode> episodes = itemList.OfType<Episode>().Select(baseItem => baseItem).ToList();

            _logger.LogInformation($"(Sync) Season contains {episodes.Count} episodes");
            var episodesWatched = episodes.Where(item => _userDataManager.GetUserData(_userId, item).Played).ToList();
            _logger.LogInformation($"(Sync) User has watched {episodesWatched.Count} out of {episodes.Count} episodes of this season");
            Episode highestEpisodeWatched;
            if (episodesWatched.Any()) {
                highestEpisodeWatched = episodesWatched.OrderByDescending(item => item.IndexNumber).First();
                _logger.LogInformation($"(Sync) The latest watched episode for this user of this season is {highestEpisodeWatched.IndexNumber}");
            } else {
                _logger.LogInformation($"(Sync) User has not watched any episodes of this season, skipping...");
                continue;
            }
            returnDictionary.Add(highestEpisodeWatched/*, _userDataManager.GetUserData(_userId, highestEpisodeWatched).LastPlayedDate ?? DateTime.UtcNow*/);
        }
        
        _logger.LogInformation($"(Sync) Found {returnDictionary.Count} seasons that contain user data");
        return returnDictionary;
    }
}