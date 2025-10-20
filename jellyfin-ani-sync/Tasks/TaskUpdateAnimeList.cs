using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync {
    public class TaskUpdateAnimeList : IScheduledTask {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IApplicationPaths _applicationPaths;

        public string Name => "AniSync Update Anime List";
        public string Key => "UpdateAnimeList";
        public string Description => "Update the anime list to the latest version.";
        public string Category => "AniSync";

        public TaskUpdateAnimeList(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory,
            IApplicationPaths applicationPaths) {
            _loggerFactory = loggerFactory;
            _httpClientFactory = httpClientFactory;
            _applicationPaths = applicationPaths;
        }


        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress) {
            return Task.Run(async () => {
                UpdateAnimeList updateAnimeList = new UpdateAnimeList(_httpClientFactory, _loggerFactory, _applicationPaths);
                await updateAnimeList.Update();
            }, cancellationToken);
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() {
            var trigger = new TaskTriggerInfo {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromDays(1).Ticks
            };

            return new[] { trigger };
        }

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken) {
            await Task.Run(async () => {
                UpdateAnimeList updateAnimeList = new UpdateAnimeList(_httpClientFactory, _loggerFactory, _applicationPaths);
                await updateAnimeList.Update();
            }, cancellationToken);
        }
    }
}