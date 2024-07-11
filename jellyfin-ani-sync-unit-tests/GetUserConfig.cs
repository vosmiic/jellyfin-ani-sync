using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Serialization;
using jellyfin_ani_sync.Configuration;
using jellyfin_ani_sync.Models;

namespace jellyfin_ani_sync_unit_tests;

public class GetUserConfig {
    public static UserConfig ManuallyGetUserConfig() {
        XmlSerializer xmlSerializer = new XmlSerializer(typeof(PluginConfiguration));
        using var fileStream = new FileStream(Secrets.configFileLocation, FileMode.Open);
        PluginConfiguration pluginConfiguration = (PluginConfiguration)xmlSerializer.Deserialize(fileStream);

        return pluginConfiguration.UserConfig.FirstOrDefault(item => item.UserId == Secrets.userId);
    }

    public static ProviderApiAuth ManuallyGetProviderAuthConfig(ApiName providerName) {
        XmlSerializer xmlSerializer = new XmlSerializer(typeof(PluginConfiguration));
        using var fileStream = new FileStream(Secrets.configFileLocation, FileMode.Open);
        PluginConfiguration pluginConfiguration = (PluginConfiguration)xmlSerializer.Deserialize(fileStream);

        return pluginConfiguration.ProviderApiAuth.FirstOrDefault(item => item.Name == providerName);
    }
}