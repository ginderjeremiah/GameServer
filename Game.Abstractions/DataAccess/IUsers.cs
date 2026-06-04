using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IUsers
    {
        /// <summary>
        /// Loads the active (non-archived) user with the given username, including their players and roles.
        /// </summary>
        Task<User?> GetUser(string username);

        /// <summary>
        /// Determines whether the username is taken by an active (non-archived) account. Archived users
        /// do not count, so their usernames are available for reuse.
        /// </summary>
        Task<bool> CheckIfUsernameExists(string userName);

        /// <summary>
        /// Loads a single user by id (including roles) regardless of archived/banned status, for admin management.
        /// </summary>
        Task<User?> GetUserById(int id);

        /// <summary>
        /// Returns a page of non-archived users (including their roles), optionally filtered by a
        /// case-insensitive username search and/or membership in a given role.
        /// </summary>
        Task<List<User>> SearchUsers(string? search, int? roleId, int skip, int take);

        /// <summary>
        /// Counts the non-archived users matching the same filters as <see cref="SearchUsers"/>.
        /// </summary>
        Task<int> CountUsers(string? search, int? roleId);

        /// <summary>
        /// Replaces the set of roles granted to the user with the given role ids. Returns false if the user does not exist.
        /// </summary>
        Task<bool> SetUserRoles(int userId, IReadOnlyCollection<int> roleIds);

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
