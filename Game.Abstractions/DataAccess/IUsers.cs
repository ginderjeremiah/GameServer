using Game.Abstractions.Contracts.Identity;
using Game.Core.Players;

namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Outcome of a <see cref="IUsers.SetUserRoles"/> attempt, so the caller can surface the
    /// appropriate message. The data tier owns validating the assignment against the role set.
    /// </summary>
    public enum SetUserRolesStatus
    {
        Success,
        UserNotFound,
        UnknownRole,
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
        /// Loads the credentials of the active (non-archived) account with the given username — the data
        /// the login use case needs to authenticate and establish a session. Returns <see langword="null"/>
        /// when no active account matches.
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
        /// Replaces the set of roles granted to the user with the given role ids, validating that every id
        /// refers to a real role. Returns <see cref="SetUserRolesStatus.UserNotFound"/> if the user does not
        /// exist or <see cref="SetUserRolesStatus.UnknownRole"/> if any id is not a known role.
        /// </summary>
        Task<SetUserRolesStatus> SetUserRoles(int userId, IReadOnlyCollection<int> roleIds);

        /// <summary>
        /// Archives (soft-deletes) the user, freeing their username for reuse. Returns false if the user does not exist.
        /// </summary>
        Task<bool> ArchiveUser(int userId);

        /// <summary>
        /// Bans the user while keeping their username reserved. Returns false if the user does not exist.
        /// </summary>
        Task<bool> BanUser(int userId);
    }
}
