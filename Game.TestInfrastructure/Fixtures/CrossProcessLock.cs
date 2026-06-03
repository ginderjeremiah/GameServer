namespace Game.TestInfrastructure.Fixtures
{
    /// <summary>
    /// A cross-process exclusive lock backed by an OS file lock (honored by .NET's
    /// <see cref="FileShare.None"/> on both Windows and Linux). It is used to serialize integration
    /// test assemblies that share a single set of backing services — the reuse path described in
    /// <see cref="PreexistingContainerInfo"/> — so their per-test truncate/flush cleanup cannot
    /// corrupt each other. Under Testcontainers every assembly owns its containers, so no lock is
    /// needed. Because the lock is an OS file handle, it is released automatically if the holding
    /// process exits unexpectedly, so a crashed run cannot leave a stale lock behind.
    /// </summary>
    internal sealed class CrossProcessLock : IDisposable
    {
        // Generous upper bound so a wedged peer fails the run loudly instead of hanging forever.
        private static readonly TimeSpan _acquireTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(100);

        private readonly FileStream _handle;

        private CrossProcessLock(FileStream handle)
        {
            _handle = handle;
        }

        public static async Task<CrossProcessLock> AcquireAsync(string lockFilePath, CancellationToken cancellationToken = default)
        {
            var deadline = DateTime.UtcNow + _acquireTimeout;
            while (true)
            {
                try
                {
                    var handle = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    return new CrossProcessLock(handle);
                }
                catch (IOException) when (DateTime.UtcNow < deadline)
                {
                    // Another process holds the lock; wait and retry until it releases or we time out.
                    await Task.Delay(_pollInterval, cancellationToken);
                }
            }
        }

        public void Dispose()
        {
            _handle.Dispose();
        }
    }
}
