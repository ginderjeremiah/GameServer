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
        /// Atomically replaces the stored state for <paramref name="username"/> with <paramref name="next"/>
        /// (TTL <paramref name="retention"/>) only if the currently stored state still matches
        /// <paramref name="expected"/> — or, when <paramref name="expected"/> is <see langword="null"/>, only if
        /// no state is currently stored. Returns <see langword="false"/> when a concurrent failure changed it
        /// first, so the caller re-reads and retries; this compare-and-set is what keeps the failure count
        /// race-safe under concurrent attempts (a plain read-modify-write would lose increments).
        /// </summary>
        Task<bool> TryUpdate(string username, LoginBackoffState? expected, LoginBackoffState next, TimeSpan retention, CancellationToken cancellationToken = default);

        /// <summary>Clears any tracked backoff state for <paramref name="username"/> (on a successful login).</summary>
        Task Clear(string username, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The tracked consecutive-failure state for an account: how many failures have accrued in the current
    /// window and the instant before which the next attempt is rejected (equal to the failure instant while
    /// still under the free-attempt threshold, i.e. no active lock).
    /// <para>
    /// Its JSON serialization must stay a deterministic, idempotent round-trip (<c>Serialize(Deserialize(x))
    /// == Serialize(x)</c>): the store's compare-and-set (<see cref="TryUpdate"/>) matches the stored payload
    /// by re-serialising the expected state. Adding a field with a culture-sensitive or non-idempotent
    /// converter would break that match and turn the guard's retry into a spin, so keep this record
    /// plainly serializable.
    /// </para>
    /// </summary>
    public record LoginBackoffState(int FailureCount, DateTimeOffset LockedUntil);
}
