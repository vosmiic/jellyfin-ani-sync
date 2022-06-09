using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Helpers {
    public class AnimeListHelpers {
        /// <summary>
        /// Get the AniDb ID from the set of providers provided.
        /// </summary>
        /// <param name="logger">Logger.</param>
        /// <param name="providers">Dictionary of providers.</param>
        /// <param name="episodeNumber">Episode number.</param>
        /// <param name="seasonNumber">Season number.</param>
        /// <returns></returns>
        public static async Task<(int? aniDbId, int? episodeOffset)> GetAniDbId(ILogger logger, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, Dictionary<string, string> providers, int episodeNumber, int seasonNumber) {
            int aniDbId;
            if (providers.ContainsKey("Anidb")) {
                logger.LogInformation($"Anime already has AniDb ID; no need to look it up");
                if (int.TryParse(providers["Anidb"], out aniDbId)) return (aniDbId, null);
            } else if (providers.ContainsKey("Tvdb")) {
                int tvDbId;
                if (!int.TryParse(providers["Tvdb"], out tvDbId)) return (null, null);
                AnimeListXml animeListXml = await GetAnimeListFileContents(logger, loggerFactory, httpClientFactory);
                if (animeListXml == null) return (null, null);
                var foundAnime = animeListXml.Anime.Where(anime => int.TryParse(anime.Tvdbid, out int xmlTvDbId) && xmlTvDbId == tvDbId &&
                                                                   int.TryParse(anime.Defaulttvdbseason, out int xmlSeason) && xmlSeason == seasonNumber).ToList();
                if (!foundAnime.Any()) {
                    logger.LogInformation("Anime not found in anime list XML; querying the appropriate providers API");
                    return (null, null);
                }

                logger.LogInformation("Anime reference found in anime list XML");
                if (foundAnime.Count() == 1) return int.TryParse(foundAnime.First().Anidbid, out aniDbId) ? (aniDbId, null) : (null, null);
                for (var i = 0; i < foundAnime.Count; i++) {
                    // xml first seasons episode offset is always null
                    if ((int.TryParse(foundAnime[i].Episodeoffset, out int episodeOffset) && episodeOffset <= episodeNumber) || i == 0) {
                        if (foundAnime.ElementAtOrDefault(i + 1) != null && int.TryParse(foundAnime[i + 1].Episodeoffset, out int nextEpisodeOffset) && nextEpisodeOffset <= episodeNumber) continue;
                        logger.LogInformation($"Anime {foundAnime[i].Name} found in anime XML file");
                        return int.TryParse(foundAnime[i].Anidbid, out aniDbId) ? (aniDbId, episodeOffset) : (null, null);
                    }
                }
            }

            return (null, null);
        }

        /// <summary>
        /// Get the contents of the anime list file.
        /// </summary>
        /// <param name="logger">Logger.</param>
        /// <returns></returns>
        private static async Task<AnimeListXml> GetAnimeListFileContents(ILogger logger, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory) {
            if (Plugin.Instance.PluginConfiguration.animeListSaveLocation == null) {
                return null;
            }

            try {
                FileInfo animeListXml = new FileInfo(Path.Combine(Plugin.Instance.PluginConfiguration.animeListSaveLocation, "anime-list-full.xml"));
                if (!animeListXml.Exists) {
                    logger.LogInformation("Anime list XML not found; attempting to download...");
                    UpdateAnimeList updateAnimeList = new UpdateAnimeList(httpClientFactory, loggerFactory);
                    if (await updateAnimeList.Update()) {
                        logger.LogInformation("Anime list XML downloaded");
                    }
                }

                using (var stream = File.OpenRead(Path.Combine(Plugin.Instance.PluginConfiguration.animeListSaveLocation, "anime-list-full.xml"))) {
                    var serializer = new XmlSerializer(typeof(AnimeListXml));
                    return (AnimeListXml)serializer.Deserialize(stream);
                }
            } catch (Exception e) {
                logger.LogError($"Could not deserialize anime list XML; {e.Message}. Try forcibly redownloading the XML file");
                return null;
            }
        }

        [XmlRoot(ElementName = "anime")]
        public class AnimeListAnime {
            [XmlElement(ElementName = "name")] public string Name { get; set; }

            [XmlAttribute(AttributeName = "anidbid")]
            public string Anidbid { get; set; }

            [XmlAttribute(AttributeName = "tvdbid")]
            public string Tvdbid { get; set; }

            [XmlAttribute(AttributeName = "defaulttvdbseason")]
            public string Defaulttvdbseason { get; set; }

            [XmlAttribute(AttributeName = "episodeoffset")]
            public string Episodeoffset { get; set; }

            [XmlAttribute(AttributeName = "tmdbid")]
            public string Tmdbid { get; set; }
        }

        [XmlRoot(ElementName = "anime-list")]
        public class AnimeListXml {
            [XmlElement(ElementName = "anime")] public List<AnimeListAnime> Anime { get; set; }
        }
    }
}