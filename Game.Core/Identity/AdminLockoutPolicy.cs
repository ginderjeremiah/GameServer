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
    /// The outcome of evaluating a single-user lifecycle action (archive/ban) against the admin-lockout
    /// rules.
    /// </summary>
    public enum UserActionProtection
    {
        /// <summary>The action is safe to apply.</summary>
        Allowed,

        /// <summary>The action targets the acting admin's own account.</summary>
        SelfTarget,

        /// <summary>The action would take the last usable admin out of circulation.</summary>
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
        /// <param name="otherUsableAdminsRemain">Whether any usable admin (non-archived, non-banned) other than the target would remain afterwards.</param>
        public static RoleChangeProtection CheckRoleChange(
            int actingUserId,
            int targetUserId,
            bool targetHasAdminRole,
            bool requestedRolesIncludeAdmin,
            bool otherUsableAdminsRemain)
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

            return otherUsableAdminsRemain ? RoleChangeProtection.Allowed : RoleChangeProtection.LastAdmin;
        }

        /// <summary>
        /// Evaluates a single-user lifecycle action (archive or ban) for the lockout hazards: targeting the
        /// acting admin's own account, or taking the last usable admin out of circulation. Acting on a
        /// non-admin is always allowed, since it cannot reduce the pool of admins.
        /// </summary>
        /// <param name="actingUserId">The admin performing the action.</param>
        /// <param name="targetUserId">The user being archived or banned.</param>
        /// <param name="targetHasAdminRole">Whether the target currently holds the Admin role.</param>
        /// <param name="otherUsableAdminsRemain">Whether any usable admin other than the target would remain afterwards.</param>
        public static UserActionProtection CheckUserAction(
            int actingUserId,
            int targetUserId,
            bool targetHasAdminRole,
            bool otherUsableAdminsRemain)
        {
            if (IsSelfTarget(actingUserId, targetUserId))
            {
                return UserActionProtection.SelfTarget;
            }

            if (!targetHasAdminRole)
            {
                return UserActionProtection.Allowed;
            }

            return otherUsableAdminsRemain ? UserActionProtection.Allowed : UserActionProtection.LastAdmin;
        }
    }
}
