using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Diacritics.Extensions;
using jellyfin_ani_sync.Api;
using jellyfin_ani_sync.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;

namespace jellyfin_ani_sync;

public class ServerEntry : IServerEntryPoint {
    private ISessionManager _sessionManager;
    private ILogger<ServerEntry> _logger;
    private MalApiCalls _malApiCalls;

    public ServerEntry(ISessionManager sessionManager, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory) {
        _sessionManager = sessionManager;
        _logger = loggerFactory.CreateLogger<ServerEntry>();
        _malApiCalls = new MalApiCalls(httpClientFactory, loggerFactory);
    }

    public Task RunAsync() {
        _sessionManager.PlaybackStopped += PlaybackStopped;
        return Task.CompletedTask;
    }

    public async void PlaybackStopped(object sender, PlaybackStopEventArgs e) {
        var video = e.Item as Video;
        Episode? episode = video as Episode;
        Anime malAnime;
        if (episode != null) {
            // todo add played to completion after debug
            List<Anime> animeList = await _malApiCalls.SearchAnime(episode.SeriesName, new[] { "id", "title", "alternative_titles" });
            foreach (var anime in animeList) {
                if (String.Compare(anime.Title, episode.SeriesName, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreSymbols) == 0 ||
                    String.Compare(anime.AlternativeTitles.En, episode.SeriesName, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreSymbols) == 0) {
                    _logger.LogInformation($"Found matching series: {anime.Title}");
                    Anime matchingAnime = anime;
                    if (episode.Season.IndexNumber is > 1) {
                        // if this is not the first season, then we need to lookup the related season.
                        // we dont yet support specials, which are considered season 0 in jellyfin.
                        try {
                            matchingAnime = await GetDifferentSeasonAnime(anime.Id, episode.Season.IndexNumber.Value);
                        } catch (NullReferenceException exception) {
                            _logger.LogError(exception.Message);
                            _logger.LogWarning("Could not find next season");
                            return;
                        }

                        _logger.LogInformation(matchingAnime.Title);
                    }

                    UserAnimeListData detectedAnime = await GetAnime(matchingAnime.Id, Status.Watching);
                    if (detectedAnime != null) {
                        _logger.LogInformation($"Series ({matchingAnime.Title}) found on watching list");
                        await UpdateAnimeStatus(detectedAnime, episode.IndexNumber);
                        return;
                    } else {
                        if (Plugin.Instance.PluginConfiguration.PlanToWatchOnly || Plugin.Instance.PluginConfiguration.RewatchCompleted) {
                            // search for plan to watch first, then completed
                            // todo refactor
                            if (Plugin.Instance.PluginConfiguration.PlanToWatchOnly) {
                                detectedAnime = await GetAnime(matchingAnime.Id, Status.Plan_to_watch); // this also needs to be for rewatch completed or below needs reshuffling
                                if (detectedAnime != null) {
                                    _logger.LogInformation($"Series ({matchingAnime.Title}) found on plan to watch list");
                                    await UpdateAnimeStatus(detectedAnime, episode.IndexNumber);
                                    return;
                                } else if (!Plugin.Instance.PluginConfiguration.RewatchCompleted) {
                                    _logger.LogWarning($"Series ({matchingAnime.Title}) found, but not on Plan To Watch list so ignoring");
                                    return;
                                }
                            }

                            if (Plugin.Instance.PluginConfiguration.RewatchCompleted) {
                                // user has already watched the show, and wants the show to be set as re-watching as per config
                                detectedAnime = await GetAnime(matchingAnime.Id, Status.Completed);
                                if (detectedAnime != null) {
                                    await UpdateAnimeStatus(detectedAnime, episode.IndexNumber, true);
                                    return;
                                } else {
                                    _logger.LogWarning($"Series ({matchingAnime.Title}) found, but on Completed list and the user does not want to re-watch a series so ignoring");
                                }
                            }
                        } else {
                            // do a general search for the show
                            detectedAnime = await GetAnime(matchingAnime.Id);
                            if (detectedAnime != null && detectedAnime.ListStatus.Status == Status.Completed && Plugin.Instance.PluginConfiguration.RewatchCompleted) {
                                // user has already watched the show, and wants the show to be set as re-watching as per config
                                await UpdateAnimeStatus(detectedAnime, episode.IndexNumber, true);
                                return;
                            } else {
                                // show is not on the users list at all. must be a new series, add it to the watching list.
                                _logger.LogInformation($"Series ({matchingAnime.Title}) not on user list");
                                await UpdateAnimeStatus(new UserAnimeListData { Anime = anime }, episode.IndexNumber);
                                return;
                            }
                        }
                    }
                }
            }

            _logger.LogWarning("Series not found");
        }
    }

    /// <summary>
    /// Get a single result from a user anime search.
    /// </summary>
    /// <param name="animeId">ID of the anime you want to get.</param>
    /// <param name="status">User status of the show.</param>
    /// <returns>Single anime result.</returns>
    private async Task<UserAnimeListData> GetAnime(int animeId, Status? status = null) {
        List<UserAnimeListData> watchingAnime = await _malApiCalls.GetUserAnimeList(status, idSearch: animeId);

        foreach (var animeListData in watchingAnime) {
            if (animeListData.Anime.Id == animeId) {
                return animeListData;
            }
        }

        return null;
    }

    /// <summary>
    /// Update a users anime status.
    /// </summary>
    /// <param name="detectedAnime">The anime search result to update.</param>
    /// <param name="episodeNumber">The episode number to update the anime to.</param>
    private async Task UpdateAnimeStatus(UserAnimeListData detectedAnime, int? episodeNumber, bool? setRewatching = null) {
        if (episodeNumber != null) {
            if (detectedAnime.ListStatus != null) {
                if (detectedAnime.ListStatus.NumEpisodesWatched < episodeNumber.Value) {
                    if (episodeNumber.Value == detectedAnime.Anime.NumEpisodes) {
                        // user has reached the number of episodes in the show, set as completed
                        var response = await _malApiCalls.UpdateAnimeStatus(detectedAnime.Anime.Id, episodeNumber.Value, Status.Completed);
                        _logger.LogInformation($"Series ({detectedAnime.Anime.Title}) complete, marking show as complete in MAL");
                        if (detectedAnime.ListStatus.IsRewatching) {
                            // also increase number of times re-watched by 1
                            // only way to get the number of times re-watched is by doing the update and capturing the response, and then re-updating :/
                            _logger.LogInformation($"Series ({detectedAnime.Anime.Title}) has also been re-watched, increasing re-watch count by 1");
                            await _malApiCalls.UpdateAnimeStatus(detectedAnime.Anime.Id, episodeNumber.Value, Status.Completed, numberOfTimesRewatched: response.NumTimesRewatched + 1);
                        }
                    } else {
                        if (detectedAnime.ListStatus.IsRewatching) {
                            // MAL likes to mark re-watching shows as completed, instead of watching. I guess technically both are correct
                            _logger.LogInformation($"User is re-watching series ({detectedAnime.Anime.Title}), set as completed but update re-watch progress");
                            await _malApiCalls.UpdateAnimeStatus(detectedAnime.Anime.Id, episodeNumber.Value, Status.Completed);
                        } else {
                            await _malApiCalls.UpdateAnimeStatus(detectedAnime.Anime.Id, episodeNumber.Value, Status.Watching);
                        }
                    }

                    _logger.LogInformation($"Updated series ({detectedAnime.Anime.Title}) progress to {episodeNumber.Value}");
                } else {
                    if (setRewatching != null && setRewatching.Value) {
                        _logger.LogInformation($"Series ({detectedAnime.Anime.Title}) has already been watched, marking show as re-watching");
                        await _malApiCalls.UpdateAnimeStatus(detectedAnime.Anime.Id, episodeNumber.Value, Status.Completed, true);
                    } else {
                        _logger.LogInformation("MAL reports episode already watched; not updating");
                    }
                }
            } else {
                // status is not set, must be a new show
                _logger.LogInformation($"Adding new series ({detectedAnime.Anime.Title}) to user list as watching with a progress of {episodeNumber.Value}");
                await _malApiCalls.UpdateAnimeStatus(detectedAnime.Anime.Id, episodeNumber.Value, Status.Watching);
                // test this
            }
        }
    }

    /// <summary>
    /// Get further anime seasons. Jellyfin uses numbered seasons whereas MAL uses entirely different entities.
    /// </summary>
    /// <param name="animeId"></param>
    /// <param name="seasonNumber"></param>
    /// <returns></returns>
    private async Task<Anime> GetDifferentSeasonAnime(int animeId, int seasonNumber) {
        _logger.LogInformation($"Attempting to get season 1...");
        Anime initialSeason = await _malApiCalls.GetAnime(animeId, new[] { "related_anime" });

        int i = 1;
        while (i != seasonNumber) {
            RelatedAnime initialSeasonRelatedAnime = initialSeason.RelatedAnime.FirstOrDefault(item => item.RelationType == RelationType.Sequel);
            if (initialSeasonRelatedAnime != null) {
                _logger.LogInformation($"Attempting to get season {i + 1}...");
                Anime nextSeason = await _malApiCalls.GetAnime(initialSeasonRelatedAnime.Anime.Id, new[] { "related_anime" });

                initialSeason = nextSeason;
            } else {
                _logger.LogInformation("Could not find any related anime");
                throw new NullReferenceException();
            }

            i++;
        }

        return initialSeason;
    }

    public void Dispose() {
        _sessionManager.PlaybackStopped -= PlaybackStopped;
        GC.SuppressFinalize(this);
    }
}