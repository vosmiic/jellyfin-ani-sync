using System;
using System.Net.Http;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync;

public class UserDataServerEntry : IServerEntryPoint {
    private readonly IUserDataManager _userDataManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILibraryManager _libraryManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServerApplicationHost _serverApplicationHost;
    private readonly IHttpClientFactory _httpClientFactory;

    public UserDataServerEntry(IUserDataManager userDataManager,
        IFileSystem fileSystem,
        ILibraryManager libraryManager,
        ILoggerFactory loggerFactory,
        IHttpContextAccessor httpContextAccessor,
        IServerApplicationHost serverApplicationHost,
        IHttpClientFactory httpClientFactory) {
        _userDataManager = userDataManager;
        _fileSystem = fileSystem;
        _libraryManager = libraryManager;
        _loggerFactory = loggerFactory;
        _httpContextAccessor = httpContextAccessor;
        _serverApplicationHost = serverApplicationHost;
        _httpClientFactory = httpClientFactory;
    }

    public Task RunAsync() {
        _userDataManager.UserDataSaved += UserDataManagerOnUserDataSaved;
        return Task.CompletedTask;
    }

    private async void UserDataManagerOnUserDataSaved(object sender, UserDataSaveEventArgs e) {
        if (e.SaveReason == UserDataSaveReason.TogglePlayed) {
            if (!e.UserData.Played || e.Item is not Video) return;


            UpdateProviderStatus updateProviderStatus = new UpdateProviderStatus(_fileSystem, _libraryManager, _loggerFactory, _httpContextAccessor, _serverApplicationHost, _httpClientFactory);
            await updateProviderStatus.Update(e.Item, e.UserId, true);
        }
    }

    public void Dispose() {
        _userDataManager.UserDataSaved -= UserDataManagerOnUserDataSaved;
        GC.SuppressFinalize(this);
    }
}