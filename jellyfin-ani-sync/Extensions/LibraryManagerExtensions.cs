#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace jellyfin_ani_sync.Extensions { 
    public static class LibraryManagerExtensions {
        /// <summary>
        /// Checks if the user has access to the list of <see cref="CollectionFolder"/>.
        /// </summary>
        /// <returns>True if the user has access to the list of <see cref="CollectionFolder"/>, false if not.</returns>
        public static bool UserHasAccessToLibraries(this ILibraryManager libraryManager, HashSet<Guid> libraryIds, User user) {
            Dictionary<Guid, string> librariesUserHasAccessTo = libraryManager.GetLibrariesUserHasAccessTo(user);
            return libraryIds.All(id => librariesUserHasAccessTo.ContainsKey(id));
        }

        /// <summary>
        /// Retrieves the ID and name of the <see cref="CollectionFolder"/>s the <see cref="User"/> has access to.
        /// </summary>
        /// <param name="libraryManager">Instance of <see cref="ILibraryManager"/>.</param>
        /// <param name="user">The <see cref="User"/> to get the <see cref="CollectionFolder"/>s of.</param>
        /// <returns><see cref="Dictionary{Guid,string}"/> of <see cref="CollectionFolder"/> IDs and names.</returns>
        public static Dictionary<Guid, string> GetLibrariesUserHasAccessTo(this ILibraryManager libraryManager, User user) {
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