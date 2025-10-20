using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using jellyfin_ani_sync.Interfaces;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync {
    public class SessionServerEntry : IHostedService {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IApplicationPaths _applicationPaths;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<SessionServerEntry> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IAsyncDelayer _delayer;


        private readonly ILibraryManager _libraryManager;
        private readonly IFileSystem _fileSystem;

        public SessionServerEntry(ISessionManager sessionManager, ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory, ILibraryManager libraryManager, IFileSystem fileSystem,
            IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor,
            IApplicationPaths applicationPaths, IMemoryCache memoryCache) {
            _httpClientFactory = httpClientFactory;
            _serverApplicationHost = serverApplicationHost;
            _httpContextAccessor = httpContextAccessor;
            _applicationPaths = applicationPaths;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<SessionServerEntry>();
            _sessionManager = sessionManager;
            _libraryManager = libraryManager;
            _fileSystem = fileSystem;
            _memoryCache = memoryCache;
            _delayer = new Delayer();
        }

        public Task StartAsync(CancellationToken cancellationToken) {
            _sessionManager.PlaybackStopped += PlaybackStopped;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) {
            _sessionManager.PlaybackStopped -= PlaybackStopped;
            return Task.CompletedTask;
        }

        public async void PlaybackStopped(object sender, PlaybackStopEventArgs e) {
            try {
                UpdateProviderStatus updateProviderStatus = new UpdateProviderStatus(_fileSystem, _libraryManager, _loggerFactory, _httpContextAccessor, _serverApplicationHost, _httpClientFactory, _applicationPaths, _memoryCache, _delayer);
                foreach (User user in e.Users) {
                    await updateProviderStatus.Update(e.Item, user.Id, e.PlayedToCompletion);
                }
            } catch (Exception exception) {
                _logger.LogError($"Fatal error occured during anime sync job: {exception}");
            }
        }
    }
}