using Game.Abstractions.DataAccess;

namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// The canonical <see cref="IUserLogins"/> test double: records every call and the token it arrived under,
    /// so a test can assert the dedupe memo skipped a DB round-trip (<see cref="RecordConnectionCallCount"/>),
    /// that the request's token reached the data tier (<see cref="LastRecordToken"/>,
    /// <see cref="LastSaveToken"/>), or when the call happened relative to other events
    /// (<see cref="OnRecordConnection"/>) — and ignore the rest.
    /// <see cref="ThrowOnRecordConnection"/> pins the failure path.
    /// </summary>
    public sealed class FakeUserLogins : IUserLogins
    {
        public int RecordConnectionCallCount { get; private set; }
        public int SaveDeviceInfoCallCount { get; private set; }

        /// <summary>The token of the most recent call; <see cref="CancellationToken.None"/> if none has run.</summary>
        public CancellationToken LastRecordToken { get; private set; }
        public CancellationToken LastSaveToken { get; private set; }

        /// <summary>When set, <see cref="RecordConnection"/> throws after recording the attempt.</summary>
        public bool ThrowOnRecordConnection { get; set; }

        /// <summary>Invoked at the start of <see cref="RecordConnection"/>, before any configured throw.</summary>
        public Action? OnRecordConnection { get; set; }

        public Task RecordConnection(
            int userId,
            string ipAddress,
            string deviceFingerprintHash,
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform,
            CancellationToken cancellationToken = default)
        {
            RecordConnectionCallCount++;
            LastRecordToken = cancellationToken;
            OnRecordConnection?.Invoke();
            if (ThrowOnRecordConnection)
            {
                throw new InvalidOperationException("simulated tracking failure");
            }

            return Task.CompletedTask;
        }

        public Task SaveDeviceInfo(
            int userId,
            string deviceFingerprintHash,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform,
            double? deviceMemory,
            int? hardwareConcurrency,
            CancellationToken cancellationToken = default)
        {
            SaveDeviceInfoCallCount++;
            LastSaveToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
