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
        Task<string> Issue(int userId, int? playerId, TimeSpan lifetime, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically validates and invalidates a refresh token (single use). Returns the associated
        /// session data when the token was valid, or <see langword="null"/> when it was missing,
        /// expired, or already consumed.
        /// </summary>
        Task<RefreshTokenData?> Consume(string refreshToken, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The session information carried by a refresh token: the user it authenticates and the selected
    /// player id (once chosen; <see langword="null"/> before selection), so a refresh keeps the same
    /// character bound. Roles are deliberately not cached here — a refresh re-derives them from the
    /// account's live state instead, so a role change (or a ban) takes effect on the account's next
    /// refresh rather than only at its next login (see backend docs).
    /// </summary>
    public record RefreshTokenData(int UserId, int? PlayerId);
}
