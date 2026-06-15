namespace Game.Core.Identity
{
    /// <summary>
    /// The outcome of evaluating a role replacement against the admin-lockout rules.
    /// </summary>
    public enum RoleChangeProtection
    {
        /// <summary>The role change is safe to apply.</summary>
        Allowed,

        /// <summary>The change would strip the acting admin's own Admin role.</summary>
        SelfAdminRemoval,

        /// <summary>The change would remove the Admin role from the last remaining admin.</summary>
        LastAdmin,
    }

    /// <summary>
    /// Pure self-protection rules for privileged user-administration actions, guarding an admin from
    /// locking themselves — or everyone — out: banning/archiving their own account, dropping their own
    /// Admin role, or removing the Admin role from the last remaining admin. The rules take only plain
    /// facts, so the data tier supplies them (the target's current roles, whether another admin remains)
    /// and this layer makes the decision — keeping it unit-testable and free of data access.
    /// </summary>
    public static class AdminLockoutPolicy
    {
        /// <summary>
        /// Whether a single-user action (ban/archive) targets the acting admin's own account, which would
        /// let them lock themselves out and is therefore disallowed.
        /// </summary>
        public static bool IsSelfTarget(int actingUserId, int targetUserId)
        {
            return actingUserId == targetUserId;
        }

        /// <summary>
        /// Evaluates a role replacement for the lockout hazards: stripping the caller's own Admin role, or
        /// removing the Admin role from the last remaining admin. A change that does not remove the target's
        /// Admin role (a grant, or any change to a user who is not losing Admin) is always allowed.
        /// </summary>
        /// <param name="actingUserId">The admin performing the change.</param>
        /// <param name="targetUserId">The user whose roles are being replaced.</param>
        /// <param name="targetHasAdminRole">Whether the target currently holds the Admin role.</param>
        /// <param name="requestedRolesIncludeAdmin">Whether the requested role set keeps the Admin role.</param>
        /// <param name="otherAdminsRemain">Whether any admin other than the target would remain afterwards.</param>
        public static RoleChangeProtection CheckRoleChange(
            int actingUserId,
            int targetUserId,
            bool targetHasAdminRole,
            bool requestedRolesIncludeAdmin,
            bool otherAdminsRemain)
        {
            var removesAdminRole = targetHasAdminRole && !requestedRolesIncludeAdmin;
            if (!removesAdminRole)
            {
                return RoleChangeProtection.Allowed;
            }

            if (IsSelfTarget(actingUserId, targetUserId))
            {
                return RoleChangeProtection.SelfAdminRemoval;
            }

            return otherAdminsRemain ? RoleChangeProtection.Allowed : RoleChangeProtection.LastAdmin;
        }
    }
}
