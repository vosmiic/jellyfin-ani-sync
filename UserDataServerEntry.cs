using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync {
    public class UserDataServerEntry : IServerEntryPoint {
        private readonly IUserDataManager _userDataManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILibraryManager _libraryManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly IApplicationPaths _applicationPaths;
        private readonly ILogger<UpdateProviderStatus> _logger;

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
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<UpdateProviderStatus>();
            _httpContextAccessor = httpContextAccessor;
            _serverApplicationHost = serverApplicationHost;
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _applicationPaths = applicationPaths;
        }

        public Task RunAsync() {
            _userDataManager.UserDataSaved += UserDataManagerOnUserDataSaved;
            return Task.CompletedTask;
        }

        private async void UserDataManagerOnUserDataSaved(object sender, UserDataSaveEventArgs e) {
            if (e.SaveReason == UserDataSaveReason.TogglePlayed && Plugin.Instance.PluginConfiguration.watchedTickboxUpdatesProvider) {
                if (!e.UserData.Played || e.Item is not Video) return;
                await UpdateJob(e);
            }
        }

        private async Task UpdateJob(UserDataSaveEventArgs e) {
            var aniSyncConfigUser = Plugin.Instance?.PluginConfiguration.UserConfig.FirstOrDefault(item => item.UserId == e.UserId);
            if (aniSyncConfigUser != null && UpdateProviderStatus.LibraryCheck(aniSyncConfigUser, _libraryManager, _fileSystem, _logger, e.Item)) {
                if (_memoryCache.TryGetValue("lastQuery", out DateTime lastQuery)) {
                    if ((DateTime.UtcNow - lastQuery).TotalSeconds <= 5) {
                        Thread.Sleep(5000);
                        _logger.LogInformation("Too many requests! Waiting 5 seconds...");
                    }
                }
                
                _memoryCache.Set("lastQuery", DateTime.UtcNow, new MemoryCacheEntryOptions {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
                });
                UpdateProviderStatus updateProviderStatus = new UpdateProviderStatus(_fileSystem, _libraryManager, _loggerFactory, _httpContextAccessor, _serverApplicationHost, _httpClientFactory, _applicationPaths);
                await updateProviderStatus.Update(e.Item, e.UserId, true);
            }
        }

        public void Dispose() {
            _userDataManager.UserDataSaved -= UserDataManagerOnUserDataSaved;
            GC.SuppressFinalize(this);
        }
    }
}