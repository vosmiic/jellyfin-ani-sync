using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync;

public class UpdateAnimeList {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TaskUpdateAnimeList> _logger;

    public UpdateAnimeList(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory) {
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger<TaskUpdateAnimeList>();
    }


    public async Task Update() {
        if (Plugin.Instance.PluginConfiguration.animeListSaveLocation == null) {
            _logger.LogInformation("User has not set anime list save location; skipping");
            return;
        }

        var client = _httpClientFactory.CreateClient(NamedClient.Default);
        var response = await client.GetAsync("https://api.github.com/repos/Anime-Lists/anime-lists/contents/anime-list-full.xml");
        if (response.IsSuccessStatusCode) {
            StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
            string streamText = await streamReader.ReadToEndAsync();

            GitFile formattedResponse = JsonSerializer.Deserialize<GitFile>(streamText);

            if (formattedResponse == null) {
                _logger.LogError("Could not update anime list; anime list could not be parsed");
                return;
            }

            if (await CheckIfFileSizeHasChanged(formattedResponse.Size)) {
                await GetLatestAnimeList(client, formattedResponse.DownloadUrl);
            }
        } else {
            _logger.LogError($"Could not update anime list; {response.StatusCode} from repo");
        }
    }

    private async Task<bool> CheckIfFileSizeHasChanged(int fileSize) {
        FileInfo animeListFile = new FileInfo(Path.Combine(Plugin.Instance.PluginConfiguration.animeListSaveLocation, "anime-list-full.xml"));
        if (animeListFile.Exists) return animeListFile.Length != fileSize;
        return true;
    }

    private async Task GetLatestAnimeList(HttpClient httpClient, string downloadUrl) {
        try {
            await using (var response = await httpClient.GetStreamAsync(downloadUrl)) {
                await using (var fileStream = new FileStream(Path.Combine(Plugin.Instance.PluginConfiguration.animeListSaveLocation, "anime-list-full.xml"), FileMode.OpenOrCreate)) {
                    await response.CopyToAsync(fileStream);
                }
            }
        } catch (Exception e) {
            _logger.LogError($"Could not update anime list; {e.Message}");
        }
    }

    public class GitFile {
        [JsonPropertyName("name")] public string Name { get; set; }

        [JsonPropertyName("size")] public int Size { get; set; }

        [JsonPropertyName("download_url")] public string DownloadUrl { get; set; }
    }
}