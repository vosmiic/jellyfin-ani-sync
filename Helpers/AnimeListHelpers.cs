using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Serialization;
using MediaBrowser.Common.Configuration;
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
        public static async Task<(int? aniDbId, int? episodeOffset)> GetAniDbId(ILogger logger, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, IApplicationPaths applicationPaths, Dictionary<string, string> providers, int episodeNumber, int seasonNumber) {
            int aniDbId;
            AnimeListXml animeListXml = await GetAnimeListFileContents(logger, loggerFactory, httpClientFactory, applicationPaths);
            if (animeListXml == null) return (null, null);
            if (providers.ContainsKey("Anidb")) {
                logger.LogInformation("(Anidb) Anime already has AniDb ID; no need to look it up");
                if (!int.TryParse(providers["Anidb"], out aniDbId)) return (null, null);
                var foundAnime = animeListXml.Anime.Where(anime => int.TryParse(anime.Anidbid, out int xmlAniDbId) && xmlAniDbId == aniDbId &&
                                                                   int.TryParse(anime.Defaulttvdbseason, out int xmlSeason) && xmlSeason == seasonNumber).ToList();
                switch (foundAnime.Count()) {
                    case 1:
                        logger.LogInformation($"(Anidb) Anime {foundAnime[0].Name} found in anime XML file");
                        return int.TryParse(foundAnime.First().Anidbid, out aniDbId) ? (aniDbId, null) : (null, null);
                    case > 1:
                        logger.LogWarning("(Anidb) More than one result found; possibly an issue with the XML. Falling back to other metadata providers if available...");
                        break;
                    case 0:
                        logger.LogWarning("(Anidb) Anime not found in anime list XML; falling back to other metadata providers if available...");
                        break;
                }
            }

            if (providers.ContainsKey("Tvdb")) {
                int tvDbId;
                if (!int.TryParse(providers["Tvdb"], out tvDbId)) return (null, null);
                var foundAnime = animeListXml.Anime.Where(anime => int.TryParse(anime.Tvdbid, out int xmlTvDbId) && xmlTvDbId == tvDbId &&
                                                                   int.TryParse(anime.Defaulttvdbseason, out int xmlSeason) && xmlSeason == seasonNumber).ToList();
                if (!foundAnime.Any()) {
                    logger.LogWarning("(Tvdb) Anime not found in anime list XML; querying the appropriate providers API");
                    return (null, null);
                }

                logger.LogInformation("(Tvdb) Anime reference found in anime list XML");
                if (foundAnime.Count() == 1) return int.TryParse(foundAnime.First().Anidbid, out aniDbId) ? (aniDbId, null) : (null, null);
                for (var i = 0; i < foundAnime.Count; i++) {
                    // xml first seasons episode offset is always null
                    if ((int.TryParse(foundAnime[i].Episodeoffset, out int episodeOffset) && episodeOffset <= episodeNumber) || i == 0) {
                        if (foundAnime.ElementAtOrDefault(i + 1) != null && int.TryParse(foundAnime[i + 1].Episodeoffset, out int nextEpisodeOffset) && nextEpisodeOffset <= episodeNumber) continue;
                        logger.LogInformation($"(Tvdb) Anime {foundAnime[i].Name} found in anime XML file (using Tvdb ID)");
                        return int.TryParse(foundAnime[i].Anidbid, out aniDbId) ? (aniDbId, episodeOffset) : (null, null);
                    }
                }
            }

            return (null, null);
        }

        /// <summary>
        /// Get the season number of an AniDb entry.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="httpClientFactory"></param>
        /// <param name="applicationPaths"></param>
        /// <param name="aniDbId"></param>
        /// <returns>Season.</returns>
        public static async Task<AnimeListAnime> GetAniDbSeason(ILogger logger, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, IApplicationPaths applicationPaths, int aniDbId) {
            AnimeListXml animeListXml = await GetAnimeListFileContents(logger, loggerFactory, httpClientFactory, applicationPaths);
            if (animeListXml == null) return null;

            return animeListXml.Anime.FirstOrDefault(anime => int.TryParse(anime.Anidbid, out int xmlAniDbId) && xmlAniDbId == aniDbId);
        }


        public static async Task<IEnumerable<AnimeListAnime>> ListAllSeasonOfAniDbSeries(ILogger logger, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, IApplicationPaths applicationPaths, int aniDbId) {
            AnimeListXml animeListXml = await GetAnimeListFileContents(logger, loggerFactory, httpClientFactory, applicationPaths);
            if (animeListXml == null) return null;

            AnimeListAnime foundXmlAnime = animeListXml.Anime.FirstOrDefault(anime => int.TryParse(anime.Anidbid, out int xmlAniDbId) && xmlAniDbId == aniDbId);
            if (foundXmlAnime == null) return null;

            return animeListXml.Anime.Where(anime => anime.Tvdbid == foundXmlAnime.Tvdbid);
        }

        /// <summary>
        /// Get the contents of the anime list file.
        /// </summary>
        /// <param name="logger">Logger.</param>
        /// <returns></returns>
        private static async Task<AnimeListXml> GetAnimeListFileContents(ILogger logger, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, IApplicationPaths applicationPaths) {
            UpdateAnimeList updateAnimeList = new UpdateAnimeList(httpClientFactory, loggerFactory, applicationPaths);

            try {
                FileInfo animeListXml = new FileInfo(updateAnimeList.Path);
                if (!animeListXml.Exists) {
                    logger.LogInformation("Anime list XML not found; attempting to download...");
                    if (await updateAnimeList.Update()) {
                        logger.LogInformation("Anime list XML downloaded");
                    }
                }

                using (var stream = File.OpenRead(updateAnimeList.Path)) {
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