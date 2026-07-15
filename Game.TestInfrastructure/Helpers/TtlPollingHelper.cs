using StackExchange.Redis;

namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// Shared TTL-polling helper for the sliding-idle-TTL cache-eviction integration tests (#439, #537).
    /// </summary>
    public static class TtlPollingHelper
    {
        /// <summary>
        /// Polls the key's TTL until it satisfies <paramref name="predicate"/> (defaults to "any TTL is set"),
        /// tolerating a fire-and-forget write not having landed yet. <c>KeyTimeToLiveAsync</c> returns null
        /// both for a missing key and a key with no expiry, so a non-null result proves an expiry is attached.
        /// </summary>
        public static Task<TimeSpan?> WaitForTtlAsync(IDatabase db, string key, Func<TimeSpan, bool>? predicate = null)
        {
            predicate ??= _ => true;
            return PollingHelper.PollUntilAsync(
                () => db.KeyTimeToLiveAsync(key), ttl => ttl is not null && predicate(ttl.Value));
        }
    }
}
