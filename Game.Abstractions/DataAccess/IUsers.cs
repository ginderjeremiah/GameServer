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
    /// <see cref="IUsers.BanUser"/> / <see cref="IUsers.UnarchiveUser"/> / <see cref="IUsers.UnbanUser"/>),
    /// so the caller can surface the appropriate message. The data tier always rejects an admin targeting
    /// their own account with this action (self-reversal included — a banned/archived admin's still-valid
    /// session token would otherwise let them undo the action against themselves before it expires); the
    /// archive/ban lockout rules additionally guard against taking the last usable admin out of
    /// circulation, which reinstating can never do.
    /// </summary>
    public enum UserActionStatus
    {
        Success,
        UserNotFound,

        /// <summary>The acting admin attempted the action on their own account.</summary>
        SelfTarget,

        /// <summary>The action would have taken the last usable admin out of circulation.</summary>
        LastAdmin,

        /// <summary>
        /// Unarchiving would collide with an active account that has since claimed the freed username.
        /// </summary>
        UsernameTaken,
    }

    /// <summary>
    /// Outcome of a <see cref="IUsers.CreatePlayer"/> attempt, so the caller can surface the appropriate
    /// message. The per-account character cap is enforced in the data tier under a row lock so it holds
    /// against concurrent creations. (Distinct from the application layer's <c>CreatePlayerStatus</c>,
    /// which also reports name-validation failures decided before persistence.)
    /// </summary>
    public enum CreatePlayerOutcome
    {
        Success,

        /// <summary>The account already holds the maximum number of characters.</summary>
        CapReached,

        /// <summary>No active (non-archived) account matched the supplied id.</summary>
        UserNotFound,
    }

    /// <summary>
    /// Result of <see cref="IUsers.CreatePlayer"/>: an outcome plus, on success, the summary of the newly
    /// created character (so the caller can return it without a second read).
    /// </summary>
    public record CreatePlayerResult(CreatePlayerOutcome Outcome, PlayerSummary? Player)
    {
        public static CreatePlayerResult Created(PlayerSummary player)
        {
            return new CreatePlayerResult(CreatePlayerOutcome.Success, player);
        }

        public static CreatePlayerResult Failed(CreatePlayerOutcome outcome)
        {
            return new CreatePlayerResult(outcome, null);
        }
    }

    public interface IUsers
    {
        /// <summary>
        /// Persists a brand-new account (the user record described by <paramref name="account"/>) with no
        /// characters — a freshly signed-up account creates its first character later, on the select screen.
        /// The insert is committed immediately (not deferred to the surrounding unit of work) so the
        /// active-username uniqueness guard can be honoured: returns <see langword="false"/> when the
        /// username was claimed by a concurrently-created active account, <see langword="true"/> otherwise.
        /// </summary>
        Task<bool> CreateAccount(NewAccount account, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates an additional character on an existing account from the <paramref name="player"/>
        /// blueprint, enforcing the per-account cap (<paramref name="maxPlayersPerAccount"/>) as anti-cheat.
        /// The count check and insert run under a lock on the owning user row and commit in-tier, so two
        /// concurrent creations for the same account can't both slip past the cap. Returns
        /// <see cref="CreatePlayerOutcome.CapReached"/> when the account is already at the cap and
        /// <see cref="CreatePlayerOutcome.UserNotFound"/> when no active account matches.
        /// </summary>
        Task<CreatePlayerResult> CreatePlayer(int userId, NewPlayer player, int maxPlayersPerAccount, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads the credentials of the non-archived account with the given username — the data the login
        /// use case needs to authenticate and establish a session. A banned account is still returned,
        /// flagged via <see cref="AccountCredentials.IsBanned"/> so the login flow can reject it with a
        /// distinct reason. Returns <see langword="null"/> when no such account matches (archived accounts
        /// are excluded, freeing the username for reuse).
        /// </summary>
        Task<AccountCredentials?> GetUser(string username, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the ids of the active (non-archived) user's players. Used to validate that a selected
        /// player belongs to the authenticated account (anti-cheat) before binding the session. Empty when
        /// the user does not exist, is archived, or has no players.
        /// </summary>
        Task<IReadOnlyList<int>> GetPlayerIds(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns lightweight summaries of the active (non-archived) user's players for the login
        /// player-selection list (name, level, current zone). Empty when the user does not exist, is
        /// archived, or has no players.
        /// </summary>
        Task<IReadOnlyList<PlayerSummary>> GetPlayerSummaries(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether the username is taken by an active (non-archived) account. Archived users
        /// do not count, so their usernames are available for reuse.
        /// </summary>
        Task<bool> CheckIfUsernameExists(string userName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces the stored password hash for the given user. Used to transparently upgrade a
        /// credential to the current work factor after a successful login. No-op if the user does not exist.
        /// </summary>
        Task UpdatePasswordHash(int userId, string passHash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a page of users (with their roles and player summaries), optionally filtered by a
        /// case-insensitive search matched against the username and the names of the user's players,
        /// and/or membership in a given role and/or archived state.
        /// </summary>
        Task<List<AdminUser>> SearchUsers(string? search, int? roleId, bool? archived, int skip, int take, CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts the users matching the same filters as <see cref="SearchUsers"/>.
        /// </summary>
        Task<int> CountUsers(string? search, int? roleId, bool? archived, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces the set of roles granted to the target user with the given role ids, validating that
        /// every id refers to a real role and enforcing the admin-lockout rules: the acting admin cannot
        /// strip their own Admin role, nor remove the Admin role from the last remaining admin. Returns the
        /// matching <see cref="SetUserRolesStatus"/> for each rejection, or
        /// <see cref="SetUserRolesStatus.Success"/> when the change is applied.
        /// </summary>
        Task<SetUserRolesStatus> SetUserRoles(int actingUserId, int targetUserId, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Archives (soft-deletes) the target user, freeing their username for reuse. Enforces the
        /// admin-lockout rules: rejects an admin archiving their own account
        /// (<see cref="UserActionStatus.SelfTarget"/>) and archiving the last usable admin
        /// (<see cref="UserActionStatus.LastAdmin"/>). Returns <see cref="UserActionStatus.UserNotFound"/>
        /// if the user does not exist.
        /// </summary>
        Task<UserActionStatus> ArchiveUser(int actingUserId, int targetUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bans the target user while keeping their username reserved. Enforces the admin-lockout rules:
        /// rejects an admin banning their own account (<see cref="UserActionStatus.SelfTarget"/>) and
        /// banning the last usable admin (<see cref="UserActionStatus.LastAdmin"/>). Returns
        /// <see cref="UserActionStatus.UserNotFound"/> if the user does not exist.
        /// </summary>
        Task<UserActionStatus> BanUser(int actingUserId, int targetUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reverses <see cref="ArchiveUser"/>: clears the target's archived state, restoring their username
        /// to active-uniqueness enforcement. Rejects the acting admin targeting their own account
        /// (<see cref="UserActionStatus.SelfTarget"/>) and <see cref="UserActionStatus.UserNotFound"/> if the
        /// user does not exist. Since archiving frees the username, another active account may have claimed
        /// it in the meantime; unarchiving then returns <see cref="UserActionStatus.UsernameTaken"/> rather
        /// than silently renaming either account — the admin must resolve the collision first. Reinstating
        /// never reduces the usable-admin pool, so unlike <see cref="ArchiveUser"/> there is no last-admin
        /// guard. A no-op (already active) target returns <see cref="UserActionStatus.Success"/>.
        /// </summary>
        Task<UserActionStatus> UnarchiveUser(int actingUserId, int targetUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reverses <see cref="BanUser"/>: clears the target's banned state. Rejects the acting admin
        /// targeting their own account (<see cref="UserActionStatus.SelfTarget"/>) and returns
        /// <see cref="UserActionStatus.UserNotFound"/> if the user does not exist. Reinstating never reduces
        /// the usable-admin pool, so unlike <see cref="BanUser"/> there is no last-admin guard. A no-op
        /// (already unbanned) target returns <see cref="UserActionStatus.Success"/>.
        /// </summary>
        Task<UserActionStatus> UnbanUser(int actingUserId, int targetUserId, CancellationToken cancellationToken = default);
    }
}
