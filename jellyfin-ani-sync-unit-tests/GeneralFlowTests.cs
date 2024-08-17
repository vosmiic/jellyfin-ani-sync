using System;
using System.Net.Http;
using System.Threading.Tasks;
using jellyfin_ani_sync;
using jellyfin_ani_sync.Configuration;
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

    [SetUp]
    public void SetUp() {
        _mockFileSystem = new Mock<IFileSystem>();
        _mockLibraryManager = new Mock<ILibraryManager>();
        _LoggerFactory = new NullLoggerFactory();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockServerApplicationHost = new Mock<IServerApplicationHost>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockApplicationPaths = new Mock<IApplicationPaths>();
        _mockApiCallHelpers = new Mock<IApiCallHelpers>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockAsyncDelayer = new Mock<IAsyncDelayer>();
        _updateProviderStatus = new UpdateProviderStatus(_mockFileSystem.Object,
            _mockLibraryManager.Object, _LoggerFactory, _mockHttpContextAccessor.Object,
            _mockServerApplicationHost.Object, _mockHttpClientFactory.Object, _mockApplicationPaths.Object,
            _memoryCache, _mockAsyncDelayer.Object);
        _updateProviderStatus.ApiCallHelpers = _mockApiCallHelpers.Object;
    }

    /// <summary>
    /// Mark an anime that doesn't exist in the user list as complete.
    /// </summary>
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

    /// <summary>
    /// Tests updating an anime not present in the users list.
    /// </summary>
    [Test]
    public async Task UpdateAnimeWithoutExistingStatusAndInProgress() {
        int episodesWatched = 10;
        Anime detectedAnime = new Anime {
            Id = 1,
            Title = "title",
            NumEpisodes = episodesWatched + 1
        };

        _mockApiCallHelpers.Setup(s => s.UpdateAnime(1, episodesWatched,
            Status.Watching, It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>())).Returns(Task.FromResult(new UpdateAnimeStatusResponse()));

        await _updateProviderStatus.UpdateAnimeStatus(detectedAnime, episodesWatched);

        _mockApiCallHelpers.Verify(s => s.UpdateAnime(1, episodesWatched,
            Status.Watching, It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>()), Times.Once);
    }


    /// <summary>
    /// Checks if we handle a null response from the API.
    /// </summary>
    [Test]
    public async Task UpdateAnimeNullResponseHandling() {
        int episodesWatched = 10;
        Anime detectedAnime = new Anime {
            Id = 1,
            Title = "title",
            NumEpisodes = episodesWatched + 1
        };

        _mockApiCallHelpers.Setup(s => s.UpdateAnime(1, episodesWatched,
            Status.Watching, It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>())).Returns(Task.FromResult<UpdateAnimeStatusResponse>(null));

        Assert.DoesNotThrowAsync(async () => await _updateProviderStatus.UpdateAnimeStatus(detectedAnime, episodesWatched));
    }

    /// <summary>
    /// Tests the case of watching an episode you have already watched; expected should be to skip the update process.
    /// </summary>
    [Test]
    public async Task UpdateAnimeWithAlreadyWatched() {
        int episodesWatched = 10;
        Anime detectedAnime = new Anime {
            Id = 1,
            Title = "title",
            MyListStatus = new MyListStatus {
                NumEpisodesWatched = episodesWatched + 1
            }
        };

        await _updateProviderStatus.UpdateAnimeStatus(detectedAnime, episodesWatched);

        _mockApiCallHelpers.Verify(s => s.UpdateAnime(It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<Status>(), It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>()), Times.Never);
    }

    /// <summary>
    /// Update with a single episode (movie/ova).
    /// </summary>
    [Test]
    public async Task UpdateAnimeWithSingleEpisode() {
        int episodesWatched = 1;
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

        _mockApiCallHelpers.Verify(s => s.UpdateAnime(It.IsAny<int>(), It.IsAny<int>(),
            Status.Completed, It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>()), Times.Once);
    }

    /// <summary>
    /// Update while rewatching and complete at the same time (user watches the last episode of a series they have already seen).
    /// </summary>
    [TestCase(ApiName.Mal, 2)]
    [TestCase(ApiName.AniList, 1)]
    public async Task UpdateAnimeWithReWatchCompleted(ApiName apiName, int updateMethodTimesCalled) {
        int episodesWatched = 10;
        Anime detectedAnime = new Anime {
            Id = 1,
            Title = "title",
            NumEpisodes = episodesWatched,
            MyListStatus = new MyListStatus {
                NumEpisodesWatched = episodesWatched
            }
        };

        _mockApiCallHelpers.Setup(s => s.UpdateAnime(1, episodesWatched,
            Status.Completed, It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>())).Returns(Task.FromResult(new UpdateAnimeStatusResponse()));

        _updateProviderStatus.ApiName = apiName;
        await _updateProviderStatus.UpdateAnimeStatus(detectedAnime, episodesWatched, setRewatching: true);

        _mockApiCallHelpers.Verify(s => s.UpdateAnime(It.IsAny<int>(), It.IsAny<int>(),
            Status.Completed, It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>()), Times.Exactly(updateMethodTimesCalled));
    }

    /// <summary>
    /// Update in-progress rewatch.
    /// </summary>
    [TestCase(ApiName.Mal)]
    [TestCase(ApiName.AniList)]
    public async Task UpdateAnimeReWatchingInProgress(ApiName apiName) {
        int episodesWatched = 10;
        Anime detectedAnime = new Anime {
            Id = 1,
            Title = "title",
            NumEpisodes = episodesWatched + 1,
            MyListStatus = new MyListStatus {
                NumEpisodesWatched = episodesWatched - 1,
                IsRewatching = true
            }
        };

        _mockApiCallHelpers.Setup(s => s.UpdateAnime(1, episodesWatched,
            It.IsAny<Status>(), It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>())).Returns(Task.FromResult(new UpdateAnimeStatusResponse()));

        _updateProviderStatus.ApiName = apiName;
        await _updateProviderStatus.UpdateAnimeStatus(detectedAnime, episodesWatched, setRewatching: true);

        _mockApiCallHelpers.Verify(s => s.UpdateAnime(It.IsAny<int>(), It.IsAny<int>(),
            apiName == ApiName.Mal ? Status.Completed : Status.Watching, It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>()), Times.Once);
    }

    /// <summary>
    /// Update in-progress first episode rewatch.
    /// </summary>
    [Test]
    public async Task UpdateAnimeReWatchingFirstEpisode() {
        int episodesWatched = 1;
        Anime detectedAnime = new Anime {
            Id = 1,
            Title = "title",
            NumEpisodes = episodesWatched + 1,
            MyListStatus = new MyListStatus {
                NumEpisodesWatched = 0
            }
        };

        _mockApiCallHelpers.Setup(s => s.UpdateAnime(1, episodesWatched,
            It.IsAny<Status>(), It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>())).Returns(Task.FromResult(new UpdateAnimeStatusResponse()));

        await _updateProviderStatus.UpdateAnimeStatus(detectedAnime, episodesWatched);

        _mockApiCallHelpers.Verify(s => s.UpdateAnime(It.IsAny<int>(), It.IsAny<int>(),
            Status.Watching, It.IsAny<bool?>(), It.IsAny<int?>(),
            It.Is<DateTime>(startDate => startDate >= DateTime.Now - TimeSpan.FromSeconds(30) && startDate < DateTime.Now + TimeSpan.FromSeconds(30)), // give range in case test takes some time for some reason
            It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>()), Times.Once);
    }

    /// <summary>
    /// Update new rewatch.
    /// </summary>
    [TestCase(ApiName.Mal, 1)]
    [TestCase(ApiName.AniList, 2)]
    public async Task UpdateAnimeNewReWatching(ApiName apiName, int expectedMethodCallCount) {
        int episodesWatched = 2;
        Anime detectedAnime = new Anime {
            Id = 1,
            Title = "title",
            NumEpisodes = episodesWatched + 2,
            MyListStatus = new MyListStatus {
                NumEpisodesWatched = episodesWatched + 1
            }
        };

        _mockApiCallHelpers.Setup(s => s.UpdateAnime(1, episodesWatched,
            It.IsAny<Status>(), It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>())).Returns(Task.FromResult(new UpdateAnimeStatusResponse()));

        _updateProviderStatus.ApiName = apiName;
        await _updateProviderStatus.UpdateAnimeStatus(detectedAnime, episodesWatched, setRewatching: true);

        _mockApiCallHelpers.Verify(s => s.UpdateAnime(It.IsAny<int>(), It.IsAny<int>(),
            Status.Completed, true, It.IsAny<int?>(), It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(), It.IsAny<string>(),
            It.IsAny<AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse>(), It.IsAny<bool?>()), Times.Exactly(expectedMethodCallCount));
    }
}