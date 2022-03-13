using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using jellyfin_ani_sync.Api;
using jellyfin_ani_sync.Api.Anilist;
using jellyfin_ani_sync.Models;

namespace jellyfin_ani_sync.Helpers {
    public class ApiCallHelpers {
        private MalApiCalls _malApiCalls;
        private AniListApiCalls _aniListApiCalls;

        /// <summary>
        /// This class attempts to combine the different APIs into a single form.
        /// </summary>
        /// <param name="malApiCalls"></param>
        /// <param name="aniListApiCalls"></param>
        public ApiCallHelpers(MalApiCalls malApiCalls = null, AniListApiCalls aniListApiCalls = null) {
            _malApiCalls = malApiCalls;
            _aniListApiCalls = aniListApiCalls;
        }

        public async Task<List<Anime>> SearchAnime(string query) {
            if (_malApiCalls != null) {
                return await _malApiCalls.SearchAnime(query, new[] { "id", "title", "alternative_titles" });
            }

            if (_aniListApiCalls != null) {
                List<AniListSearch.Media> animeList = await _aniListApiCalls.SearchAnime(query);
                List<Anime> convertedList = new List<Anime>();
                foreach (AniListSearch.Media media in animeList) {
                    convertedList.Add(new Anime {
                        Id = media.Id,
                        Title = media.Title.English,
                        AlternativeTitles = new AlternativeTitles {
                            En = media.Title.English,
                            Ja = media.Title.Native,
                            Synonyms = new List<string> {
                                { media.Title.Romaji },
                                { media.Title.UserPreferred }
                            }
                        }
                    });
                }

                return convertedList;
            }

            return null;
        }

        public async Task<Anime> GetAnime(int id) {
            if (_malApiCalls != null) {
                return await _malApiCalls.GetAnime(id, new[] { "title", "related_anime", "my_list_status", "num_episodes" });
            }

            if (_aniListApiCalls != null) {
                AniListSearch.Media anime = await _aniListApiCalls.GetAnime(id);
                Anime convertedAnime = ClassConversions.ConvertAnime(anime);

                if (anime.MediaListEntry != null) {
                    convertedAnime.MyListStatus.RewatchCount = anime.MediaListEntry.RepeatCount;

                    switch (anime.MediaListEntry.MediaListStatus) {
                        case AniListSearch.MediaListStatus.Current:
                            convertedAnime.MyListStatus.Status = Status.Plan_to_watch;
                            break;
                        case AniListSearch.MediaListStatus.Completed:
                        case AniListSearch.MediaListStatus.Repeating:
                            convertedAnime.MyListStatus.Status = Status.Completed;
                            break;
                        case AniListSearch.MediaListStatus.Dropped:
                            convertedAnime.MyListStatus.Status = Status.Dropped;
                            break;
                        case AniListSearch.MediaListStatus.Paused:
                            convertedAnime.MyListStatus.Status = Status.On_hold;
                            break;
                        case AniListSearch.MediaListStatus.Planning:
                            convertedAnime.MyListStatus.Status = Status.Plan_to_watch;
                            break;
                    }
                }

                convertedAnime.RelatedAnime = new List<RelatedAnime>();
                foreach (AniListSearch.MediaEdge relation in anime.Relations.Media) {
                    RelatedAnime relatedAnime = new RelatedAnime {
                        Anime = ClassConversions.ConvertAnime(relation.Media)
                    };

                    switch (relation.RelationType) {
                        case AniListSearch.MediaRelation.Sequel:
                            relatedAnime.RelationType = RelationType.Sequel;
                            break;
                        case AniListSearch.MediaRelation.Side_Story:
                            relatedAnime.RelationType = RelationType.Side_Story;
                            break;
                        case AniListSearch.MediaRelation.Alternative:
                            relatedAnime.RelationType = RelationType.Alternative_Setting;
                            break;
                    }

                    convertedAnime.RelatedAnime.Add(relatedAnime);
                }

                return convertedAnime;
            }

            return null;
        }

        public async Task<UpdateAnimeStatusResponse> UpdateAnime(int animeId, int numberOfWatchedEpisodes, Status status,
            bool? isRewatching = null, int? numberOfTimesRewatched = null, DateTime? startDate = null, DateTime? endDate = null) {
            if (_malApiCalls != null) {
                return await _malApiCalls.UpdateAnimeStatus(animeId, numberOfWatchedEpisodes, status, isRewatching, numberOfTimesRewatched, startDate, endDate);
            }

            if (_aniListApiCalls != null) {
                AniListSearch.MediaListStatus anilistStatus;
                switch (status) {
                    case Status.Watching:
                        anilistStatus = AniListSearch.MediaListStatus.Current;
                        break;
                    case Status.Completed:
                        anilistStatus = isRewatching != null && isRewatching.Value ? AniListSearch.MediaListStatus.Repeating : AniListSearch.MediaListStatus.Completed;
                        break;
                    case Status.On_hold:
                        anilistStatus = AniListSearch.MediaListStatus.Paused;
                        break;
                    case Status.Dropped:
                        anilistStatus = AniListSearch.MediaListStatus.Dropped;
                        break;
                    case Status.Plan_to_watch:
                        anilistStatus = AniListSearch.MediaListStatus.Planning;
                        break;
                    default:
                        anilistStatus = AniListSearch.MediaListStatus.Current;
                        break;
                }

                if (await _aniListApiCalls.UpdateAnime(animeId, anilistStatus, numberOfWatchedEpisodes, numberOfTimesRewatched, startDate, endDate)) {
                    return new UpdateAnimeStatusResponse();
                }
            }

            return null;
        }
    }
}