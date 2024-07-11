using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api.Shikimori;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Interfaces;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Shikimori;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace jellyfin_ani_sync_unit_tests.API_tests;

public class Shikimori {
    private ShikimoriApiCalls _shikimoriApiCalls;
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
        _shikimoriApiCalls = new ShikimoriApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost.Object, _httpContextAccessor.Object, memoryCache, mockDelayer.Object, new Dictionary<string, string> { { "User-Agent", "awd" } }, new UserConfig {
            UserApiAuth = new [] {
                new UserApiAuth {
                    AccessToken = "accessToken",
                    Name = ApiName.Shikimori,
                    RefreshToken = "refreshToken"
                }
            },
            KeyPairs = new List<KeyPairs>()
        });
    }

    [Test]
    public async Task TestGetUser() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith("/users/whoami"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new ShikimoriApiCalls.User {
                    Id = 1
                })
            }
        });
        var result = await _shikimoriApiCalls.GetUserInformation();

        Assert.IsNotNull(result);
    }

    [Test]
    public async Task TestGenericSearch() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Post,
                RequestUrlMatch = url => url.EndsWith("/graphql"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new ShikimoriApiCalls.GraphqlResponse<Dictionary<string, List<ShikimoriAnime>>> {
                    Data = new Dictionary<string, List<ShikimoriAnime>> {
                        {
                            "animes", new List<ShikimoriAnime> {
                                new () {
                                    Id = 1.ToString()
                                }
                            }
                        }
                    }
                })
            }
        });
        var result = await _shikimoriApiCalls.SearchAnime(String.Empty);

        Assert.IsNotNull(result?[0].Id);
    }

    [Test]
    public async Task TestGenericSearchRetrievingMaximumResults() {
        var shikimoriMediaList = new List<ShikimoriAnime>();
        for (int i = 0; i < 50; i++) {
            shikimoriMediaList.Add(new ShikimoriAnime {
                Id = i.ToString()
            });
        }

        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Post,
                RequestUrlMatch = url => url.EndsWith("/graphql"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new ShikimoriApiCalls.GraphqlResponse<Dictionary<string, List<ShikimoriAnime>>> {
                    Data = new Dictionary<string, List<ShikimoriAnime>> {
                        {
                            "animes", shikimoriMediaList
                        }
                    }
                })
            }
        });

        var result = await _shikimoriApiCalls.SearchAnime(String.Empty);

        Assert.IsTrue(result?.Count == 450);
    }

    [Test]
    public async Task TestGetAnime() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Post,
                RequestUrlMatch = url => url.EndsWith("/graphql"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new ShikimoriApiCalls.GraphqlResponse<Dictionary<string, List<ShikimoriAnime>>> {
                    Data = new Dictionary<string, List<ShikimoriAnime>> {
                        {
                            "animes", new List<ShikimoriAnime> {
                                new () {
                                    Id = 1.ToString()
                                }
                            }
                        }
                    }
                })
            }
        });
        var result = await _shikimoriApiCalls.GetAnime(1.ToString());

        Assert.IsNotNull(result);
    }

    [Test]
    public async Task TestGetRelatedAnime() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Post,
                RequestUrlMatch = url => url.EndsWith("/graphql"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new ShikimoriApiCalls.GraphqlResponse<Dictionary<string, List<ShikimoriAnime>>> {
                    Data = new Dictionary<string, List<ShikimoriAnime>> {
                        {
                            "animes", new List<ShikimoriAnime> {
                                new () {
                                    Id = 1.ToString()
                                }
                            }
                        }
                    }
                })
            }
        });
        var result = await _shikimoriApiCalls.GetAnime(1.ToString(), getRelated: true);

        Assert.IsNotNull(result);
    }

    [Test]
    public async Task TestUpdateAnime() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith("/users/whoami"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new ShikimoriApiCalls.User {
                    Id = 1
                })
            },
            new () {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith("/user_rates"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new List<ShikimoriUpdate.UserRate> {
                    new () {
                        AnimeId = 1.ToString()
                    }
                })
            },
            new () {
                RequestMethod = HttpMethod.Post,
                RequestUrlMatch = url => url.EndsWith("/user_rates"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = String.Empty
            }
        });
        var result = await _shikimoriApiCalls.UpdateAnime(1.ToString(), ShikimoriUserRate.StatusEnum.watching, 1, 1);

        Assert.IsTrue(result);
    }

    [Test]
    public async Task TestGetAnimeList() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith("/users/whoami"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new ShikimoriApiCalls.User {
                    Id = 1
                })
            },
            new () {
                RequestMethod = HttpMethod.Post,
                RequestUrlMatch = url => url.EndsWith("/graphql"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new ShikimoriApiCalls.GraphqlResponse<Dictionary<string, List<ShikimoriAnime>>> {
                    Data = new Dictionary<string, List<ShikimoriAnime>> {
                        {
                            "animes", new List<ShikimoriAnime> {
                                new () {
                                    Id = 1.ToString()
                                }
                            }
                        }
                    }
                })
            }
        });
        var result = await _shikimoriApiCalls.GetUserAnimeList();

        Assert.IsNotNull(result);
    }
}