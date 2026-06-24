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

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // Honor the host's shutdown deadline: multiplexer disposal is normally fast, but a hung close must
            // not stall host shutdown past the deadline. If the token trips first, stop awaiting and let the
            // process exit reclaim the connection — the same bounded give-up the socket drain and write-behind
            // drain apply. The disposal task is left to finish (or die with the process) in the background.
            try
            {
                await _disposeConnections().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown deadline reached before disposal completed; unwinding is the deliberate give-up.
            }
        }
    }
}
