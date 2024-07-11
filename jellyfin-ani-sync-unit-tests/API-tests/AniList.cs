using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api.Anilist;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Models;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace jellyfin_ani_sync_unit_tests.API_tests;

public class AniList {
    private AniListApiCalls _aniListApiCalls;
    private ILoggerFactory _loggerFactory;
    private Mock<IServerApplicationHost> _serverApplicationHost;
    private Mock<IHttpContextAccessor> _httpContextAccessor;
    private IHttpClientFactory _httpClientFactory;

    private void Setup(List<Helpers.HttpCall> httpCalls) {
        _loggerFactory = new NullLoggerFactory();
        _serverApplicationHost = new Mock<IServerApplicationHost>();
        _httpContextAccessor = new Mock<IHttpContextAccessor>();
        Helpers.MockHttpCalls(httpCalls, ref _httpClientFactory);
        _aniListApiCalls = new AniListApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost.Object, _httpContextAccessor.Object, new UserConfig {
            UserApiAuth = new [] {
                new UserApiAuth {
                    AccessToken = "accessToken",
                    Name = ApiName.AniList,
                    RefreshToken = "refreshToken"
                }
            }
        });
    }

    [Test]
    public async Task TestGenericSearch() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Post,
                RequestUrlMatch = url => url.Contains("anilist"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new AniListSearch.AniListSearchMedia {
                    Data = new AniListSearch.AniListSearchData {
                        Page = new AniListSearch.Page {
                            Media = new List<AniListSearch.Media> {
                                new()  {
                                    Id = 1
                                }
                            },
                            PageInfo = new AniListSearch.PageInfo {
                                HasNextPage = false
                            }
                        }
                    }
                })
            }
        });

        var result = await _aniListApiCalls.SearchAnime(String.Empty);

        Assert.IsNotNull(result[0].Id);
    }

    [Test]
    public async Task TestGettingCurrentUser() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Post,
                RequestUrlMatch = url => url.Contains("anilist"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new AniListViewer.AniListGetViewer {
                    Data = new AniListViewer.AniListViewerData {
                        Viewer = new AniListViewer.Viewer {
                            Id = 1
                        }
                    }
                })
            }
        });
        var result = await _aniListApiCalls.GetCurrentUser();

        Assert.IsNotNull(result);
    }

    [Test]
    public async Task TestUpdatingAnime() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Post,
                RequestUrlMatch = url => url.Contains("anilist"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = String.Empty
            }
        });
        var result = await _aniListApiCalls.UpdateAnime(1,
            AniListSearch.MediaListStatus.Current,
            1,
            numberOfTimesRewatched: 1,
            startDate: DateTime.UtcNow - TimeSpan.FromHours(1),
            endDate: DateTime.UtcNow);

        Assert.IsTrue(result);
    }

    [Test]
    public async Task TestGenericSearchPaging() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Post,
                RequestUrlMatch = url => url.Contains("anilist"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new AniListSearch.AniListSearchMedia {
                    Data = new AniListSearch.AniListSearchData {
                        Page = new AniListSearch.Page {
                            Media = new List<AniListSearch.Media> {
                                new()  {
                                    Id = 1
                                }
                            },
                            PageInfo = new AniListSearch.PageInfo {
                                HasNextPage = true
                            }
                        }
                    }
                })
            }
        });
        var result = await _aniListApiCalls.SearchAnime(String.Empty);

        Assert.IsTrue(result.Count == 10);
    }

    [Test]
    public async Task TestGetAnime() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Post,
                RequestUrlMatch = url => url.Contains("anilist"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new AniListGet.AniListGetMedia {
                    Data = new AniListGet.AniListGetData {
                        Media = new AniListSearch.Media {
                            Id = 1
                        }
                    }
                })
            }
        });
        var result = await _aniListApiCalls.GetAnime(1);

        Assert.IsNotNull(result.Id);
    }
}