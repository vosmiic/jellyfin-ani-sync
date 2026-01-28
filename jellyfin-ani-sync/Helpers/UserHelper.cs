using System;
using System.Linq;
using System.Security;
using System.Security.Claims;
using Jellyfin.Extensions;
using jellyfin_ani_sync.Enums;

namespace jellyfin_ani_sync.Helpers {
    public static class UserHelper {

        public static string? GetClaimValue(ClaimsPrincipal user, string name)
            => user.Claims.FirstOrDefault(claim => claim.Type.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;

        public static Guid? GetCurrentUserId(ClaimsPrincipal claimsPrincipal)
        {
            string currentUserString = GetClaimValue(claimsPrincipal, ClaimValues.UserId) ?? string.Empty;
            if (Guid.TryParse(currentUserString, out Guid userId))
            {
                return userId;
            }
            return null;
        }

        public static Guid? GetUserId(ClaimsPrincipal claimsPrincipal, Guid? userId)
        {
            var currentUserId = GetCurrentUserId(claimsPrincipal);

            if (currentUserId.IsNullOrEmpty()) return null;

            if (userId.IsNullOrEmpty()) return currentUserId;

            var isAdministrator = claimsPrincipal.IsInRole(nameof(UserRoles.Administrator));

            if (!userId.Equals(currentUserId) && !isAdministrator) return null;

            return userId.Value;
        }
    }
}