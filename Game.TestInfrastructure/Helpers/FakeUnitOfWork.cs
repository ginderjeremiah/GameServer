using Game.Application;

namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// The canonical <see cref="IUnitOfWork"/> test double: commits nothing and records that it was asked
    /// to, so a test can assert the commit happened (<see cref="CommitCount"/>) and under which token
    /// (<see cref="LastToken"/>), or ignore both when it only needs the commit boundary satisfied.
    /// </summary>
    public sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int CommitCount { get; private set; }

        /// <summary>The token of the most recent commit; <see cref="CancellationToken.None"/> if none has run.</summary>
        public CancellationToken LastToken { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            LastToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
