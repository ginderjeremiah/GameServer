using Game.Abstractions.DataAccess;
using Game.Application.Auth;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Game.Application.Tests.Auth
{
    /// <summary>
    /// Integration tests for the guard's compare-and-set retry over the real Redis-backed store. The pure
    /// backoff arithmetic is unit-tested in <see cref="LoginBackoffPolicyTests"/>; these cover the part that
    /// arithmetic alone cannot — that concurrent failures for one account each land, so the streak climbs at
    /// the attempt rate instead of losing increments to a read-modify-write race.
    /// </summary>
    [Collection("Integration")]
    public class LoginBackoffGuardIntegrationTests : ApplicationIntegrationTestBase
    {
        public LoginBackoffGuardIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task RegisterFailure_ConcurrentAttemptsForOneAccount_CountReflectsEveryFailure()
        {
            using var scope = CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ILoginBackoffStore>();
            // A high threshold keeps every attempt a no-delay increment, isolating the count from the lock math;
            // RegisterFailure increments on every call regardless, so this targets purely the lost-update race.
            var guard = new LoginBackoffGuard(
                store,
                new LoginBackoffPolicy(Options.Create(new LoginBackoffOptions { FailureThreshold = 1000, FailureWindowSeconds = 600 })),
                TimeProvider.System);

            const string username = "concurrent-backoff-user";
            const int attempts = 50;

            // Release all failures at once against the same account; without an atomic compare-and-set the
            // racing read-modify-writes would clobber each other and the stored count would fall short.
            var ready = new TaskCompletionSource();
            var tasks = Enumerable.Range(0, attempts)
                .Select(_ => Task.Run(async () =>
                {
                    await ready.Task;
                    await guard.RegisterFailure(username, CancellationToken);
                }))
                .ToArray();
            ready.SetResult();
            await Task.WhenAll(tasks);

            var state = await store.Get(username, CancellationToken);
            Assert.NotNull(state);
            Assert.Equal(attempts, state.FailureCount);
        }

        [Fact]
        public async Task RegisterFailure_SequentialAttempts_IncrementsCountEachTime()
        {
            using var scope = CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ILoginBackoffStore>();
            var guard = new LoginBackoffGuard(
                store,
                new LoginBackoffPolicy(Options.Create(new LoginBackoffOptions { FailureThreshold = 1000, FailureWindowSeconds = 600 })),
                TimeProvider.System);

            const string username = "sequential-backoff-user";

            // The first failure compare-and-sets from an absent key; each subsequent one swaps the prior state.
            await guard.RegisterFailure(username, CancellationToken);
            await guard.RegisterFailure(username, CancellationToken);
            await guard.RegisterFailure(username, CancellationToken);

            var state = await store.Get(username, CancellationToken);
            Assert.NotNull(state);
            Assert.Equal(3, state.FailureCount);
        }
    }
}
