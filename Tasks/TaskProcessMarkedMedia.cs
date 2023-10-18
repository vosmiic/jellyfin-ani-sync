using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync;

public class TaskProcessMarkedMedia {
    public List<(Guid userId, BaseItem baseItem)> itemsToUpdate { get; set; } = new ();
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<TaskProcessMarkedMedia> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IFileSystem _fileSystem;
    private readonly IMemoryCache _memoryCache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServerApplicationHost _serverApplicationHost;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApplicationPaths _applicationPaths;

    public TaskProcessMarkedMedia(ILoggerFactory loggerFactory, ILibraryManager libraryManager, IFileSystem fileSystem, IMemoryCache memoryCache, IHttpContextAccessor httpContextAccessor, IServerApplicationHost serverApplicationHost, IHttpClientFactory httpClientFactory, IApplicationPaths applicationPaths) {
        _loggerFactory = loggerFactory;
        _libraryManager = libraryManager;
        _fileSystem = fileSystem;
        _memoryCache = memoryCache;
        _httpContextAccessor = httpContextAccessor;
        _serverApplicationHost = serverApplicationHost;
        _httpClientFactory = httpClientFactory;
        _applicationPaths = applicationPaths;
        _logger = loggerFactory.CreateLogger<TaskProcessMarkedMedia>();
    }

    public async Task RunUpdate() {
        while (itemsToUpdate.Any()) {
            var item = itemsToUpdate.FirstOrDefault();
            var aniSyncConfigUser = Plugin.Instance?.PluginConfiguration.UserConfig.FirstOrDefault(uc => uc.UserId == item.userId);
            if (aniSyncConfigUser != null && UpdateProviderStatus.LibraryCheck(aniSyncConfigUser, _libraryManager, _fileSystem, _logger, item.baseItem)) {
                if (_memoryCache.TryGetValue("lastQuery", out DateTime lastQuery)) {
                    if ((DateTime.UtcNow - lastQuery).TotalSeconds <= 5) {
                        await Task.Delay(5000);
                        _logger.LogInformation("Too many requests! Waiting 5 seconds...");
                    }
                }

                _memoryCache.Set("lastQuery", DateTime.UtcNow, new MemoryCacheEntryOptions {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
                });
                UpdateProviderStatus updateProviderStatus = new UpdateProviderStatus(_fileSystem, _libraryManager, _loggerFactory, _httpContextAccessor, _serverApplicationHost, _httpClientFactory, _applicationPaths);
                await updateProviderStatus.Update(item.baseItem, item.userId, true);
            }

            itemsToUpdate.Remove(item);
        }
    }
}