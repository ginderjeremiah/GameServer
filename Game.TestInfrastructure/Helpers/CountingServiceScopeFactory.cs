using Microsoft.Extensions.DependencyInjection;

namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// Wraps a real <see cref="IServiceScopeFactory"/> to count how many work scopes have been opened, so a
    /// test can tell whether a command began executing — e.g. whether a second command opened its scope
    /// while a first still held the per-socket command lock.
    /// </summary>
    public sealed class CountingServiceScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        private int _scopesCreated;

        public int ScopesCreated => Volatile.Read(ref _scopesCreated);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _scopesCreated);
            return inner.CreateScope();
        }
    }
}
