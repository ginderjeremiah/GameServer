namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Persists the per-account consecutive-login-failure state that drives an exponential login backoff
    /// (defence-in-depth against a slow, distributed credential guess that stays under any single IP's rate
    /// limit). State is keyed per account and reset on a successful login. Backed by the same Redis instance
    /// auth already depends on (consistent with the refresh-token store), so it adds no new durability
    /// assumption — losing the state only resets a streak, never grants or denies access on its own.
    /// </summary>
    public interface ILoginBackoffStore
    {
        /// <summary>
        /// Reads the current backoff state for <paramref name="username"/>, or <see langword="null"/> when no
        /// failures are currently tracked for it.
        /// </summary>
        Task<LoginBackoffState?> Get(string username, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists <paramref name="state"/> for <paramref name="username"/>, replacing any prior state, with
        /// <paramref name="retention"/> as its TTL so a stale streak self-expires after a quiet period.
        /// </summary>
        Task Set(string username, LoginBackoffState state, TimeSpan retention, CancellationToken cancellationToken = default);

        /// <summary>Clears any tracked backoff state for <paramref name="username"/> (on a successful login).</summary>
        Task Clear(string username, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The tracked consecutive-failure state for an account: how many failures have accrued in the current
    /// window and the instant before which the next attempt is rejected (equal to the failure instant while
    /// still under the free-attempt threshold, i.e. no active lock).
    /// </summary>
    public record LoginBackoffState(int FailureCount, DateTimeOffset LockedUntil);
}
