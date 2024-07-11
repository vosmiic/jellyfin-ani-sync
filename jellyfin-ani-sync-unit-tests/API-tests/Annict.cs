using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api.Annict;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Annict;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace jellyfin_ani_sync_unit_tests.API_tests; 

public class Annict {
    private AnnictApiCalls _annictApiCalls;
    private ILoggerFactory _loggerFactory;
    private Mock<IServerApplicationHost> _serverApplicationHost;
    private Mock<IHttpContextAccessor> _httpContextAccessor;
    private IHttpClientFactory _httpClientFactory;

    private void Setup(HttpStatusCode responseCode, string responseContent) {
        _loggerFactory = new NullLoggerFactory();
        _serverApplicationHost = new Mock<IServerApplicationHost>();
        _httpContextAccessor = new Mock<IHttpContextAccessor>();
        Helpers.MockHttpCalls(responseCode, responseContent, ref _httpClientFactory);
        _annictApiCalls = new AnnictApiCalls(_httpClientFactory, _loggerFactory, _serverApplicationHost.Object, _httpContextAccessor.Object, new UserConfig {
            UserApiAuth = new [] {
                new UserApiAuth {
                    AccessToken = "accessToken",
                    Name = ApiName.Annict,
                    RefreshToken = "refreshToken"
                }
            }
        });
    }

    [Test]
    public async Task TestGenericSearch() {
        Setup(HttpStatusCode.OK, JsonSerializer.Serialize(new AnnictSearch.AnnictSearchMedia {
            AnnictSearchData = new AnnictSearch.AnnictSearchData {
                SearchWorks = new AnnictSearch.SearchWorks {
                    Nodes = new List<AnnictSearch.AnnictAnime> {
                        new()  {
                            Id = String.Empty
                        }
                    }
                }
            }
        }));
        var result = await _annictApiCalls.SearchAnime(String.Empty);
        
        Assert.IsNotNull(result?[0]?.Id);
    }

    [Test]
    public async Task TestGettingCurrentUser() {
        Setup(HttpStatusCode.OK, JsonSerializer.Serialize(new AnnictViewer.AnnictViewerRoot {
            AnnictSearchData = new AnnictViewer.AnnictViewerData {
                Viewer = new AnnictViewer.AnnictViewerDetails {
                    username = String.Empty
                }
            }
        }));
        var result = await _annictApiCalls.GetCurrentUser();
        
        Assert.IsNotNull(result);
    }
    
    [Test]
    public async Task TestGettingUserAnimeList() {
        Setup(HttpStatusCode.OK, JsonSerializer.Serialize(new AnnictMediaList.AnnictUserMediaList {
            AnnictSearchData = new AnnictMediaList.AnnictUserMediaListData {
                Viewer = new AnnictMediaList.AnnictUserMediaListViewer {
                    AnnictUserMediaListLibraryEntries = new AnnictMediaList.AnnictUserMediaListLibraryEntries {
                        Nodes = new List<AnnictSearch.AnnictAnime> {
                            new()  {
                                Id = String.Empty
                            }
                        },
                        PageInfo = new AnnictMediaList.PageInfo {
                            HasNextPage = false
                        }
                    }
                }
            }
        }));
        var result = await _annictApiCalls.GetAnimeList(AnnictSearch.AnnictMediaStatus.Watched);
        
        Assert.IsNotNull(result);
    }

    [Test]
    public async Task TestUpdatingAnime() {
        Setup(HttpStatusCode.OK, String.Empty);
        var result = await _annictApiCalls.UpdateAnime("V29yay02Njg=", AnnictSearch.AnnictMediaStatus.Watched);
        
        Assert.IsTrue(result);
    }
}