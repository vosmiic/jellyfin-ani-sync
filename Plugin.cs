#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using jellyfin_ani_sync.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace jellyfin_ani_sync {
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer) {
            Instance = this;
        }

        public override string Name => "Ani-Sync";
        public override Guid Id => Guid.Parse("c78f11cf-93e6-4423-8c42-d2c255b70e47");
        public override string Description => "Synchronize anime watch status between Jellyfin and anime tracking sites.";
        public PluginConfiguration PluginConfiguration => Configuration;
        public static Plugin? Instance { get; private set; }

        public IEnumerable<PluginPageInfo> GetPages() {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.ConfigPage.html", GetType().Namespace)
                }
            };
        }
    }
}