using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api.Kitsu;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Interfaces;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Kitsu;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace jellyfin_ani_sync_unit_tests.API_tests; 

public class Kitsu {
    private KitsuApiCalls _kitsuApiCalls;
    private ILoggerFactory _loggerFactory;
    private Mock<IServerApplicationHost> _serverApplicationHost;
    private Mock<IHttpContextAccessor> _httpContextAccessor;
    private IHttpClientFactory _httpClientFactory;
    private MemoryCache _memoryCache;
    private Mock<IAsyncDelayer> _mockDelayer;

    private void Setup(List<Helpers.HttpCall> httpCalls) {
        _loggerFactory = new NullLoggerFactory();
        _serverApplicationHost = new Mock<IServerApplicationHost>();
        _httpContextAccessor = new Mock<IHttpContextAccessor>();
        Helpers.MockHttpCalls(httpCalls, ref _httpClientFactory);
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockDelayer = new Mock<IAsyncDelayer>();
        _kitsuApiCalls = new KitsuApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost.Object, _httpContextAccessor.Object, _memoryCache, _mockDelayer.Object, new UserConfig {
            UserApiAuth = new [] {
                new UserApiAuth {
                    AccessToken = "accessToken",
                    Name = ApiName.Kitsu,
                    RefreshToken = "refreshToken"
                }
            },
            KeyPairs = new List<KeyPairs> {
                new KeyPairs {
                    Key = "KitsuUserId",
                    Value = 1.ToString()
                }
            }
        });
    }
    
    [Test]
    public async Task TestGenericSearch() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith("/anime"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new KitsuSearch.KitsuSearchMedia {
                    KitsuSearchData = new List<KitsuSearch.KitsuAnime> {
                        new()  {
                            Id = 1
                        }
                    }
                })
            }
        });
        var result = await _kitsuApiCalls.SearchAnime(String.Empty);
        
        Assert.IsNotNull(result[0].Id);
    }

    [Test]
    public async Task TestGetUserInformation() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith("/users"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new KitsuGetUser.KitsuUserRoot {
                    KitsuUserList = new List<KitsuGetUser.KitsuUserData> {
                        new () {
                            Id = 1,
                            KitsuUser = new KitsuGetUser.KitsuUser {
                                Id = 2,
                                Name = String.Empty
                            }
                        }
                    }
                })
            }
        });
        var result = await _kitsuApiCalls.GetUserInformation();
        
        Assert.IsNotNull(result);
    }

    [Test]
    public async Task TestGetAnime() {
        int id = 1;
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith($"/anime/{id}"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new KitsuGet.KitsuGetAnime {
                    KitsuAnimeData = new KitsuSearch.KitsuAnime {
                        Id = id
                    }
                })
            }
        });
        var result = await _kitsuApiCalls.GetAnime(id);
        
        Assert.IsNotNull(result.KitsuAnimeData.Id);
    }

    [Test]
    public async Task TestGetUserList() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith("library-entries"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new KitsuUpdate.KitsuLibraryEntryListRoot {
                    Data = new List<KitsuUpdate.KitsuLibraryEntry> {
                        new () {
                            Id = 1,
                            Attributes = new KitsuUpdate.Attributes {
                                Progress = 1
                            }
                        }
                    }
                })
            }
        });
        var result = await _kitsuApiCalls.GetUserAnimeStatus(1, 1);
        
        Assert.IsNotNull(result.Attributes.Progress);
    }

    [Test]
    public async Task TestUpdateAnimeStatusWithoutFetchingUserId() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.Contains("library-entries"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new KitsuUpdate.KitsuLibraryEntryListRoot {
                    Data = new List<KitsuUpdate.KitsuLibraryEntry> {
                        new () {
                            Id = 1,
                            Attributes = new KitsuUpdate.Attributes {
                                Progress = 1
                            }
                        }
                    }
                })
            }
        });
        var result = await _kitsuApiCalls.UpdateAnimeStatus(1,
            1,
            KitsuUpdate.Status.current,
            isRewatching: true,
            numberOfTimesRewatched: 1,
            startDate: DateTime.UtcNow - TimeSpan.FromHours(1),
            endDate: DateTime.UtcNow);
        
        Assert.IsTrue(result);
    }
    
    [Test]
    public async Task TestUpdateAnimeStatusAndFetchUserId() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith("/users"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new KitsuGetUser.KitsuUserRoot {
                    KitsuUserList = new List<KitsuGetUser.KitsuUserData> {
                        new () {
                            Id = 1,
                            KitsuUser = new KitsuGetUser.KitsuUser {
                                Id = 2,
                                Name = String.Empty
                            }
                        }
                    }
                })
            },
            new () {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.Contains("library-entries"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new KitsuUpdate.KitsuLibraryEntryListRoot {
                    Data = new List<KitsuUpdate.KitsuLibraryEntry> {
                        new () {
                            Id = 1,
                            Attributes = new KitsuUpdate.Attributes {
                                Progress = 1
                            }
                        }
                    }
                })
            }
        });

        var result = await _kitsuApiCalls.UpdateAnimeStatus(1,
            1,
            KitsuUpdate.Status.current,
            isRewatching: true,
            numberOfTimesRewatched: 1,
            startDate: DateTime.UtcNow - TimeSpan.FromHours(1),
            endDate: DateTime.UtcNow);
        
        Assert.IsTrue(result);
    }

    [Test]
    public async Task TestGetRelatedAnime() {
        Setup(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith("media-relationships"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new KitsuMediaRelationship.MediaRelationship {
                    Data = new List<KitsuMediaRelationship.RelationshipData> {
                        new () {
                            Id = 1.ToString(),
                            Relationships = new KitsuMediaRelationship.Relationships {
                                Destination = new KitsuMediaRelationship.Destination {
                                    RelationshipData = new KitsuMediaRelationship.RelationshipData {
                                        Id = 1.ToString(),
                                        Type = "anime"
                                    }
                                }
                            },
                            Attributes = new KitsuMediaRelationship.Attributes {
                                RelationType = KitsuMediaRelationship.RelationType.sequel
                            }
                        }
                    },
                    Included = new List<KitsuSearch.KitsuAnime> {
                        new () {
                            Id = 1,
                            RelationType = KitsuMediaRelationship.RelationType.sequel
                        }
                    }
                })
            }
        });
        var result = await _kitsuApiCalls.GetRelatedAnime(1);
        
        Assert.IsNotNull(result);
    }
}