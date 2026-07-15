using Game.TestInfrastructure.Helpers;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Covers <see cref="PollingHelper"/>'s poll-loop semantics and the <see cref="CancellationToken"/> overload
    /// (#1949): the token must flow into the delay so a caller can abort a stuck poll early instead of always
    /// riding out the full timeout.
    /// </summary>
    public class PollingHelperTests
    {
        [Fact]
        public async Task PollUntilAsync_SatisfiedOnFirstRead_ReturnsWithoutDelaying()
        {
            var result = await PollingHelper.PollUntilAsync(() => Task.FromResult(1), v => v == 1, timeoutMs: 5000);

            Assert.Equal(1, result);
        }

        [Fact]
        public async Task PollUntilAsync_NeverSatisfied_ReturnsLastReadValueAfterTimeout()
        {
            var result = await PollingHelper.PollUntilAsync(() => Task.FromResult(0), v => v == 1, timeoutMs: 50);

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task PollUntilAsync_WithAlreadyCancelledToken_ThrowsInsteadOfWaitingOutTheFullTimeout()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                PollingHelper.PollUntilAsync(() => Task.FromResult(0), v => v == 1, cts.Token, timeoutMs: 5000));
        }

        [Fact]
        public async Task PollUntilAsync_WithCancellationTokenOverload_SatisfiedOnFirstRead_ReturnsWithoutThrowing()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Satisfied on the first read, so the cancelled token is never consulted (no delay is awaited).
            var result = await PollingHelper.PollUntilAsync(() => Task.FromResult(1), v => v == 1, cts.Token);

            Assert.Equal(1, result);
        }
    }
}
