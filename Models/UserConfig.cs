using System;
using System.Linq;
using jellyfin_ani_sync.Configuration;

namespace jellyfin_ani_sync.Models {
    public class UserConfig {
        public UserConfig() {
            // set default options here
            PlanToWatchOnly = true;
            RewatchCompleted = true;
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

        public UserApiAuth[] UserApiAuth { get; set; }

        public void AddUserApiAuth(UserApiAuth userApiAuth) {
            if (UserApiAuth != null) {
                var apiAuthList = UserApiAuth.ToList();
                apiAuthList.Add(userApiAuth);
                UserApiAuth = apiAuthList.ToArray();
            } else {
                UserApiAuth = new[] { userApiAuth };
            }
        }

        public string[] LibraryToCheck { get; set; }
    }
}