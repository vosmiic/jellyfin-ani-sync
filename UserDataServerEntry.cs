#nullable enable
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Interfaces;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync {
    public class UserDataServerEntry : IHostedService {
        private readonly IUserDataManager _userDataManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILibraryManager _libraryManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly IApplicationPaths _applicationPaths;
        private readonly IAsyncDelayer _delayer;
        private readonly TaskProcessMarkedMedia _taskProcessMarkedMedia;
        private Task? _updateTask;

        public UserDataServerEntry(IUserDataManager userDataManager,
            IFileSystem fileSystem,
            ILibraryManager libraryManager,
            ILoggerFactory loggerFactory,
            IHttpContextAccessor httpContextAccessor,
            IServerApplicationHost serverApplicationHost,
            IHttpClientFactory httpClientFactory,
            IMemoryCache memoryCache,
            IApplicationPaths applicationPaths) {
            _userDataManager = userDataManager;
            _fileSystem = fileSystem;
            _libraryManager = libraryManager;
            loggerFactory.CreateLogger<UpdateProviderStatus>();
            _httpContextAccessor = httpContextAccessor;
            _serverApplicationHost = serverApplicationHost;
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _applicationPaths = applicationPaths;
            _delayer = new Delayer();
            _taskProcessMarkedMedia = new TaskProcessMarkedMedia(loggerFactory, _libraryManager, _fileSystem, _memoryCache, _httpContextAccessor, _serverApplicationHost, _httpClientFactory, _applicationPaths, _delayer);
        }

        public Task StartAsync(CancellationToken cancellationToken) {
            _userDataManager.UserDataSaved += UserDataManagerOnUserDataSaved;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) {
            _userDataManager.UserDataSaved -= UserDataManagerOnUserDataSaved;
            return Task.CompletedTask;
        }

        private void UserDataManagerOnUserDataSaved(object? sender, UserDataSaveEventArgs e) {
            if (e.SaveReason == UserDataSaveReason.TogglePlayed && Plugin.Instance?.PluginConfiguration.watchedTickboxUpdatesProvider == true) {
                if (!e.UserData.Played || e.Item is not Video) return;
                // asynchronous call so it doesn't prevent the UI marking the media as watched
                Episode? episode = e.Item as Episode;
                _taskProcessMarkedMedia.AddToUpdateList((e.UserId, episode?.Season.Id, e.Item as Video));
                if (_updateTask == null || _updateTask.IsCompleted) {
                    _updateTask = _taskProcessMarkedMedia.RunUpdate();
                }
            }
        }
    }
}
