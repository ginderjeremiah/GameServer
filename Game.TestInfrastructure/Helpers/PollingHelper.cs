namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// Shared polling loop for integration tests reading a fire-and-forget write (e.g. Redis
    /// <c>HashSetAndForget</c>/<c>ReclaimAndForget</c>) that can complete before the write lands
    /// on the server, so a read taken immediately after can race it (#1718).
    /// </summary>
    public static class PollingHelper
    {
        /// <summary>
        /// Polls <paramref name="read"/> every 25ms, returning as soon as <paramref name="satisfied"/> holds
        /// on the read value. Returns the last read value even if <paramref name="satisfied"/> never holds
        /// before <paramref name="timeoutMs"/> elapses, leaving the pass/fail decision to the caller.
        /// </summary>
        public static async Task<T> PollUntilAsync<T>(Func<Task<T>> read, Func<T, bool> satisfied, int timeoutMs = 5000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            T value;
            do
            {
                value = await read();
                if (satisfied(value))
                {
                    return value;
                }

                await Task.Delay(25);
            } while (DateTime.UtcNow < deadline);

            return value;
        }
    }
}
