using System.Collections.Generic;
using System.Linq;
using jellyfin_ani_sync.Models;
using MediaBrowser.Model.Plugins;

namespace jellyfin_ani_sync.Configuration {
    /// <summary>
    /// Plugin configuration.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
        /// </summary>
        public UserConfig[] UserConfig { get; set; }

        public void AddUser(UserConfig userConfig) {
            if (userConfig != null) {
                var userConfigList = UserConfig.ToList();
                userConfigList.Add(userConfig);
                UserConfig = userConfigList.ToArray();
            } else {
                UserConfig = new[] { userConfig };
            }
        }
    }
}