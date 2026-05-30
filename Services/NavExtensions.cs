using System.Security.Claims;

namespace GymSaaS.Services
{
    /// <summary>
    /// Helpers for the dynamic sidebar — checks whether the current user is
    /// allowed to see a particular view folder (i.e., a sidebar item).
    ///
    /// SuperAdmin always sees everything regardless of role permissions.
    /// All other roles must have the matching "AllowedView" claim, which is
    /// added at login from the user's role view permissions.
    /// </summary>
    public static class NavExtensions
    {
        /// <summary>
        /// Returns true when the user is allowed to see the given view folder.
        /// </summary>
        /// <param name="user">The current ClaimsPrincipal (usually <c>User</c> in a view).</param>
        /// <param name="folderName">The view folder name, e.g. "Members" or "Reception".</param>
        public static bool CanSee(this ClaimsPrincipal user, string folderName)
        {
            if (user?.Identity?.IsAuthenticated != true)
                return false;

            // SuperAdmin always passes
            if (user.IsInRole("SuperAdmin"))
                return true;

            var permissionCode = "views." + folderName.Trim().ToLowerInvariant();
            return user.HasClaim("AllowedView", permissionCode);
        }

        /// <summary>
        /// Returns true if the user can see at least one of the supplied folders.
        /// Used to decide whether to render a sidebar section header at all.
        /// </summary>
        public static bool CanSeeAny(this ClaimsPrincipal user, params string[] folderNames)
        {
            if (folderNames == null || folderNames.Length == 0)
                return false;

            foreach (var folder in folderNames)
                if (user.CanSee(folder)) return true;

            return false;
        }
    }
}
