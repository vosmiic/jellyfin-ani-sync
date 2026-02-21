using jellyfin_ani_sync.Models;
using jellyfin_ani_sync.Models.Annict;
using jellyfin_ani_sync.Models.Kitsu;
using jellyfin_ani_sync.Models.Mal;
using jellyfin_ani_sync.Models.Shikimori;
using jellyfin_ani_sync.Models.Simkl;

namespace jellyfin_ani_sync.Extensions;

public static class EnumExtensions {
    public static KitsuUpdate.Status ToKitsuStatus(this Status value) {
        return value switch {
            Status.Watching or Status.Rewatching => KitsuUpdate.Status.current,
            Status.Completed => KitsuUpdate.Status.completed,
            Status.On_hold => KitsuUpdate.Status.on_hold,
            Status.Dropped => KitsuUpdate.Status.dropped,
            Status.Plan_to_watch => KitsuUpdate.Status.planned,
            _ => KitsuUpdate.Status.current
        };
    }

    public static AniListSearch.MediaListStatus ToAniListStatus(this Status value) {
        return value switch {
            Status.Watching => AniListSearch.MediaListStatus.Current,
            Status.Completed => AniListSearch.MediaListStatus.Completed,
            Status.Rewatching => AniListSearch.MediaListStatus.Repeating,
            Status.On_hold => AniListSearch.MediaListStatus.Paused,
            Status.Dropped => AniListSearch.MediaListStatus.Dropped,
            Status.Plan_to_watch => AniListSearch.MediaListStatus.Planning,
            _ => AniListSearch.MediaListStatus.Current
        };
    }

    public static AnnictSearch.AnnictMediaStatus ToAnnictStatus(this Status value) {
        return value switch {
            Status.Watching or Status.Rewatching => AnnictSearch.AnnictMediaStatus.Watching,
            Status.Completed => AnnictSearch.AnnictMediaStatus.Watched,
            Status.On_hold => AnnictSearch.AnnictMediaStatus.On_hold,
            Status.Dropped => AnnictSearch.AnnictMediaStatus.Stop_watching,
            Status.Plan_to_watch => AnnictSearch.AnnictMediaStatus.Wanna_watch,
            _ => AnnictSearch.AnnictMediaStatus.No_state
        };
    }

    public static ShikimoriUserRate.StatusEnum ToShikimoriStatus(this Status value) {
        return value switch {
            Status.Watching => ShikimoriUserRate.StatusEnum.watching,
            Status.Completed => ShikimoriUserRate.StatusEnum.completed,
            Status.Rewatching => ShikimoriUserRate.StatusEnum.rewatching,
            Status.On_hold => ShikimoriUserRate.StatusEnum.on_hold,
            Status.Dropped => ShikimoriUserRate.StatusEnum.dropped,
            Status.Plan_to_watch => ShikimoriUserRate.StatusEnum.planned,
            _ => ShikimoriUserRate.StatusEnum.watching
        };
    }

    public static SimklStatus ToSimklStatus(this Status value) {
        return value switch {
            Status.Completed => SimklStatus.completed,
            Status.Dropped => SimklStatus.dropped,
            Status.On_hold => SimklStatus.hold,
            Status.Plan_to_watch => SimklStatus.plantowatch,
            _ => SimklStatus.watching
        };
    }
    
    
    public static Status ToMalStatus(this KitsuUpdate.Status value) {
        return value switch {
            KitsuUpdate.Status.completed => Status.Completed,
            KitsuUpdate.Status.current => Status.Watching,
            KitsuUpdate.Status.dropped => Status.Dropped,
            KitsuUpdate.Status.on_hold => Status.On_hold,
            KitsuUpdate.Status.planned => Status.Plan_to_watch,
            _ => Status.Watching
        };
    }

    public static Status ToMalStatus(this AniListSearch.MediaListStatus value) {
        return value switch {
            AniListSearch.MediaListStatus.Current => Status.Plan_to_watch,
            AniListSearch.MediaListStatus.Completed => Status.Completed,
            AniListSearch.MediaListStatus.Repeating => Status.Rewatching,
            AniListSearch.MediaListStatus.Dropped => Status.Dropped,
            AniListSearch.MediaListStatus.Paused => Status.On_hold,
            AniListSearch.MediaListStatus.Planning => Status.Plan_to_watch,
            _ => Status.Plan_to_watch
        };
    }
    
    
    public static Status ToMalStatus(this AnnictSearch.AnnictMediaStatus value) {
        return value switch {
            AnnictSearch.AnnictMediaStatus.Watching => Status.Watching,
            AnnictSearch.AnnictMediaStatus.Watched => Status.Completed,
            AnnictSearch.AnnictMediaStatus.On_hold => Status.On_hold,
            AnnictSearch.AnnictMediaStatus.Stop_watching => Status.Dropped,
            AnnictSearch.AnnictMediaStatus.Wanna_watch => Status.Plan_to_watch,
            _ => Status.Watching
        };
    }

    public static Status ToMalStatus(this ShikimoriUserRate.StatusEnum value) {
        return value switch {
            ShikimoriUserRate.StatusEnum.completed => Status.Completed,
            ShikimoriUserRate.StatusEnum.watching => Status.Watching,
            ShikimoriUserRate.StatusEnum.rewatching => Status.Rewatching,
            ShikimoriUserRate.StatusEnum.dropped => Status.Dropped,
            ShikimoriUserRate.StatusEnum.on_hold => Status.On_hold,
            ShikimoriUserRate.StatusEnum.planned => Status.Plan_to_watch,
            _ => Status.Watching
        };
    }

    public static Status ToMalStatus(this SimklStatus value) {
        return value switch {
            SimklStatus.watching => Status.Watching,
            SimklStatus.plantowatch => Status.Plan_to_watch,
            SimklStatus.hold => Status.On_hold,
            SimklStatus.completed => Status.Completed,
            SimklStatus.dropped => Status.Dropped,
            _ => Status.Watching
        };
    }

    public static RelationType ToMalRelationType(this KitsuMediaRelationship.RelationType value) {
        return value switch {
            KitsuMediaRelationship.RelationType.sequel => RelationType.Sequel,
            KitsuMediaRelationship.RelationType.side_story or KitsuMediaRelationship.RelationType.full_story or KitsuMediaRelationship.RelationType.parent_story => RelationType.Side_Story,
            KitsuMediaRelationship.RelationType.alternative_setting or KitsuMediaRelationship.RelationType.alternative_version => RelationType.Alternative_Setting,
            KitsuMediaRelationship.RelationType.spinoff or KitsuMediaRelationship.RelationType.adaptation => RelationType.Spin_Off,
            _ => RelationType.Other
        };
    }

}