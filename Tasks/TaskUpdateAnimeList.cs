using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync;

public class TaskUpdateAnimeList : IScheduledTask {
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;

    public string Name => "AniSync Update Anime List";
    public string Key => "UpdateAnimeList";
    public string Description => "Update the anime list to the latest version.";
    public string Category => "AniSync";

    public TaskUpdateAnimeList(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory) {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
    }


    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() {
        var trigger = new TaskTriggerInfo {
            Type = TaskTriggerInfo.TriggerDaily,
            TimeOfDayTicks = 0
        };

        return new[] { trigger };
    }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken) {
        await Task.Run(async () => {
            UpdateAnimeList updateAnimeList = new UpdateAnimeList(_httpClientFactory, _loggerFactory);
            await updateAnimeList.Update();
        }, cancellationToken);
    }
}