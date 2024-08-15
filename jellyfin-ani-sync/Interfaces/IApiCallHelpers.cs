using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api;
using jellyfin_ani_sync.Models.Mal;

namespace jellyfin_ani_sync.Helpers;

public interface IApiCallHelpers {
    Task<List<Anime>> SearchAnime(string query);
    Task<Anime> GetAnime(int id, string alternativeId = null, bool getRelated = false);
    Task<Anime> GetAnime(AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse ids, string title, bool getRelated = false);

    Task<UpdateAnimeStatusResponse> UpdateAnime(int animeId, int numberOfWatchedEpisodes, Status status,
        bool? isRewatching = null, int? numberOfTimesRewatched = null, DateTime? startDate = null, DateTime? endDate = null, string alternativeId = null, AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse ids = null, bool? isShow = null);

    Task<MalApiCalls.User> GetUser();
    Task<List<Anime>> GetAnimeList(Status status, int? userId = null);
}