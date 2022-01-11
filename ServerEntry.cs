using System;
using System.Collections.Generic;
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

    public void PlaybackStopped(object sender, PlaybackStopEventArgs e) {
        var video = e.Item as Video;
        Episode? episode = video as Episode;
        Anime malAnime;
        if (episode != null) {
            List<Anime> animeList = _malApiCalls.SearchAnime(episode.SeriesName, new[] { "id", "title", "alternative_titles" });
            foreach (var anime in animeList) {
                if (anime.Title.Equals(episode.SeriesName, StringComparison.OrdinalIgnoreCase) || anime.AlternativeTitles.En.Equals(episode.SeriesName, StringComparison.OrdinalIgnoreCase)) {
                    _logger.LogInformation($"Found matching anime: {anime.Title}");
                }
            }
        }
    }

    public void Dispose() {
        _sessionManager.PlaybackStopped -= PlaybackStopped;
        GC.SuppressFinalize(this);
    }
}