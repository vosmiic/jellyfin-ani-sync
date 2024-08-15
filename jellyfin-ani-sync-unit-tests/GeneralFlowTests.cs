using System;
using System.Net.Http;
using System.Threading.Tasks;
using jellyfin_ani_sync;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Interfaces;
using jellyfin_ani_sync.Models.Mal;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace jellyfin_ani_sync_unit_tests;

public class GeneralFlowTests {
    private Mock<IFileSystem> _mockFileSystem { get; set; }
    private Mock<ILibraryManager> _mockLibraryManager { get; set; }
    private ILoggerFactory _LoggerFactory { get; set; }
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor { get; set; }
    private Mock<IServerApplicationHost> _mockServerApplicationHost { get; set; }
    private Mock<IHttpClientFactory> _mockHttpClientFactory { get; set; }
    private Mock<IApplicationPaths> _mockApplicationPaths { get; set; }
    private MemoryCache _memoryCache { get; set; }
    private Mock<IAsyncDelayer> _mockAsyncDelayer { get; set; }
    private Mock<IApiCallHelpers> _mockApiCallHelpers { get; set; }
    private UpdateProviderStatus _updateProviderStatus { get; set; }

    [OneTimeSetUp]
    public void OneTimeSetUp() {
        _mockFileSystem = new Mock<IFileSystem>();
        _mockLibraryManager = new Mock<ILibraryManager>();
        _LoggerFactory = new NullLoggerFactory();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockServerApplicationHost = new Mock<IServerApplicationHost>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockApplicationPaths = new Mock<IApplicationPaths>();
        _mockApiCallHelpers = new Mock<IApiCallHelpers>();
    }

    [SetUp]
    public void SetUp() {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockAsyncDelayer = new Mock<IAsyncDelayer>();
        _updateProviderStatus = new UpdateProviderStatus(_mockFileSystem.Object,
            _mockLibraryManager.Object, _LoggerFactory, _mockHttpContextAccessor.Object,
            _mockServerApplicationHost.Object, _mockHttpClientFactory.Object, _mockApplicationPaths.Object,
            _memoryCache, _mockAsyncDelayer.Object);
        _updateProviderStatus.ApiCallHelpers = _mockApiCallHelpers.Object;
    }

    [Test]
    public async Task UpdateAnimeWithoutExistingStatusAndFinished() {
        int episodesWatched = 10;
        Anime detectedAnime = new Anime {
            Id = 1,
            Title = "title",
            NumEpisodes = episodesWatched
        };
        
        _mockApiCallHelpers.Setup(s => s.UpdateAnime(1, episodesWatched,
            Status.Completed, It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>())).Returns(Task.FromResult(new UpdateAnimeStatusResponse()));
        
        await _updateProviderStatus.UpdateAnimeStatus(detectedAnime, episodesWatched);
        
        _mockApiCallHelpers.Verify(s => s.UpdateAnime(1, episodesWatched,
            Status.Completed, It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>()), Times.Once);
    }
}