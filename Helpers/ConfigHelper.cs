#nullable enable
using System.Linq;
using jellyfin_ani_sync.Configuration;
using Microsoft.Extensions.Logging;

namespace jellyfin_ani_sync.Helpers;

public class ConfigHelper {
    public static string? GetShikimoriAppName(ILogger logger) {
        string? shikimoriAppName = Plugin.Instance?.PluginConfiguration.shikimoriAppName;

        if (string.IsNullOrEmpty(shikimoriAppName)) {
            logger.LogError("No Shikimori app name in config. Please provide a Shikimori app name on the config page.");
        }

        return shikimoriAppName;
    }

    public static string? GetSimklClientId(ILogger logger) {
        string? simklClientId = Plugin.Instance?.PluginConfiguration.ProviderApiAuth?.FirstOrDefault(item => item.Name == ApiName.Simkl)?.ClientId;

        if (string.IsNullOrEmpty(simklClientId)) {
            logger.LogError("No Simkl client ID in config. Please provide a client ID for Simkl on the config page.");
        }

        return simklClientId;
    }

    public static bool GetSimklUpdateAll() {
        bool? simklUpdateAll = Plugin.Instance?.PluginConfiguration.simklUpdateAll;

        if (simklUpdateAll == null) {
            return false;
        }

        return true;
    }
}