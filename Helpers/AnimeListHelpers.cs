using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Serialization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Movies;
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
        public static async Task<(int? aniDbId, int? episodeOffset)> GetAniDbId(ILogger logger, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, IApplicationPaths applicationPaths, Video video, int episodeNumber, int seasonNumber, AnimeListXml animeListXml) {
            int aniDbId;
            if (animeListXml == null) return (null, null);
            Dictionary<string, string> providers;
            if (video is Episode) {
                //Search for Anidb id at season level
                providers = (video as Episode).Season.ProviderIds.ContainsKey("Anidb") ? (video as Episode).Season.ProviderIds : (video as Episode).Series.ProviderIds;
            } else if (video is Movie) {
                providers = (video as Movie).ProviderIds;
            } else {
                return (null, null);
            }

            if (providers.ContainsKey("Anidb")) {
                logger.LogInformation("(Anidb) Anime already has AniDb ID; no need to look it up");
                if (!int.TryParse(providers["Anidb"], out aniDbId)) return (null, null);
                var foundAnime = animeListXml.Anime.Where(anime => int.TryParse(anime.Anidbid, out int xmlAniDbId) &&
                                                                   xmlAniDbId == aniDbId &&
                                                                   (
                                                                       (video as Episode).Season.ProviderIds.ContainsKey("Anidb") ||
                                                                       (int.TryParse(anime.Defaulttvdbseason, out int xmlSeason) &&
                                                                        xmlSeason == seasonNumber ||
                                                                        anime.Defaulttvdbseason == "a")
                                                                   )
                ).ToList();
                switch (foundAnime.Count()) {
                    case 1:
                        var related = animeListXml.Anime.Where(anime => anime.Tvdbid == foundAnime.First().Tvdbid).ToList();
                        if (video is Episode episode && episode.Series.Children.OfType<Season>().Count() > 1 && related.Count > 1) {
                            // contains more than 1 season, need to do a lookup
                            logger.LogInformation($"(Anidb) Anime {episode.Series.Name} found in anime XML file");
                            logger.LogInformation($"(Anidb) Looking up anime {episode.Series.Name} in the anime XML file by absolute episode number...");
                            var aniDb = GetAniDbByEpisodeOffset(logger, GetAbsoluteEpisodeNumber(episode), seasonNumber, related);
                            if (aniDb != null) {
                                logger.LogInformation($"(Anidb) Anime {episode.Series.Name} found in anime XML file, detected AniDB ID {aniDb}");
                                return (aniDb.Value, null);
                            } else {
                                logger.LogInformation($"(Anidb) Anime {episode.Series.Name} could not found in anime XML file; falling back to other metadata providers if available...");
                            }
                        } else {
                            if (video is Episode episodeWithMultipleSeasons && episodeWithMultipleSeasons.Season.IndexNumber > 1) {
                                // user doesnt have full series; have to do season lookup
                                logger.LogInformation($"(Tvdb) Anime {episodeWithMultipleSeasons.Series.Name} found in anime XML file");
                                var aniDb = SeasonLookup(logger, seasonNumber, related);
                                return aniDb != null ? (aniDb, null) : (null, null);
                            } else {
                                logger.LogInformation($"(Tvdb) Anime {video.Name} found in anime XML file");
                                // is movie / only has one season / no related; just return the only result
                                return int.TryParse(related.First().Anidbid, out aniDbId) ? (aniDbId, null) : (null, null);
                            }
                            logger.LogInformation($"(Anidb) Anime {(video is Episode episodeWithoutSeason ? episodeWithoutSeason.Name : video.Name)} found in anime XML file");
                            // is movie / only has one season / no related; just return the only result
                            return int.TryParse(foundAnime.First().Anidbid, out aniDbId) ? (aniDbId, null) : (null, null);
                        }

                        break;
                    case > 1:
                        // here
                        logger.LogWarning("(Anidb) More than one result found; possibly an issue with the XML. Falling back to other metadata providers if available...");
                        break;
                    case 0:
                        logger.LogWarning("(Anidb) Anime not found in anime list XML; falling back to other metadata providers if available...");
                        break;
                }
            }

            //Search for tvdb id at series level
            if (video is Episode) {
                providers = (video as Episode).Series.ProviderIds;
            }

            if (providers.ContainsKey("Tvdb")) {
                int tvDbId;
                if (!int.TryParse(providers["Tvdb"], out tvDbId)) return (null, null);
                var related = animeListXml.Anime.Where(anime => int.TryParse(anime.Tvdbid, out int xmlTvDbId) && xmlTvDbId == tvDbId).ToList();

                if (!related.Any()) {
                    logger.LogWarning("(Tvdb) Anime not found in anime list XML; querying the appropriate providers API");
                    return (null, null);
                }

                logger.LogInformation("(Tvdb) Anime reference found in anime list XML");
                if (related.Count() == 1) return int.TryParse(related.First().Anidbid, out aniDbId) ? (aniDbId, null) : (null, null);

                if (video is Episode episode && episode.Series.Children.OfType<Season>().Count() > 1) {
                    var aniDb = GetAniDbByEpisodeOffset(logger, GetAbsoluteEpisodeNumber(episode), seasonNumber, related);
                    if (aniDb != null) {
                        logger.LogInformation($"(Tvdb) Anime {episode.Series.Name} found in anime XML file, detected AniDB ID {aniDb}");
                        return (aniDb.Value, null);
                    } else {
                        logger.LogInformation($"(Tvdb) Anime {episode.Series.Name} could not found in anime XML file; falling back to other metadata providers if available...");
                    }
                } else {
                    if (video is Episode episodeWithMultipleSeasons && episodeWithMultipleSeasons.Season.IndexNumber > 1) {
                        // user doesnt have full series; have to do season lookup
                        logger.LogInformation($"(Tvdb) Anime {episodeWithMultipleSeasons.Name} found in anime XML file");
                        var aniDb = SeasonLookup(logger, seasonNumber, related);
                        return aniDb != null ? (aniDb, null) : (null, null);
                    } else {
                        logger.LogInformation($"(Tvdb) Anime {video.Name} found in anime XML file");
                        // is movie / only has one season / no related; just return the only result
                        return int.TryParse(related.First().Anidbid, out aniDbId) ? (aniDbId, null) : (null, null);
                    }
                }
            }

            return (null, null);
        }

        private static int? GetAniDbByEpisodeOffset(ILogger logger, int? absoluteEpisodeNumber, int seasonNumber, List<AnimeListAnime> related) {
            if (absoluteEpisodeNumber != null) {
                var foundMapping = related.FirstOrDefault(animeListAnime => animeListAnime.MappingList?.Mapping?.FirstOrDefault(mapping => mapping.Start < absoluteEpisodeNumber && mapping.End > absoluteEpisodeNumber) != null)?.Anidbid;
                if (foundMapping != null) {
                    return int.Parse(foundMapping);
                } else {
                    logger.LogWarning("(AniDb) Could not lookup using absolute episode number (reason: no mappings found)");
                    return SeasonLookup(logger, seasonNumber, related);
                }
            } else {
                logger.LogWarning("(AniDb) Could not lookup using absolute episode number (reason: absolute episode number is null)");
                return SeasonLookup(logger, seasonNumber, related);
            }
        }

        private static int? SeasonLookup(ILogger logger, int seasonNumber, List<AnimeListAnime> related) {
            logger.LogInformation("Looking up AniDB by season offset");
            var foundMapping = related.Where(animeListAnime => animeListAnime.Defaulttvdbseason == "a").FirstOrDefault(animeListAnime => animeListAnime.MappingList.Mapping.FirstOrDefault(mapping => mapping.Tvdbseason == seasonNumber) != null)?.Anidbid ??
                               related.FirstOrDefault(animeListAnime => animeListAnime.Defaulttvdbseason == seasonNumber.ToString())?.Anidbid;
            return foundMapping != null ? int.Parse(foundMapping) : null;
        }

        private static int? GetAbsoluteEpisodeNumber(Episode episode) {
            var previousSeasons = episode.Series.Children.OfType<Season>().Where(item => item.IndexNumber < episode.Season.IndexNumber).ToList();
            int previousSeasonIndexNumber = -1;
            foreach (int indexNumber in previousSeasons.Where(item => item.IndexNumber != null).Select(item => item.IndexNumber).OrderBy(item => item.Value)) {
                if (previousSeasonIndexNumber == -1) {
                    previousSeasonIndexNumber = indexNumber;
                } else {
                    if (previousSeasonIndexNumber != indexNumber - 1) {
                        // series does not contain all seasons, cannot get absolute episode number
                        return null;
                    }

                    previousSeasonIndexNumber = indexNumber;
                }
            }

            var previousSeasonsEpisodeCount = previousSeasons.SelectMany(item => item.Children.OfType<Episode>()).Count();
            // this is presuming the user has all episodes
            return previousSeasonsEpisodeCount + episode.IndexNumber;
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
        public static async Task<AnimeListAnime> GetAniDbSeason(ILogger logger, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, IApplicationPaths applicationPaths, int aniDbId, AnimeListXml animeListXml) {
            if (animeListXml == null) return null;

            return animeListXml.Anime.FirstOrDefault(anime => int.TryParse(anime.Anidbid, out int xmlAniDbId) && xmlAniDbId == aniDbId);
        }


        public static async Task<IEnumerable<AnimeListAnime>> ListAllSeasonOfAniDbSeries(ILogger logger, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, IApplicationPaths applicationPaths, int aniDbId, AnimeListXml animeListXml) {
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
        public static async Task<AnimeListXml> GetAnimeListFileContents(ILogger logger, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, IApplicationPaths applicationPaths) {
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

            [XmlElement(ElementName = "mapping-list")]
            public MappingList MappingList { get; set; }

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

        [XmlRoot(ElementName = "mapping-list")]
        public class MappingList {
            [XmlElement(ElementName = "mapping")] public List<Mapping> Mapping { get; set; }
        }

        [XmlRoot(ElementName = "mapping")]
        public class Mapping {
            [XmlAttribute(AttributeName = "anidbseason")]
            public int Anidbseason { get; set; }

            [XmlAttribute(AttributeName = "tvdbseason")]
            public int Tvdbseason { get; set; }

            [XmlText] public string Text { get; set; }

            [XmlAttribute(AttributeName = "start")]
            public int Start { get; set; }

            [XmlAttribute(AttributeName = "end")] public int End { get; set; }

            [XmlAttribute(AttributeName = "offset")]
            public int Offset { get; set; }
        }

        [XmlRoot(ElementName = "anime-list")]
        public class AnimeListXml {
            [XmlElement(ElementName = "anime")] public List<AnimeListAnime> Anime { get; set; }
        }
    }
}