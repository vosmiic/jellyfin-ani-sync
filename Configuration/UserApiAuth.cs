using System.ComponentModel.DataAnnotations;

namespace jellyfin_ani_sync.Configuration {
    public enum ApiName {
        [Display(Name = "MyAnimeList")]
        Mal,
        [Display(Name = "AniList")]
        AniList,
        [Display(Name = "Kitsu")]
        Kitsu
    }

    public class UserApiAuth {
        /// <summary>
        /// Name of the API provider.
        /// </summary>
        public ApiName Name { get; set; }
        /// <summary>
        /// Access token of the authenticated instance.
        /// </summary>
        public string AccessToken { get; set; }
        /// <summary>
        /// Refresh token of the authenticated instance.
        /// </summary>
        public string RefreshToken { get; set; }
    }
}