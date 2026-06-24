using Game.Infrastructure.Redis;
using Xunit;

namespace Game.Infrastructure.Tests
{
    /// <summary>
    /// Pins the graceful-shutdown hook for the shared Redis multiplexers (#954): the hosted service disposes the
    /// process-lifetime connections on stop and never on start. The disposal action is injected so the contract is
    /// verified without opening a real connection — the actual multiplexer dispose/clear is covered by
    /// <see cref="RedisMultiplexerFactoryTests"/>.
    /// </summary>
    public class RedisConnectionLifetimeTests
    {
        [Fact]
        public async Task StopAsync_InvokesTheDisposalAction()
        {
            var disposeCount = 0;
            var lifetime = new RedisConnectionLifetime(() =>
            {
                disposeCount++;
                return Task.CompletedTask;
            });

            await lifetime.StopAsync(CancellationToken.None);

            Assert.Equal(1, disposeCount);
        }

        [Fact]
        public async Task StopAsync_DisposalHangs_GivesUpWhenTheShutdownDeadlineTrips()
        {
            // A disposal that never completes models a hung multiplexer close. StopAsync must honor the host's
            // shutdown token and return rather than block host shutdown indefinitely.
            var lifetime = new RedisConnectionLifetime(() => new TaskCompletionSource().Task);

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Returns (does not throw, does not hang) once the deadline has tripped.
            await lifetime.StopAsync(cts.Token).WaitAsync(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task StartAsync_DoesNotDispose()
        {
            var disposed = false;
            var lifetime = new RedisConnectionLifetime(() =>
            {
                disposed = true;
                return Task.CompletedTask;
            });

            await lifetime.StartAsync(CancellationToken.None);

            // Connections are torn down on stop only; startup must leave them live.
            Assert.False(disposed);
        }
    }
}
