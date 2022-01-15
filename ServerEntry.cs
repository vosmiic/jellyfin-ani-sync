using System;
using System.Collections.Generic;
using System.Globalization;
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
                    _logger.LogInformation($"Found matching anime: {anime.Title}");
                    UserAnimeListData detectedAnime = await GetAnime(anime.Id, Status.Watching);
                    if (detectedAnime == null) {
                        if (Plugin.Instance.PluginConfiguration.PlanToWatchOnly) {
                            detectedAnime = await GetAnime(anime.Id, Status.Plan_to_watch);
                            if (detectedAnime != null) {
                                _logger.LogInformation("Anime found on plan to watch list");
                                await UpdateAnimeWatchProgress(detectedAnime, episode.IndexNumber);
                            } else {
                                _logger.LogWarning("Anime found, but not on Plan To Watch list so ignoring");
                            }
                        } else {
                            // search globally for the anime and add it to the users watching list
                        }
                    } else {
                        _logger.LogInformation("Anime found on watching list");
                        await UpdateAnimeWatchProgress(detectedAnime, episode.IndexNumber);
                        break;
                    }
                } else {
                    _logger.LogWarning("Anime not found");
                }
            }
        }
    }

    private async Task<UserAnimeListData> GetAnime(int animeId, Status status) {
        List<UserAnimeListData> watchingAnime = await _malApiCalls.GetUserAnimeList(status, idSearch: animeId);

        foreach (var animeListData in watchingAnime) {
            if (animeListData.Anime.Id == animeId) {
                return animeListData;
            }
        }

        return null;
    }

    private async Task UpdateAnimeWatchProgress(UserAnimeListData detectedAnime, int? indexNumber) {
        if (indexNumber != null && detectedAnime.ListStatus.NumEpisodesWatched < indexNumber.Value) {
            await _malApiCalls.UpdateAnimeStatus(detectedAnime.Anime.Id, indexNumber.Value);
            _logger.LogInformation($"Updated {detectedAnime.Anime.Title} progress to {indexNumber.Value}");
        } else {
            _logger.LogInformation("MAL reports episode already watched; not updating");
        }
    }

    public void Dispose() {
        _sessionManager.PlaybackStopped -= PlaybackStopped;
        GC.SuppressFinalize(this);
    }
}