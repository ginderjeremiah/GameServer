using Game.Application;

namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// An <see cref="IUnitOfWork"/> that commits nothing, for tests whose subject reaches the commit
    /// boundary but asserts nothing about it. Use <c>CapturingUnitOfWork</c>-style local doubles only
    /// when a test needs to observe the commit itself.
    /// </summary>
    public sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
