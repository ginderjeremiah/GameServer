using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.DataAccess;
using Game.DataAccess.Repositories.Caching;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Covers the Redis pub/sub glue of <see cref="ReferenceCacheSynchronizer"/>: a notification published
    /// by another instance triggers a background reload of the local snapshot holders, while the instance's
    /// own notification is skipped (its caches were already reloaded synchronously by the admin filter).
    /// The coalescing/retry behaviour behind the subscription is unit-tested in
    /// <see cref="CoalescingReferenceCacheReloaderTests"/>.
    /// </summary>
    [Collection("Integration")]
    public class ReferenceCacheSynchronizerTests : ApplicationIntegrationTestBase
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(15);

        public ReferenceCacheSynchronizerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task ForeignNotification_ReloadsTheCachesInTheBackground()
        {
            int itemId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                itemId = (await TestDataSeeder.CreateItemAsync(context, name: "Before")).Id;
            }

            await ReloadReferenceCachesAsync();

            // Rename the item directly in the database — the stand-in for another instance's committed
            // admin write. The local snapshot keeps serving the old name until a reload runs.
            using (var updateScope = CreateScope())
            {
                var context = updateScope.ServiceProvider.GetRequiredService<GameContext>();
                await context.Items
                    .Where(i => i.Id == itemId)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.Name, "After"), CancellationToken);
            }

            Assert.Equal("Before", GetCachedItemName(itemId));

            using var scope = CreateScope();
            // Resolved through DI so the test exercises the production registrations (policy, reloader, holders).
            var synchronizer = scope.ServiceProvider.GetRequiredService<ReferenceCacheSynchronizer>();
            await synchronizer.StartAsync(CancellationToken);
            try
            {
                // A foreign publisher id on the payload, so the message must not be skipped.
                var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
                await pubsub.Publish(Constants.PUBSUB_REFERENCE_DATA_CHANNEL, Guid.NewGuid().ToString());

                await WaitUntilAsync(() => GetCachedItemName(itemId) == "After", "the caches to serve the updated item");
            }
            finally
            {
                await synchronizer.StopAsync(CancellationToken.None);
            }
        }

        [Fact]
        public async Task OwnNotification_IsSkipped()
        {
            using var scope = CreateScope();
            var holder = scope.ServiceProvider.GetRequiredService<ItemsCacheHolder>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();

            // Built manually (rather than resolved) for a short debounce, so the negative wait below stays
            // brief; it still wraps the DI holders, so a skipped reload is observable on the real snapshots.
            var policy = new ReferenceCacheReloadPolicy(TimeSpan.FromMilliseconds(50), maxAttempts: 1, baseDelay: TimeSpan.Zero);
            var reloader = new CoalescingReferenceCacheReloader(
                scope.ServiceProvider.GetServices<IReloadableReferenceCache>(),
                policy,
                scope.ServiceProvider.GetRequiredService<ILogger<CoalescingReferenceCacheReloader>>());
            using var synchronizer = new ReferenceCacheSynchronizer(pubsub, reloader);

            await synchronizer.StartAsync(CancellationToken);
            try
            {
                // The instance's own notification must not reload: a reload always publishes a fresh
                // snapshot object, so an unchanged reference proves no reload ran.
                var snapshotBeforeOwnMessage = holder.Current;
                await synchronizer.NotifyChangedAsync();
                await Task.Delay(policy.DebounceWindow * 10, CancellationToken);
                Assert.Same(snapshotBeforeOwnMessage, holder.Current);

                // Positive control: a foreign notification through the same live subscription does reload,
                // proving the wait above was long enough to have observed one.
                await pubsub.Publish(Constants.PUBSUB_REFERENCE_DATA_CHANNEL, Guid.NewGuid().ToString());
                await WaitUntilAsync(() => !ReferenceEquals(snapshotBeforeOwnMessage, holder.Current), "the foreign notification to reload");
            }
            finally
            {
                await synchronizer.StopAsync(CancellationToken.None);
            }
        }

        private string? GetCachedItemName(int itemId)
        {
            using var scope = CreateScope();
            var items = scope.ServiceProvider.GetRequiredService<IItems>();
            return items.All().FirstOrDefault(i => i.Id == itemId)?.Name;
        }

        private async Task WaitUntilAsync(Func<bool> condition, string description)
        {
            var deadline = DateTime.UtcNow + WaitTimeout;
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    Assert.Fail($"Timed out waiting for {description}.");
                }

                await Task.Delay(25, CancellationToken);
            }
        }
    }
}
