#nullable enable
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
}