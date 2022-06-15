using System;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync {
    public class SessionServerEntry : IServerEntryPoint {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IApplicationPaths _applicationPaths;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ISessionManager _sessionManager;


        private readonly ILibraryManager _libraryManager;
        private readonly IFileSystem _fileSystem;

        public SessionServerEntry(ISessionManager sessionManager, ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory, ILibraryManager libraryManager, IFileSystem fileSystem,
            IServerApplicationHost serverApplicationHost, IHttpContextAccessor httpContextAccessor,
            IApplicationPaths applicationPaths) {
            _httpClientFactory = httpClientFactory;
            _serverApplicationHost = serverApplicationHost;
            _httpContextAccessor = httpContextAccessor;
            _applicationPaths = applicationPaths;
            _loggerFactory = loggerFactory;
            _sessionManager = sessionManager;
            _libraryManager = libraryManager;
            _fileSystem = fileSystem;
        }

        public Task RunAsync() {
            _sessionManager.PlaybackStopped += PlaybackStopped;
            return Task.CompletedTask;
        }

        public async void PlaybackStopped(object sender, PlaybackStopEventArgs e) {
            if (Plugin.Instance.PluginConfiguration.ProviderApiAuth is { Length: > 0 }) {
                UpdateProviderStatus updateProviderStatus = new UpdateProviderStatus(_fileSystem, _libraryManager, _loggerFactory, _httpContextAccessor, _serverApplicationHost, _httpClientFactory, _applicationPaths);
                foreach (User user in e.Users) {
                    await updateProviderStatus.Update(e.Item, user.Id, e.PlayedToCompletion);
                }
            }
        }


        public void Dispose() {
            _sessionManager.PlaybackStopped -= PlaybackStopped;
            GC.SuppressFinalize(this);
        }
    }
}