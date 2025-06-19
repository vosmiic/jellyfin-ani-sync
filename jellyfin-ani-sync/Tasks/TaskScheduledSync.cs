using Jellyfin.Data.Entities;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System;
using jellyfin_ani_sync.Interfaces;

namespace jellyfin_ani_sync {
    public class TaskScheduledSync : IScheduledTask {
        private readonly IUserManager _userManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IApplicationPaths _applicationPaths;
        private readonly IUserDataManager _userDataManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger<TaskScheduledSync> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IAsyncDelayer _delayer;
        private IServerApplicationHost _serverApplicationHost;
        private IHttpContextAccessor _httpContextAccessor;
        private const string _moduleName = "Scheduled Sync";

        public string Name => "AniSync sync anime";
        public string Key => "ScheduledSync";
        public string Description => "Sync your watch progress to the default provider";
        public string Category => "AniSync";

        public TaskScheduledSync(
            IUserManager userManager,
            ILibraryManager libraryManager,
            ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory,
            IApplicationPaths applicationPaths,
            IUserDataManager userDataManager,
            IFileSystem fileSystem,
            IMemoryCache memoryCache
        ) {
            _userManager = userManager;
            _libraryManager = libraryManager;
            _loggerFactory = loggerFactory;
            _httpClientFactory = httpClientFactory;
            _applicationPaths = applicationPaths;
            _userDataManager = userDataManager;
            _fileSystem = fileSystem;
            _logger = loggerFactory.CreateLogger<TaskScheduledSync>();
            _memoryCache = memoryCache;
            _delayer = new Delayer();
        }

        private async Task SyncTask() {
            foreach (User user in _userManager.Users) {
                await (new SyncProviderFromLocal(_userManager, _libraryManager, _loggerFactory, _httpClientFactory, _applicationPaths, _userDataManager, _fileSystem, _memoryCache, _delayer, user.Id.ToString())).SyncFromLocal();
            }
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() {
            var trigger = new TaskTriggerInfo {
                Type = TaskTriggerInfo.TriggerInterval,
                IntervalTicks = TimeSpan.FromHours(4).Ticks
            };

            return new[] { trigger };
        }

        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress) {
            return Task.Run(async () => {
                await SyncTask();
            }, cancellationToken);
        }

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken) {
            await Task.Run(async () => {
                await SyncTask();
            }, cancellationToken);
        }
    }
}
