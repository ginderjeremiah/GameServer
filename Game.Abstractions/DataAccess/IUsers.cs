using Game.Abstractions.Contracts.Identity;
using Game.Core.Players;

namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Outcome of a <see cref="IUsers.SetUserRoles"/> attempt, so the caller can surface the
    /// appropriate message. The data tier owns validating the assignment against the role set and
    /// enforcing the admin-lockout self-protection rules.
    /// </summary>
    public enum SetUserRolesStatus
    {
        Success,
        UserNotFound,
        UnknownRole,

        /// <summary>The acting admin attempted to remove their own Admin role.</summary>
        SelfAdminRemoval,

        /// <summary>The change would have removed the Admin role from the last remaining admin.</summary>
        LastAdmin,
    }

    /// <summary>
    /// Outcome of a single-user lifecycle action (<see cref="IUsers.ArchiveUser"/> /
    /// <see cref="IUsers.BanUser"/>), so the caller can surface the appropriate message. The data tier
    /// enforces the admin-lockout rules: an admin cannot target their own account, nor take the last
    /// usable admin out of circulation.
    /// </summary>
    public enum UserActionStatus
    {
        Success,
        UserNotFound,

        /// <summary>The acting admin attempted the action on their own account.</summary>
        SelfTarget,

        /// <summary>The action would have taken the last usable admin out of circulation.</summary>
        LastAdmin,
    }

    public interface IUsers
    {
        /// <summary>
        /// Persists a brand-new account: the user record described by <paramref name="account"/> together
        /// with its initial player graph (built from the <paramref name="player"/> blueprint), linked via
        /// the navigation property so EF resolves the foreign key without the user's store-generated id.
        /// The insert is committed immediately (not deferred to the surrounding unit of work) so the
        /// active-username uniqueness guard can be honoured: returns <see langword="false"/> when the
        /// username was claimed by a concurrently-created active account, <see langword="true"/> otherwise.
        /// </summary>
        Task<bool> CreateAccount(NewAccount account, NewPlayer player);

        /// <summary>
        /// Loads the credentials of the non-archived account with the given username — the data the login
        /// use case needs to authenticate and establish a session. A banned account is still returned,
        /// flagged via <see cref="AccountCredentials.IsBanned"/> so the login flow can reject it with a
        /// distinct reason. Returns <see langword="null"/> when no such account matches (archived accounts
        /// are excluded, freeing the username for reuse).
        /// </summary>
        Task<AccountCredentials?> GetUser(string username);

        /// <summary>
        /// Returns the ids of the active (non-archived) user's players. Used to re-derive a session's
        /// player binding when rehydrating an evicted session cache for a still-valid access token.
        /// Empty when the user does not exist, is archived, or has no players.
        /// </summary>
        Task<IReadOnlyList<int>> GetPlayerIds(int userId);

        /// <summary>
        /// Determines whether the username is taken by an active (non-archived) account. Archived users
        /// do not count, so their usernames are available for reuse.
        /// </summary>
        Task<bool> CheckIfUsernameExists(string userName);

        /// <summary>
        /// Replaces the stored password hash for the given user. Used to transparently upgrade a
        /// credential to the current work factor after a successful login. No-op if the user does not exist.
        /// </summary>
        Task UpdatePasswordHash(int userId, string passHash);

        /// <summary>
        /// Returns a page of users (with their roles and player summaries), optionally filtered by a
        /// case-insensitive search matched against the username and the names of the user's players,
        /// and/or membership in a given role and/or archived state.
        /// </summary>
        Task<List<AdminUser>> SearchUsers(string? search, int? roleId, bool? archived, int skip, int take);

        /// <summary>
        /// Counts the users matching the same filters as <see cref="SearchUsers"/>.
        /// </summary>
        Task<int> CountUsers(string? search, int? roleId, bool? archived);

        /// <summary>
        /// Replaces the set of roles granted to the target user with the given role ids, validating that
        /// every id refers to a real role and enforcing the admin-lockout rules: the acting admin cannot
        /// strip their own Admin role, nor remove the Admin role from the last remaining admin. Returns the
        /// matching <see cref="SetUserRolesStatus"/> for each rejection, or
        /// <see cref="SetUserRolesStatus.Success"/> when the change is applied.
        /// </summary>
        Task<SetUserRolesStatus> SetUserRoles(int actingUserId, int targetUserId, IReadOnlyCollection<int> roleIds);

        /// <summary>
        /// Archives (soft-deletes) the target user, freeing their username for reuse. Enforces the
        /// admin-lockout rules: rejects an admin archiving their own account
        /// (<see cref="UserActionStatus.SelfTarget"/>) and archiving the last usable admin
        /// (<see cref="UserActionStatus.LastAdmin"/>). Returns <see cref="UserActionStatus.UserNotFound"/>
        /// if the user does not exist.
        /// </summary>
        Task<UserActionStatus> ArchiveUser(int actingUserId, int targetUserId);

        /// <summary>
        /// Bans the target user while keeping their username reserved. Enforces the admin-lockout rules:
        /// rejects an admin banning their own account (<see cref="UserActionStatus.SelfTarget"/>) and
        /// banning the last usable admin (<see cref="UserActionStatus.LastAdmin"/>). Returns
        /// <see cref="UserActionStatus.UserNotFound"/> if the user does not exist.
        /// </summary>
        Task<UserActionStatus> BanUser(int actingUserId, int targetUserId);
    }
}
