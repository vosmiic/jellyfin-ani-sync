#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;
using jellyfin_ani_sync.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;


namespace jellyfin_ani_sync {
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages {
        public Plugin(IApplicationPaths applicationPaths, IServerConfigurationManager serverConfigurationManager, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer) {
            Instance = this;
            if (PluginConfiguration.enableUserPages)
                CheckPluginPages(applicationPaths, serverConfigurationManager);
        }

        public override string Name => "Ani-Sync";
        public override Guid Id => Guid.Parse("c78f11cf-93e6-4423-8c42-d2c255b70e47");
        public override string Description => "Synchronize anime watch status between Jellyfin and anime tracking sites.";
        public PluginConfiguration PluginConfiguration => Configuration;
        public static Plugin? Instance { get; private set; }

        public void CheckPluginPages(IApplicationPaths applicationPaths, IServerConfigurationManager serverConfigurationManager)
        {
            int pluginPageConfigVersion = 1;
            string pluginPagesConfig = Path.Combine(applicationPaths.PluginConfigurationsPath, "Jellyfin.Plugin.PluginPages", "config.json");
        
            JObject config = new JObject();
            if (!File.Exists(pluginPagesConfig))
            {
                FileInfo info = new FileInfo(pluginPagesConfig);
                info.Directory?.Create();
            }
            else
            {
                config = JObject.Parse(File.ReadAllText(pluginPagesConfig));
            }

            if (!config.ContainsKey("pages"))
            {
                config.Add("pages", new JArray());
            }

            JObject? hssPageConfig = config.Value<JArray>("pages")!.FirstOrDefault(x =>
                x.Value<string>("Id") == typeof(Plugin).Namespace) as JObject;

            if (hssPageConfig != null)
            {
                if ((hssPageConfig.Value<int?>("Version") ?? 0) < pluginPageConfigVersion)
                {
                    config.Value<JArray>("pages")!.Remove(hssPageConfig);
                }
            }
            
            if (!config.Value<JArray>("pages")!.Any(x => x.Value<string>("Id") == typeof(Plugin).Namespace))
            {
                Assembly? pluginPagesAssembly = AssemblyLoadContext.All.SelectMany(x => x.Assemblies).FirstOrDefault(x => x.FullName?.Contains("Jellyfin.Plugin.PluginPages") ?? false);
                
                Version earliestVersionWithSubUrls = new Version("2.4.1.0");
                bool supportsSubUrls = pluginPagesAssembly != null && pluginPagesAssembly.GetName().Version >= earliestVersionWithSubUrls;
                
                string rootUrl = serverConfigurationManager.GetNetworkConfiguration().BaseUrl.TrimStart('/').Trim();
                if (!string.IsNullOrEmpty(rootUrl))
                {
                    rootUrl = $"/{rootUrl}";
                }
                
                config.Value<JArray>("pages")!.Add(new JObject
                {
                    { "Id", typeof(Plugin).Namespace },
                    { "Url", $"{(supportsSubUrls ? "" : rootUrl)}/AniSync/settings" },
                    { "DisplayText", "AniSync Configuration" },
                    { "Icon", "build" },
                    { "Version", pluginPageConfigVersion }
                });
        
                File.WriteAllText(pluginPagesConfig, config.ToString(Formatting.Indented));
            }
        }

        public IEnumerable<PluginPageInfo> GetPages() {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.ConfigPage.html"
                },
                new PluginPageInfo {
                    Name = "AniSync_ConfigPageJs",
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.ConfigPageJs.js"
                },
                new PluginPageInfo {
                    Name = "AniSync_CommonJs",
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.CommonJs.js"
                },
                new PluginPageInfo {
                    Name = "AniSync_ManualSync",
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.ManualSync.html"
                },
                new PluginPageInfo {
                    Name = "AniSync_ManualSyncJs",
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.ManualSyncJs.js"
                }
            };
        }

        /// <summary>
        /// Get the views that the plugin serves.
        /// </summary>
        /// <returns>Array of <see cref="PluginPageInfo"/>.</returns>
        public IEnumerable<PluginPageInfo> GetViews()
        {
            return new[]
            {
                new PluginPageInfo {
                    Name = "settings",
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.ConfigPageUser.html"
                }
            };
        }
    }
}