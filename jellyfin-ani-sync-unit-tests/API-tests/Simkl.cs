using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api.Shikimori;
using jellyfin_ani_sync.Api.Simkl;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Models.Simkl;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace jellyfin_ani_sync_unit_tests.API_tests;

public class Simkl {
    private SimklApiCalls _simklApiCalls;

    [SetUp]
    public void Setup() {
        var mockFactory = new Mock<IHttpClientFactory>();
        var client = new HttpClient();
        mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(client);
        IHttpClientFactory factory = mockFactory.Object;

        var mockLoggerFactory = new NullLoggerFactory();
        var mockServerApplicationHost = new Mock<IServerApplicationHost>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var userConfig = GetUserConfig.ManuallyGetUserConfig();
        _simklApiCalls = new SimklApiCalls(factory, mockLoggerFactory, mockServerApplicationHost.Object, mockHttpContextAccessor.Object, new Dictionary<string, string> { { "simkl-api-key", GetUserConfig.ManuallyGetProviderAuthConfig(ApiName.Simkl).ClientId } }, GetUserConfig.ManuallyGetUserConfig());
    }

    [Test]
    public async Task TestAuthCall() {
        var result = await _simklApiCalls.GetLastActivity();

        Assert.IsTrue(result);
    }

    [Test]
    public async Task TestSearchAnime() {
        var result = await _simklApiCalls.SearchAnime("monogatari");

        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result);
    }

    [Test]
    public async Task TestGetAnime() {
        var result = await _simklApiCalls.GetAnime(45006);

        Assert.IsNotNull(result?.Title);
    }

    [Test]
    public async Task TestGetUserList() {
        var result = await _simklApiCalls.GetUserAnimeList(SimklStatus.plantowatch);

        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result);
    }

    [Test]
    public async Task TestGetAnimeByIdLookup() {
        var result = await _simklApiCalls.GetAnimeByIdLookup(new AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse {
            MyAnimeList = 5081
        }, "Bakemonogatari");

        Assert.IsNotNull(result?.Title);
    }

    [Test]
    public async Task TestUpdateAnime() {
        var result = await _simklApiCalls.UpdateAnime(45006, SimklStatus.plantowatch, true, new AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse {
            AniDb = 6327
        }, 10);

        Assert.IsTrue(result);
    }
}