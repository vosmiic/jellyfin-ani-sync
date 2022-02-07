using System;
using System.Linq;
using jellyfin_ani_sync.Configuration;

namespace jellyfin_ani_sync.Models;

public class UserConfig {
    public UserConfig() {
        // set default options here
        PlanToWatchOnly = true;
        RewatchCompleted = false;
    }

    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the API should only search for shows on the users plan to watch list.
    /// </summary>
    public bool PlanToWatchOnly { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the plugin should automatically set completed shows as re-watching.
    /// </summary>
    public bool RewatchCompleted { get; set; }

    public ApiAuth[] ApiAuth { get; set; }

    public void AddApiAuth(ApiAuth apiAuth) {
        if (ApiAuth != null) {
            var apiAuthList = ApiAuth.ToList();
            apiAuthList.Add(apiAuth);
            ApiAuth = apiAuthList.ToArray();
        } else {
            ApiAuth = new[] { apiAuth };
        }
    }

    public string[] LibraryToCheck { get; set; }
}