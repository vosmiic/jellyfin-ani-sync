using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Plugins;

namespace jellyfin_ani_sync.Configuration {
    /// <summary>
    /// The configuration options.
    /// </summary>
    public enum SomeOptions {
        /// <summary>
        /// Option one.
        /// </summary>
        OneOption,

        /// <summary>
        /// Second option.
        /// </summary>
        AnotherOption
    }

    /// <summary>
    /// Plugin configuration.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
        /// </summary>
        public PluginConfiguration() {
            // set default options here
            Options = SomeOptions.AnotherOption;
            PlanToWatchOnly = true;
            RewatchCompleted = false;
            AnInteger = 2;
            AString = "string";
        }

        /// <summary>
        /// Gets or sets a value indicating whether the API should only search for shows on the users plan to watch list.
        /// </summary>
        public bool PlanToWatchOnly { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the plugin should automatically set completed shows as re-watching.
        /// </summary>
        public bool RewatchCompleted { get; set; }

        /// <summary>
        /// Gets or sets an integer setting.
        /// </summary>
        public int AnInteger { get; set; }

        /// <summary>
        /// Gets or sets a string setting.
        /// </summary>
        public string AString { get; set; }

        /// <summary>
        /// Gets or sets an enum option.
        /// </summary>
        public SomeOptions Options { get; set; }

        public ApiAuth[] ApiAuth { get; set; }
        public string[] LibraryToCheck  { get; set; }
        
        public void AddApiAuth(ApiAuth apiAuth)
        {
            if (ApiAuth != null) {
                var apiAuthList = ApiAuth.ToList();
                apiAuthList.Add(apiAuth);
                ApiAuth = apiAuthList.ToArray();
            } else {
                ApiAuth = new[] { apiAuth };
            }
        }
    }
}