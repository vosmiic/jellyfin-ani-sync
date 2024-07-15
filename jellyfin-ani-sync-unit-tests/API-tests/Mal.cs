using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Interfaces;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Mal;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace jellyfin_ani_sync_unit_tests.API_tests;

public class Mal {
    private MalApiCalls _malApiCalls;
    private ILoggerFactory _loggerFactory;
    private Mock<IServerApplicationHost> _serverApplicationHost;
    private Mock<IHttpContextAccessor> _httpContextAccessor;
    private IHttpClientFactory _httpClientFactory;

    private void Setup(List<Helpers.HttpCall> httpCalls) {
        _loggerFactory = new NullLoggerFactory();
        _serverApplicationHost = new Mock<IServerApplicationHost>();
        _httpContextAccessor = new Mock<IHttpContextAccessor>();
        Helpers.MockHttpCalls(httpCalls, ref _httpClientFactory);
        MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());
        Mock<IAsyncDelayer> mockDelayer = new Mock<IAsyncDelayer>();
        _malApiCalls = new MalApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost.Object, _httpContextAccessor.Object, memoryCache, mockDelayer.Object, new UserConfig {
            UserApiAuth = new [] {
                new UserApiAuth {
                    AccessToken = "accessToken",
                    Name = ApiName.Mal,
                    RefreshToken = "refreshToken"
                }
            }
        });
    }

    [Test]
    public async Task TestGenericGetUserInformation() {
        Setup(new List<Helpers.HttpCall> {
            new()  {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith("/users/@me"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new MalApiCalls.User {
                    Id = 1,
                    Name = "name",
                    Location = "location",
                    JoinedAt = DateTime.UtcNow,
                    Picture = "picture"
                })
            }
        });
        var result = await _malApiCalls.GetUserInformation();
        Assert.IsNotNull(result.Id);
    }

    [Test]
    public async Task TestGenericSearchAnime() {
        Setup(new List<Helpers.HttpCall> {
            new()  {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith("/anime"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new SearchAnimeResponse {
                    Data = new List<AnimeList> {
                        new()  {
                            Anime = new Anime {
                                Id = 1
                            }
                        }
                    }
                })
            }
        });
        
        var result = await _malApiCalls.SearchAnime(String.Empty, new[] { String.Empty });
        Assert.IsTrue(result is { Count: > 0 });
    }

    [Test]
    public async Task TestGenericGetAnime() {
        int animeId = 1;
        Setup(new List<Helpers.HttpCall> {
            new()  {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith($"/anime/{animeId}"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new Anime {
                    Id = animeId
                })
            }
        });
        
        var result = await _malApiCalls.GetAnime(animeId);
        Assert.IsNotNull(result);
    }

    [Test]
    public async Task TestUpdateAnimeStatus() {
        Setup(new List<Helpers.HttpCall> {
            new()  {
                RequestMethod = HttpMethod.Put,
                RequestUrlMatch = url => url.EndsWith("my_list_status"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new UpdateAnimeStatusResponse())
            }
        });

        var makeChange = await _malApiCalls.UpdateAnimeStatus(339,
            1,
            Status.Completed,
            isRewatching: true,
            numberOfTimesRewatched: 1,
            startDate: DateTime.UtcNow - TimeSpan.FromHours(1),
            endDate: DateTime.UtcNow);
        Assert.IsNotNull(makeChange);
    }

    /*[Test]
    [TestCase("The Melancholy of Haruhi Suzumiya", "TheMelancholyofHaruhiSuzumiya")]
    [TestCase("Kono Subarashii Sekai ni Shukufuku wo! 2: Kono Subarashii Geijutsu ni Shukufuku wo! ", "KonoSubarashiiSekainiShukufukuwo!2:KonoSubarashiiGeijutsuniShuku")]
    public void TruncateStringAndRemoveSpaces(string input, string expected) =>
        Assert.IsTrue(MalHelper.TruncateQuery(input) == expected);*/
}