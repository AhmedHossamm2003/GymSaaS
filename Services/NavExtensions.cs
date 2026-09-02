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

        // ── Branch scoping ─────────────────────────────────────────────
        // A staff user can be assigned to one or more branches (UserBranches),
        // surfaced as "BranchId" claims at login. The rule across the app:
        //   • assigned to one or more branches → see only those branches' data
        //   • assigned to no branch            → see ALL branches' data

        /// <summary>
        /// The branch IDs this user is scoped to. Empty = unrestricted (all branches).
        /// </summary>
        public static List<Guid> AssignedBranchIds(this ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
                return new List<Guid>();

            return user.FindAll("BranchId")
                .Select(c => Guid.TryParse(c.Value, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// True when the user is restricted to a subset of branches (has at least
        /// one branch assignment). False = sees all branches.
        /// </summary>
        public static bool IsBranchRestricted(this ClaimsPrincipal user) =>
            user.AssignedBranchIds().Count > 0;

        /// <summary>
        /// True when the user may see data for the given branch — either they are
        /// unrestricted, or the branch is among their assignments.
        /// </summary>
        public static bool CanAccessBranch(this ClaimsPrincipal user, Guid branchId)
        {
            var ids = user.AssignedBranchIds();
            return ids.Count == 0 || ids.Contains(branchId);
        }
    }
}
