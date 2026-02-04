#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Jellyfin.Extensions;
using jellyfin_ani_sync.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace jellyfin_ani_sync.Helpers {
    public static class UserHelper {
        private static string? GetClaimValue(ClaimsPrincipal user, string name)
            => user.Claims.FirstOrDefault(claim => claim.Type.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;

        private static Guid? GetCurrentUserId(ClaimsPrincipal claimsPrincipal)
        {
            string currentUserString = GetClaimValue(claimsPrincipal, ClaimValues.UserId) ?? string.Empty;
            if (Guid.TryParse(currentUserString, out Guid userId))
            {
                return userId;
            }
            return null;
        }

        /// <summary>
        /// Checks permissions to change user AniSync configuration and returns said user.
        /// </summary>
        /// <param name="userManager">Instance of <see cref="IUserManager"/>.</param>
        /// <param name="claimsPrincipal">The <see cref="ClaimsPrincipal"/> of the currently logged-in user.</param>
        /// <param name="userId">ID of the <see cref="User"/> the logged-in user would like to access. Null to access the logged-in user.</param>
        /// <returns>User if the requester has permissions to interact with the user. Null if the requester cannot interact with the user./></returns>
        public static User? GetUser(IUserManager userManager, ClaimsPrincipal claimsPrincipal, Guid? userId)
        {
            var currentUserId = GetCurrentUserId(claimsPrincipal);

            if (currentUserId.IsNullOrEmpty()) return null;
            
            var currentUser = userManager.GetUserById(currentUserId.Value);

            if (currentUser == null) return null;
            
            if (userId.IsNullOrEmpty()) return currentUser;

            var isAdministrator = currentUser.Permissions.Any(permission => permission is { Kind: PermissionKind.IsAdministrator, Value: true });

            if (!userId.Equals(currentUserId) && !isAdministrator) return null;

            return currentUser;
        }

        /// <summary>
        /// Checks if the user has access to the list of <see cref="CollectionFolder"/>.
        /// </summary>
        /// <returns>True if the user has access to the list of <see cref="CollectionFolder"/>, false if not.</returns>
        public static bool UserHasAccessToLibraries(ILibraryManager libraryManager, HashSet<Guid> libraryIds, User user) {
            Dictionary<Guid, string> librariesUserHasAccessTo = GetLibrariesUserHasAccessTo(libraryManager, user);
            if (!libraryIds.All(id => librariesUserHasAccessTo.ContainsKey(id))) // check if the user has access to all libraries
                return false;
            return true;
        }

        /// <summary>
        /// Retrieves the ID and name of the <see cref="CollectionFolder"/>s the <see cref="User"/> has access to.
        /// </summary>
        /// <param name="libraryManager">Instance of <see cref="ILibraryManager"/>.</param>
        /// <param name="user">The <see cref="User"/> to get the <see cref="CollectionFolder"/>s of.</param>
        /// <returns><see cref="Dictionary{Guid,string}"/> of <see cref="CollectionFolder"/> IDs and names.</returns>
        public static Dictionary<Guid, string> GetLibrariesUserHasAccessTo(ILibraryManager libraryManager, User user) {
            Dictionary<Guid, string> virtualFolderInfos = new Dictionary<Guid, string>();
            bool userHasAccessToAllLibraries = user.Permissions.Any(permission => permission is { Kind: PermissionKind.EnableAllFolders, Value: true });
            if (userHasAccessToAllLibraries) {
                // user has "Enable access to all libraries" ticked in their access config
                return libraryManager.GetVirtualFolders().ToDictionary(virtualFolderInfo => Guid.Parse(virtualFolderInfo.ItemId), virtualFolderInfo => virtualFolderInfo.Name);
            }
            
            IEnumerable<Preference> userLibraryPreferences = user.Preferences.Where(preference => preference.Kind == PreferenceKind.EnabledFolders);
            foreach (Preference userLibraryPreference in userLibraryPreferences) {
                if (userLibraryPreference.Value == String.Empty) {
                    if (!userHasAccessToAllLibraries)
                        return virtualFolderInfos;
                    // user has all libraries ticked in their access config
                    return libraryManager.GetVirtualFolders().ToDictionary(virtualFolderInfo => Guid.Parse(virtualFolderInfo.ItemId), virtualFolderInfo => virtualFolderInfo.Name);
                }
                IEnumerable<Guid> libraryIds = userLibraryPreference.Value.Split(',').Where(id => Guid.TryParse(id, out _)).Select(Guid.Parse);
                foreach (Guid libraryId in libraryIds) {
                    CollectionFolder? libraryFromDb = libraryManager.GetItemById<CollectionFolder>(libraryId);
                    if (libraryFromDb != null) {
                        virtualFolderInfos.Add(libraryFromDb.Id, libraryFromDb.Name);
                    }
                }
            }
            
            return virtualFolderInfos;
        }
    }
}