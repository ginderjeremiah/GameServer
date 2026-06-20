using Microsoft.Extensions.Hosting;

namespace Game.Infrastructure.Redis
{
    /// <summary>
    /// Closes the process-lifetime Redis connection multiplexers on graceful host shutdown (#954). The
    /// multiplexers are shared, process-wide singletons handed out by <see cref="RedisMultiplexerFactory"/>, so a
    /// single hosted service owns disposing them on stop rather than leaving the connections to be force-killed
    /// when the process dies — the missing teardown for an app designed to run as multiple scalable instances
    /// (rolling deploys, scale-down), mirroring the graceful socket drain.
    /// </summary>
    internal sealed class RedisConnectionLifetime : IHostedService
    {
        private readonly Func<Task> _disposeConnections;

        public RedisConnectionLifetime() : this(RedisMultiplexerFactory.DisposeAllAsync) { }

        // The disposal action is injectable so the stop hook can be unit-tested without opening real connections.
        internal RedisConnectionLifetime(Func<Task> disposeConnections)
        {
            _disposeConnections = disposeConnections;
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => _disposeConnections();
    }
}
