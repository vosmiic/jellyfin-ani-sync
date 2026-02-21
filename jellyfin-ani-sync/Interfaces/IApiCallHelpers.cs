using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api;
using jellyfin_ani_sync.Models.Mal;

namespace jellyfin_ani_sync.Helpers;

/// <summary>
/// Common interface for anime tracking service API implementations.
/// </summary>
public interface IApiCallHelpers {
    /// <summary>
    /// Search for anime by query string.
    /// </summary>
    /// <param name="query">Search query</param>
    /// <returns>List of matching anime</returns>
    Task<List<Anime>> SearchAnime(string query);
    
    /// <summary>
    /// Get detailed information about a specific anime.
    /// </summary>
    /// <param name="id">Primary anime ID</param>
    /// <param name="alternativeId">Alternative ID (provider-specific)</param>
    /// <param name="getRelated">Whether to fetch related anime</param>
    /// <returns>Anime details or null if not found</returns>
    Task<Anime> GetAnime(int id, string alternativeId = null, bool getRelated = false);

    /// <summary>
    /// Update anime watch status for the authenticated user.
    /// </summary>
    Task<UpdateAnimeStatusResponse> UpdateAnime(int animeId, int numberOfWatchedEpisodes, Status status,
        bool? isRewatching = null, int? numberOfTimesRewatched = null, DateTime? startDate = null, DateTime? endDate = null, string alternativeId = null, AnimeOfflineDatabaseHelpers.OfflineDatabaseResponse ids = null, bool? isShow = null);

    /// <summary>
    /// Get information about the authenticated user.
    /// </summary>
    Task<MalApiCalls.User> GetUser();
    
    /// <summary>
    /// Get the user's anime list filtered by status.
    /// </summary>
    /// <param name="status">Watch status filter</param>
    /// <param name="userId">User ID (null for authenticated user)</param>
    Task<List<Anime>> GetAnimeList(Status status, int? userId = null);
}