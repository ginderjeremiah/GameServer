using Game.DataAccess.PlayerUpdates.Handlers;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Verifies the shared <see cref="PlayerUpdateHandlerExtensions.InsertIfMissingAsync"/> contract once, at
    /// the helper level: the existence-check-then-insert is idempotent under the queue's at-least-once read,
    /// converging to a single row whether re-applied sequentially or as a concurrent double-apply that provokes
    /// the unique-violation race the catch absorbs. The collapsed unlock handlers all route through this helper,
    /// so their per-handler idempotency tests (see <see cref="PlayerUpdateHandlerIdempotencyIntegrationTests"/>)
    /// exercise the same path and pin the wiring. Exercised against <see cref="UnlockedItem"/> as a
    /// representative entity carrying a (player, item) unique key.
    /// </summary>
    [Collection("Integration")]
    public class InsertIfMissingIntegrationTests : ApplicationIntegrationTestBase
    {
        // Enough concurrent inserts that several pass the existence check before any insert commits, so the
        // unique-violation catch path is exercised rather than only the fast existence-check no-op.
        private const int Parallelism = 8;

        public InsertIfMissingIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task RowMissing_InsertsTheRow()
        {
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            await InsertUnlockedItemAsync(playerId, itemId);

            Assert.Equal(1, await CountUnlockedItemsAsync(playerId, itemId));
        }

        [Fact]
        public async Task RowAlreadyPresent_DoesNotInsertOrInvokeFactoryTwice()
        {
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            await InsertUnlockedItemAsync(playerId, itemId);

            // The second call must short-circuit on the existence check and never run the factory.
            var factoryInvoked = false;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await context.InsertIfMissingAsync(
                    (UnlockedItem ui) => ui.PlayerId == playerId && ui.ItemId == itemId,
                    () =>
                    {
                        factoryInvoked = true;
                        return new UnlockedItem { PlayerId = playerId, ItemId = itemId };
                    });
            }

            Assert.False(factoryInvoked);
            Assert.Equal(1, await CountUnlockedItemsAsync(playerId, itemId));
        }

        [Fact]
        public async Task AppliedConcurrently_InsertsOneRowWithoutThrowing()
        {
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            // Each insert runs through its own scope/context, mirroring the synchronizer's per-event scope, so
            // several pass the existence check before any save commits — the cross-instance double-apply race.
            var scopes = Enumerable.Range(0, Parallelism).Select(_ => CreateScope()).ToList();
            try
            {
                var tasks = scopes.Select(s => Task.Run(() =>
                {
                    var context = s.ServiceProvider.GetRequiredService<GameContext>();
                    return context.InsertIfMissingAsync(
                        (UnlockedItem ui) => ui.PlayerId == playerId && ui.ItemId == itemId,
                        () => new UnlockedItem { PlayerId = playerId, ItemId = itemId });
                }));

                // No throw despite the concurrent inserts racing on the (player, item) unique key.
                await Task.WhenAll(tasks);
            }
            finally
            {
                foreach (var scope in scopes)
                {
                    scope.Dispose();
                }
            }

            Assert.Equal(1, await CountUnlockedItemsAsync(playerId, itemId));
        }

        private async Task InsertUnlockedItemAsync(int playerId, int itemId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await context.InsertIfMissingAsync(
                (UnlockedItem ui) => ui.PlayerId == playerId && ui.ItemId == itemId,
                () => new UnlockedItem { PlayerId = playerId, ItemId = itemId });
        }

        private async Task<int> CountUnlockedItemsAsync(int playerId, int itemId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return await context.UnlockedItems
                .CountAsync(ui => ui.PlayerId == playerId && ui.ItemId == itemId, CancellationToken);
        }

        private async Task<int> SeedPlayerAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            return player.Id;
        }

        private async Task<int> SeedItemAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return (await TestDataSeeder.CreateItemAsync(context)).Id;
        }
    }
}
