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
        /// Carries the selected player id (once chosen) so it survives a refresh; <see langword="null"/>
        /// before player selection. Returns the raw token value to hand back to the client (only the hash
        /// is stored).
        /// </summary>
        Task<string> Issue(int userId, IReadOnlyList<string> roles, int? playerId, TimeSpan lifetime, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically validates and invalidates a refresh token (single use). Returns the associated
        /// session data when the token was valid, or <see langword="null"/> when it was missing,
        /// expired, or already consumed.
        /// </summary>
        Task<RefreshTokenData?> Consume(string refreshToken, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The session information carried by a refresh token: the user it authenticates, the roles to bake
    /// into the next access token, and the selected player id (once chosen; <see langword="null"/> before
    /// selection). Mirrors the "roles are fixed for the session" model used at login (see backend docs) —
    /// a role change still requires a fresh login to take effect — and likewise carries the selected
    /// player forward so a refresh keeps the same character bound.
    /// </summary>
    public record RefreshTokenData(int UserId, IReadOnlyList<string> Roles, int? PlayerId);
}
