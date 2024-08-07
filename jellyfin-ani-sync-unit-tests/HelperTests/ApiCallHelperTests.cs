using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api.Kitsu;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Helpers;
using jellyfin_ani_sync.Interfaces;
using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Annict;
using jellyfin_ani_sync.Models.Kitsu;
using jellyfin_ani_sync.Models.Mal;
using jellyfin_ani_sync.Models.Shikimori;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace jellyfin_ani_sync_unit_tests.HelperTests;

public class ApiCallHelperTests {
    private List<int> uniqueIdList;

    [SetUp]
    public void Setup() {
        uniqueIdList = new List<int>();
    }
    
    [Test]
    public void AniListSearchAnimeConvertedListTest() {
        List<AniListSearch.Media> mediaList = new List<AniListSearch.Media>();
        for (int i = 0; i < 10; i++) {
            mediaList.Add(GetAniListMedia(true));
        }

        ApiCallHelpers apiCallHelpers = new ApiCallHelpers();
        List<Anime> animeList = apiCallHelpers.AniListSearchAnimeConvertedList(mediaList, true);
        Assert.AreEqual(10, animeList.Count);
        // Assert that the anime list contains the expected anime objects
        for (int i = 0; i < animeList.Count; i++) {
            Assert.AreEqual(mediaList[i].Id, animeList[i].Id);
            Assert.AreEqual(mediaList[i].Episodes, animeList[i].NumEpisodes);
            Assert.AreEqual(mediaList[i].Title.English, animeList[i].Title);
            Assert.AreEqual(mediaList[i].Title.English, animeList[i].AlternativeTitles.En);
            Assert.AreEqual(mediaList[i].Title.Native, animeList[i].AlternativeTitles.Ja);
            Assert.AreEqual(new List<string> { mediaList[i].Title.Romaji, mediaList[i].Title.UserPreferred, "Synonym1", "Synonym2" }, animeList[i].AlternativeTitles.Synonyms);
        }
    }

    [Test]
    public void KitsuSearchAnimeConvertedListTest() {
        List<KitsuSearch.KitsuAnime> mediaList = new List<KitsuSearch.KitsuAnime>();
        for (int i = 0; i < 10; i++) {
            mediaList.Add(GetKitsuAnime(true));
        }

        List<Anime> animeList = ApiCallHelpers.KitsuSearchAnimeConvertedList(mediaList);
        Assert.AreEqual(10, animeList.Count);
        // Assert that the anime list contains the expected anime objects
        for (int i = 0; i < animeList.Count; i++) {
            Assert.AreEqual(mediaList[i].Id, animeList[i].Id);
            Assert.AreEqual(mediaList[i].Attributes.EpisodeCount, animeList[i].NumEpisodes);
            Assert.AreEqual(mediaList[i].Attributes.Titles.English, animeList[i].Title);
            Assert.AreEqual(mediaList[i].Attributes.Titles.English, animeList[i].AlternativeTitles.En);
            Assert.AreEqual(mediaList[i].Attributes.Titles.Japanese, animeList[i].AlternativeTitles.Ja);
            Assert.AreEqual(new List<string> { mediaList[i].Attributes.Slug, mediaList[i].Attributes.CanonicalTitle, "Synonym1", "Synonym2" }, animeList[i].AlternativeTitles.Synonyms);
        }
    }

    [Test]
    public void AnnictSearchAnimeConvertedListTest() {
        List<AnnictSearch.AnnictAnime> mediaList = new List<AnnictSearch.AnnictAnime>();
        for (int i = 0; i < 10; i++) {
            mediaList.Add(GetAnnictAnime());
        }

        List<Anime> animeList = ApiCallHelpers.AnnictSearchAnimeConvertedList(mediaList);
        Assert.AreEqual(10, animeList.Count);
// Assert that the anime list contains the expected anime objects
        for (int i = 0; i < animeList.Count; i++) {
            Assert.AreEqual(mediaList[i].Id, animeList[i].AlternativeId);
            Assert.AreEqual(mediaList[i].TitleEn, animeList[i].Title);
            Assert.AreEqual(mediaList[i].MalAnimeId, animeList[i].Id.ToString());
            Assert.AreEqual(mediaList[i].NumberOfEpisodes, animeList[i].NumEpisodes);
        }
    }

    [Test]
    public void ShikimoriSearchAnimeConvertedListTest() {
        List<ShikimoriAnime> mediaList = new List<ShikimoriAnime>();
        for (int i = 0; i < 10; i++) {
            mediaList.Add(GetShikimoriAnime(true));
        }

        List<Anime> animeList = ApiCallHelpers.ShikimoriSearchAnimeConvertedList(mediaList, true);
        Assert.AreEqual(10, animeList.Count);
        // Assert that the anime list contains the expected anime objects
        for (int i = 0; i < animeList.Count; i++) {
            Assert.AreEqual(mediaList[i].MalId, animeList[i].Id.ToString());
            Assert.AreEqual(mediaList[i].Episodes, animeList[i].NumEpisodes);
            Assert.AreEqual(mediaList[i].Name, animeList[i].Title);
            Assert.AreEqual(mediaList[i].Synonyms.Append(mediaList[i].Russian).ToList(), animeList[i].AlternativeTitles.Synonyms);
            Assert.AreEqual(mediaList[i].English, animeList[i].AlternativeTitles.En);
            Assert.AreEqual(mediaList[i].Japanese, animeList[i].AlternativeTitles.Ja);
            Assert.IsNotNull(mediaList[i].UserRate);
            Assert.AreEqual(mediaList[i].UserRate.Episodes, animeList[i].MyListStatus.NumEpisodesWatched);
            Assert.AreEqual(mediaList[i].UserRate.Status == ShikimoriUserRate.StatusEnum.rewatching, animeList[i].MyListStatus.IsRewatching);
            Assert.AreEqual(mediaList[i].UserRate.Rewatches, animeList[i].MyListStatus.RewatchCount);
            Assert.IsNotNull(mediaList[i].Related);
            Assert.AreEqual(mediaList[i].Related.Count, animeList[i].RelatedAnime.Count);
        }
    }

    [Test]
    public void KitsuConfirmRelatedAnimeExists() {
        List<KitsuSearch.KitsuAnime> mediaList = new List<KitsuSearch.KitsuAnime>();
        List<int> idList = new List<int>();
        for (int i = 0; i < 10; i++) {
            mediaList.Add(GetKitsuAnime(true));
        }

        List<Anime> animeList = ApiCallHelpers.KitsuSearchAnimeConvertedList(mediaList);
        Assert.AreEqual(10, animeList.Count);
        // Assert that the anime list contains the expected anime objects
        foreach (var anime in animeList) {
            Assert.IsNotEmpty(anime.RelatedAnime);
            foreach (RelatedAnime relatedAnime in anime.RelatedAnime) {
                var matchingRelated = mediaList.First(item => item.Id == anime.Id).RelatedAnime.First(item => item.Id == relatedAnime.Anime.Id);
                switch (matchingRelated.RelationType) {
                    case KitsuMediaRelationship.RelationType.sequel:
                        Assert.IsTrue(relatedAnime.RelationType == RelationType.Sequel);
                        break;
                    case KitsuMediaRelationship.RelationType.side_story:
                    case KitsuMediaRelationship.RelationType.full_story:
                    case KitsuMediaRelationship.RelationType.parent_story:
                        Assert.IsTrue(relatedAnime.RelationType == RelationType.Side_Story);
                        break;
                    case KitsuMediaRelationship.RelationType.alternative_setting:
                    case KitsuMediaRelationship.RelationType.alternative_version:
                        Assert.IsTrue(relatedAnime.RelationType == RelationType.Alternative_Setting);
                        break;
                    case KitsuMediaRelationship.RelationType.spinoff:
                    case KitsuMediaRelationship.RelationType.adaptation:
                        Assert.IsTrue(relatedAnime.RelationType == RelationType.Spin_Off);
                        break;
                    default:
                        Assert.IsTrue(relatedAnime.RelationType == RelationType.Other);
                        break;
                }
                
                Assert.AreEqual(matchingRelated.Id, relatedAnime.Anime.Id);
                Assert.AreEqual(matchingRelated.Attributes.EpisodeCount, relatedAnime.Anime.NumEpisodes);
                Assert.AreEqual(matchingRelated.Attributes.Titles.English, relatedAnime.Anime.Title);
                Assert.AreEqual(matchingRelated.Attributes.Titles.English, relatedAnime.Anime.AlternativeTitles.En);
                Assert.AreEqual(matchingRelated.Attributes.Titles.Japanese, relatedAnime.Anime.AlternativeTitles.Ja);
                Assert.AreEqual(new List<string> { matchingRelated.Attributes.Slug, matchingRelated.Attributes.CanonicalTitle, "Synonym1", "Synonym2" }, relatedAnime.Anime.AlternativeTitles.Synonyms);
            }
        }
    }

    [TestCase(1, true, 1, KitsuUpdate.Status.completed, 1, true, 1, Status.Completed)]
    [TestCase(null, null, null, KitsuUpdate.Status.dropped, 0, false, 0, Status.Dropped)]
    [TestCase(10, false, 10, KitsuUpdate.Status.on_hold, 10, false, 10, Status.On_hold)]
    [TestCase(5, null, 5, KitsuUpdate.Status.current, 5, false, 5, Status.Watching)]
    [TestCase(5, true, null, KitsuUpdate.Status.planned, 5, true, 0, Status.Plan_to_watch)]
    public async Task KitsuGetConvertedUserListTest(int progress, bool reconsuming, int reconsumeCount, KitsuUpdate.Status status, int expectedProgress, bool expectedReconsuming, int expectedReconsumeCount, Status expectedStatus) {
        IHttpClientFactory httpClientFactory = null;
        ILoggerFactory loggerFactory = new NullLoggerFactory();
        Mock<IServerApplicationHost> serverApplicationHost = new Mock<IServerApplicationHost>();
        Mock<IHttpContextAccessor> httpContextAccessor = new Mock<IHttpContextAccessor>();
        MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());
        Mock<IAsyncDelayer> mockDelayer = new Mock<IAsyncDelayer>();
        int animeId = 1;
        Helpers.MockHttpCalls(new List<Helpers.HttpCall> {
            new () {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith("library-entries"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new KitsuUpdate.KitsuLibraryEntryListRoot {
                    Data = new List<KitsuUpdate.KitsuLibraryEntry> {
                        new () {
                            Id = animeId,
                            Attributes = new KitsuUpdate.Attributes {
                                Progress = progress,
                                Reconsuming = reconsuming,
                                ReconsumeCount = reconsumeCount,
                                Status = status
                            }
                        }
                    }
                })
            }
        }, ref httpClientFactory);
        
        KitsuApiCalls kitsuApiCalls = new KitsuApiCalls(httpClientFactory, loggerFactory, serverApplicationHost.Object, httpContextAccessor.Object, memoryCache, mockDelayer.Object, new UserConfig {
            UserApiAuth = new [] {
                new UserApiAuth {
                    AccessToken = "accessToken",
                    Name = ApiName.Kitsu,
                    RefreshToken = "refreshToken"
                }
            },
            KeyPairs = new List<KeyPairs> { new()  { Key = "KitsuUserId", Value = "1" }}
        });
        
        ApiCallHelpers apiCallHelpers = new ApiCallHelpers(kitsuApiCalls: kitsuApiCalls);
        MyListStatus convertedResult = await apiCallHelpers.GetConvertedKitsuUserList(animeId);
        
        Assert.IsTrue(convertedResult.NumEpisodesWatched == expectedProgress);
        Assert.IsTrue(convertedResult.IsRewatching == expectedReconsuming);
        Assert.IsTrue(convertedResult.RewatchCount == expectedReconsumeCount);
        Assert.IsTrue(convertedResult.Status == expectedStatus);
    }
    
    private ShikimoriAnime GetShikimoriAnime(bool createRelations) {
        Random random = new Random();

        List<ShikimoriRelated> relatedAnime = new List<ShikimoriRelated>();
        if (createRelations) {
            for (int i = 0; i < random.Next(0, 5); i++) {
                relatedAnime.Add(new ShikimoriRelated {
                    Anime = GetShikimoriAnime(false),
                    Relation = ((ShikimoriRelation)random.Next(0, 4)).ToString(),
                });
            }
        }

        return new ShikimoriAnime {
            Id = random.Next(1, 100).ToString(),
            MalId = random.Next(1, 100).ToString(),
            Episodes = random.Next(1, 100),
            IsCensored = random.Next(0, 1) == 0,
            UserRate = new ShikimoriUserRate {
                Status = (ShikimoriUserRate.StatusEnum)random.Next(0, 6),
                Episodes = random.Next(0, 100),
                Rewatches = random.Next(0, 10),
            },
            Related = relatedAnime,
            Name = "Title",
            Synonyms = new List<string> { "Synonym1", "Synonym2" },
            English = "Title",
            Russian = "Title",
            Japanese = "Title"
        };
    }

    private AniListSearch.Media GetAniListMedia(bool createRelations) {
        Random random = new Random();
        AniListSearch.MediaConnection mediaConnection = new AniListSearch.MediaConnection();
        if (createRelations) {
            mediaConnection.Media = new List<AniListSearch.MediaEdge>();
            for (int i = 0; i < random.Next(0, 5); i++) {
                mediaConnection.Media.Add(new AniListSearch.MediaEdge {
                    Media = GetAniListMedia(false),
                    RelationType = (AniListSearch.MediaRelation)random.Next(0, 12)
                });
            }
        }

        return new AniListSearch.Media {
            Id = random.Next(1, 100),
            Episodes = random.Next(1, 100),
            IsAdult = random.Next(0, 1) == 0,
            MediaListEntry = new AniListSearch.MediaListEntry {
                CompletedAt = new AniListSearch.FuzzyDate {
                    Day = random.Next(1, 31),
                    Month = random.Next(1, 12),
                    Year = random.Next(1970, 2024)
                },
                MediaListStatus = (AniListSearch.MediaListStatus)random.Next(0, 5),
                Progress = random.Next(0, 100),
                RepeatCount = random.Next(0, 10),
                StartedAt = new AniListSearch.FuzzyDate {
                    Day = random.Next(1, 31),
                    Month = random.Next(1, 12),
                    Year = random.Next(1970, 2024)
                }
            },
            Relations = mediaConnection,
            Title = new AniListSearch.Title { Romaji = "Title", English = "Title", Native = "Title", UserPreferred = "Title" },
            Synonyms = new List<string> { "Synonym1", "Synonym2" },
            Status = (AniListSearch.AiringStatus)random.Next(0, 4),
            SiteUrl = "https://example.com"
        };
    }

    private KitsuSearch.KitsuAnime GetKitsuAnime(bool createRelations) {
        Random random = new Random();
        KitsuSearch.MediaRelationships mediaRelationships = new KitsuSearch.MediaRelationships();
        if (createRelations) {
            mediaRelationships.Data = new List<KitsuSearch.KitsuAnime>();
            for (int i = 0; i < random.Next(0, 5); i++) {
                KitsuSearch.KitsuAnime anime = GetKitsuAnime(false);
                anime.RelationType = (KitsuMediaRelationship.RelationType?)random.Next(0, 12);
                mediaRelationships.Data.Add(anime);
            }
        }

        KitsuSearch.Relationships relationships = new KitsuSearch.Relationships {
            MediaRelationships = mediaRelationships
        };
        return new KitsuSearch.KitsuAnime {
            Id = GetUniqueRandomNumber(random, 1, 100),
            Attributes = new KitsuSearch.Attributes {
                EpisodeCount = random.Next(1, 200),
                CanonicalTitle = "Title",
                Titles = new KitsuSearch.Titles { English = "Title", EnJp = "Title", Japanese = "Title" },
                AbbreviatedTitles = new List<string> { "Synonym1", "Synonym2" },
            },
            Relationships = relationships,
            RelatedAnime = mediaRelationships.Data
        };
    }

    private AnnictSearch.AnnictAnime GetAnnictAnime() {
        Random random = new Random();

        return new AnnictSearch.AnnictAnime {
            Id = random.Next(1, 100).ToString(),
            TitleEn = "Title",
            MalAnimeId = random.Next(1, 10000).ToString(),
            ViewerStatusState = (AnnictSearch.AnnictMediaStatus)random.Next(0, 6),
            NumberOfEpisodes = random.Next(1, 100),
        };
    }

    private int GetUniqueRandomNumber(Random random, int start, int end) {
        while (true) {
            int randomInt = random.Next(start, end);

            if (!uniqueIdList.Contains(randomInt)) {
                return randomInt;
            }
        }
    }
}