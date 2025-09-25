using jellyfin_ani_sync.Models;
using MediaBrowser.Model.Plugins;

namespace jellyfin_ani_sync.Configuration {
    /// <summary>
    /// Plugin configuration.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration {

        /// <summary>
        /// Custom user configuration details.
        /// </summary>
        public UserConfig[] UserConfig { get; set; }

        /// <summary>
        /// Authentication details of the anime API providers.
        /// </summary>
        public ProviderApiAuth[] ProviderApiAuth { get; set; }

        /// <summary>
        /// Overriden callback URL set if the user is using Jellyfin over the internet. Generally should not be set if the user is on LAN.
        /// </summary>
        public string callbackUrl { get; set; }
        
        /// <summary>
        /// The URL to redirect the user to on successful authentication.
        /// </summary>
        public string callbackRedirectUrl { get; set; }
        
        /// <summary>
        /// Save location of the anime list.
        /// </summary>
        public string animeListSaveLocation { get; set; }
        
        /// <summary>
        /// Whether or not marking an episode/movie as watched sends an update to the provider API.
        /// </summary>
        public bool watchedTickboxUpdatesProvider { get; set; }
        
        /// <summary>
        /// Shikimori app name, which is required on all Shikimori API calls
        /// </summary>
        public string shikimoriAppName { get; set; }
        
        /// <summary>
        /// True to update all simkl series episodes to current point.
        /// </summary>
        public bool simklUpdateAll { get; set; }
        
        /// <summary>
        /// True to update NSFW anime.
        /// </summary>
        public bool updateNsfw { get; set; }

        /// <summary>
        /// How long the memory cache holds onto users authentication details in minutes.
        /// </summary>
        public long authenticationLinkExpireTimeMinutes { get; set; }
    }
}