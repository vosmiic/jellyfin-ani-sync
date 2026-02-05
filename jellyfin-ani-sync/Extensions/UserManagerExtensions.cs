#nullable enable
using System;
using System.Linq;
using System.Security.Claims;
using Jellyfin.Extensions;
using jellyfin_ani_sync.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Library;

namespace jellyfin_ani_sync.Extensions {
    public static class UserManagerExtensions {
        /// <summary>
        /// Checks permissions to change user Ani-Sync configuration and returns said user.
        /// </summary>
        /// <param name="userManager">Instance of <see cref="IUserManager"/>.</param>
        /// <param name="claimsPrincipal">The <see cref="ClaimsPrincipal"/> of the currently logged-in user.</param>
        /// <param name="userId">ID of the <see cref="User"/> the logged-in user would like to access. Null to access the logged-in user.</param>
        /// <returns>User if the requester has permissions to interact with the user. Null if the requester cannot interact with the user./></returns>
        public static User? GetUser(this IUserManager userManager, ClaimsPrincipal claimsPrincipal, Guid? userId)
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
    }
}