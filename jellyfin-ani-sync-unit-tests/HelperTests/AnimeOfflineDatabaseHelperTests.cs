using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using jellyfin_ani_sync.Helpers;
using NUnit.Framework;

namespace jellyfin_ani_sync_unit_tests.HelperTests;

public class AnimeOfflineDatabaseHelperTests {
    private HttpClient _httpClient;
    
    private void Setup(List<Helpers.HttpCall> httpCalls) {
        Helpers.MockHttpCalls(httpCalls, ref _httpClient);
    }

    [Test]
    public async Task DeserializeMetadataCall() {
        Setup(new List<Helpers.HttpCall> {
            new() {
                RequestMethod = HttpMethod.Get,
                RequestUrlMatch = url => url.EndsWith("ids"),
                ResponseCode = HttpStatusCode.OK,
                ResponseContent = JsonSerializer.Serialize(new AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse {
                    AniDb = 1,
                    Anilist = 1,
                    Kitsu = 1,
                    MyAnimeList = 1
                })
            }
        });

        AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse? result = await AnimeOfflineDatabaseHelpers.GetProviderIdsFromMetadataProvider(_httpClient, 1, AnimeOfflineDatabaseHelpers.Source.Myanimelist);
        Assert.IsTrue(result != null);
        Assert.IsTrue(result.AniDb == 1);
        Assert.IsTrue(result.Anilist == 1);
        Assert.IsTrue(result.Kitsu == 1);
        Assert.IsTrue(result.MyAnimeList == 1);
    }
}