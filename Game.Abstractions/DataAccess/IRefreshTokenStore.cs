namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Stores the opaque, single-use refresh tokens issued alongside short-lived JWT access tokens.
    /// Tokens are persisted server-side (keyed by a hash of the token value) so they can be rotated
    /// on use and revoked on logout.
    /// </summary>
    public interface IRefreshTokenStore
    {
        /// <summary>
        /// Issues a new refresh token for the given user, persisting it for <paramref name="lifetime"/>.
        /// Returns the raw token value to hand back to the client (only the hash is stored).
        /// </summary>
        Task<string> Issue(int userId, IReadOnlyList<string> roles, TimeSpan lifetime);

        /// <summary>
        /// Atomically validates and invalidates a refresh token (single use). Returns the associated
        /// session data when the token was valid, or <see langword="null"/> when it was missing,
        /// expired, or already consumed.
        /// </summary>
        Task<RefreshTokenData?> Consume(string refreshToken);

        /// <summary>
        /// Revokes a refresh token without issuing a replacement (e.g. on logout). No-op if unknown.
        /// </summary>
        Task Revoke(string refreshToken);
    }

    /// <summary>
    /// The session information carried by a refresh token: the user it authenticates and the roles
    /// to bake into the next access token. Mirrors the "roles are fixed for the session" model used
    /// at login (see backend docs) — a role change still requires a fresh login to take effect.
    /// </summary>
    public record RefreshTokenData(int UserId, IReadOnlyList<string> Roles);
}
