using System.Collections.Generic;
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
            TrueFalseSetting = true;
            AnInteger = 2;
            AccessToken = "string";
            AString = "string";
            ApiAuth = new List<ApiAuth> {
                new() {
                    Name = ApiName.Mal,
                    AccessToken = "token",
                    RefreshToken = "token"
                }
            };
        }

        /// <summary>
        /// Gets or sets a value indicating whether some true or false setting is enabled..
        /// </summary>
        public bool TrueFalseSetting { get; set; }

        /// <summary>
        /// Gets or sets an integer setting.
        /// </summary>
        public int AnInteger { get; set; }

        /// <summary>
        /// Gets or sets a string setting.
        /// </summary>
        public string AString { get; set; }

        /// <summary>
        /// Gets or sets the MAL access token.
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// Gets or sets an enum option.
        /// </summary>
        public SomeOptions Options { get; set; }

        public List<ApiAuth> ApiAuth { get; set; }
    }
}