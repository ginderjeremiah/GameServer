using Game.Api.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// <see cref="SocketConnectionRegistry"/> is otherwise only driven through a real host in
    /// <c>SocketDrainTests</c> (whose factory strips <see cref="IHostedService"/> registrations, so the
    /// registry is constructed directly rather than resolved) — its <see cref="IHostApplicationLifetime"/>
    /// wiring and the run-once drain guard are never exercised there. This pins the "both
    /// <see cref="IHostApplicationLifetime.ApplicationStopping"/> and <see cref="SocketConnectionRegistry.StopAsync"/>
    /// reach the same drain" contract cheaply, with a fake lifetime and no live sockets.
    /// </summary>
    public class SocketConnectionRegistryTests
    {
        [Fact]
        public async Task EnsureDraining_ApplicationStoppingAndStopAsync_ShareTheSameDrainTask()
        {
            var lifetime = new FakeHostLifetime();
            var registry = new SocketConnectionRegistry(lifetime, NullLogger<SocketConnectionRegistry>.Instance);
            await registry.StartAsync(CancellationToken.None);

            // Call StopAsync explicitly first, capturing the drain task it starts.
            var explicitTask = registry.StopAsync(CancellationToken.None);

            // The ApplicationStopping hook wired by StartAsync must reach the SAME in-flight drain rather
            // than starting a second one — pin it by signalling shutdown and re-fetching via StopAsync.
            lifetime.TriggerStopping();
            var afterStopping = registry.StopAsync(CancellationToken.None);

            Assert.Same(explicitTask, afterStopping);
            await explicitTask;
        }

        private sealed class FakeHostLifetime : IHostApplicationLifetime
        {
            private readonly CancellationTokenSource _stopping = new();

            public CancellationToken ApplicationStarted => CancellationToken.None;
            public CancellationToken ApplicationStopping => _stopping.Token;
            public CancellationToken ApplicationStopped => CancellationToken.None;

            public void StopApplication() => _stopping.Cancel();
            public void TriggerStopping() => _stopping.Cancel();
        }
    }
}
