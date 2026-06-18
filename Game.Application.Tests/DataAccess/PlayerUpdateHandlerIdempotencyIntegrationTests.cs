using Game.Core;
using Game.Core.Players.Events;
using Game.Core.Progress;
using Game.DataAccess;
using Game.DataAccess.PlayerUpdates;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Verifies the write-behind upsert handlers stay idempotent under the queue's at-least-once read (#891):
    /// re-applying the same event — sequentially or as a concurrent double-apply across instances — converges
    /// to a single row without surfacing a unique-constraint violation. Each apply runs through its own DI
    /// scope (its own <see cref="GameContext"/>), mirroring the synchronizer's per-event scope, and the
    /// concurrent variants provoke the race the non-atomic existence-check-then-insert exposes.
    /// </summary>
    [Collection("Integration")]
    public class PlayerUpdateHandlerIdempotencyIntegrationTests : ApplicationIntegrationTestBase
    {
        // Enough concurrent applies that several pass the existence check before any insert commits, so the
        // unique-violation catch path is exercised rather than only the fast existence-check no-op.
        private const int Parallelism = 8;

        public PlayerUpdateHandlerIdempotencyIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task ItemUnlocked_AppliedTwiceSequentially_InsertsOneRow()
        {
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();
            var evt = new ItemUnlockedEvent(playerId, itemId);

            await ApplyAsync(evt);
            await ApplyAsync(evt);

            Assert.Equal(1, await CountAsync(c => c.UnlockedItems.CountAsync(ui => ui.PlayerId == playerId && ui.ItemId == itemId, CancellationToken)));
        }

        [Fact]
        public async Task ItemUnlocked_AppliedConcurrently_InsertsOneRowWithoutThrowing()
        {
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            await ApplyConcurrentlyAsync(new ItemUnlockedEvent(playerId, itemId));

            Assert.Equal(1, await CountAsync(c => c.UnlockedItems.CountAsync(ui => ui.PlayerId == playerId && ui.ItemId == itemId, CancellationToken)));
        }

        [Fact]
        public async Task SkillUnlocked_AppliedConcurrently_InsertsOneRowWithoutThrowing()
        {
            var playerId = await SeedPlayerAsync();
            var skillId = (await SeedSkillAsync());

            await ApplyConcurrentlyAsync(new SkillUnlockedEvent(playerId, skillId));

            Assert.Equal(1, await CountAsync(c => c.PlayerSkills.CountAsync(ps => ps.PlayerId == playerId && ps.SkillId == skillId, CancellationToken)));
        }

        [Fact]
        public async Task ModUnlocked_AppliedConcurrently_InsertsOneRowWithoutThrowing()
        {
            var playerId = await SeedPlayerAsync();
            var modId = await SeedModAsync();

            await ApplyConcurrentlyAsync(new ModUnlockedEvent(playerId, modId));

            Assert.Equal(1, await CountAsync(c => c.UnlockedMods.CountAsync(um => um.PlayerId == playerId && um.ItemModId == modId, CancellationToken)));
        }

        [Fact]
        public async Task LogPreferenceChanged_InsertsThenConvergesToLatestValue()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: false));
            await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: true));

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await context.LogPreferences.AsNoTracking()
                .Where(lp => lp.PlayerId == playerId && lp.LogTypeId == (int)ELogType.Damage)
                .ToListAsync(CancellationToken));
            Assert.True(row.Enabled);
        }

        [Fact]
        public async Task LogPreferenceChanged_AppliedConcurrently_InsertsOneRowWithoutThrowing()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyConcurrentlyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: true));

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await context.LogPreferences.AsNoTracking()
                .Where(lp => lp.PlayerId == playerId && lp.LogTypeId == (int)ELogType.Damage)
                .ToListAsync(CancellationToken));
            Assert.True(row.Enabled);
        }

        [Fact]
        public async Task ProgressUpdated_AppliedTwice_UpsertsStatisticsAndChallengesWithoutDuplicating()
        {
            var playerId = await SeedPlayerAsync();
            int challengeId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                challengeId = (await TestDataSeeder.CreateChallengeAsync(context)).Id;
            }

            var first = MakeProgressEvent(playerId, challengeId, statValue: 5m, challengeProgress: 5m, completed: false);
            await ApplyAsync(first);

            // The second apply carries higher absolute values: the existing rows are updated in place, not
            // duplicated, so progress converges to the latest snapshot.
            var second = MakeProgressEvent(playerId, challengeId, statValue: 9m, challengeProgress: 9m, completed: true);
            await ApplyAsync(second);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var stat = Assert.Single(await verifyContext.PlayerStatistics.AsNoTracking()
                .Where(ps => ps.PlayerId == playerId && ps.StatisticTypeId == (int)EStatisticType.EnemiesKilled)
                .ToListAsync(CancellationToken));
            Assert.Equal(9m, stat.Value);

            var challenge = Assert.Single(await verifyContext.PlayerChallenges.AsNoTracking()
                .Where(pc => pc.PlayerId == playerId && pc.ChallengeId == challengeId)
                .ToListAsync(CancellationToken));
            Assert.Equal(9m, challenge.Progress);
            Assert.True(challenge.Completed);
        }

        [Fact]
        public async Task ProgressUpdated_AppliedConcurrently_InsertsOneRowPerKeyWithoutThrowing()
        {
            var playerId = await SeedPlayerAsync();
            int challengeId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                challengeId = (await TestDataSeeder.CreateChallengeAsync(context)).Id;
            }

            await ApplyConcurrentlyAsync(MakeProgressEvent(playerId, challengeId, statValue: 7m, challengeProgress: 7m, completed: false));

            Assert.Equal(1, await CountAsync(c => c.PlayerStatistics.CountAsync(ps => ps.PlayerId == playerId && ps.StatisticTypeId == (int)EStatisticType.EnemiesKilled, CancellationToken)));
            Assert.Equal(1, await CountAsync(c => c.PlayerChallenges.CountAsync(pc => pc.PlayerId == playerId && pc.ChallengeId == challengeId, CancellationToken)));
        }

        [Fact]
        public async Task AttributeAllocationsChanged_AppliedTwice_UpsertsAllocationsWithoutDuplicating()
        {
            var playerId = await SeedPlayerAsync();

            // Intellect has no seeded row (insert), Strength does (update). The second apply carries different
            // amounts: the existing rows are updated in place, not duplicated, so allocations converge.
            await ApplyAsync(MakeAttributeEvent(playerId, intellect: 10d, strength: 20d));
            await ApplyAsync(MakeAttributeEvent(playerId, intellect: 30d, strength: 40d));

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var intellect = Assert.Single(await context.PlayerAttributes.AsNoTracking()
                .Where(pa => pa.PlayerId == playerId && pa.AttributeId == (int)EAttribute.Intellect)
                .ToListAsync(CancellationToken));
            Assert.Equal(30m, intellect.Amount);
            var strength = Assert.Single(await context.PlayerAttributes.AsNoTracking()
                .Where(pa => pa.PlayerId == playerId && pa.AttributeId == (int)EAttribute.Strength)
                .ToListAsync(CancellationToken));
            Assert.Equal(40m, strength.Amount);
        }

        [Fact]
        public async Task AttributeAllocationsChanged_AppliedConcurrently_InsertsOneRowPerAttributeWithoutThrowing()
        {
            var playerId = await SeedPlayerAsync();

            // Intellect starts with no row, so the concurrent applies race on its insert; the clear-and-re-apply
            // path resolves the conflict to an update on the second pass rather than throwing.
            await ApplyConcurrentlyAsync(MakeAttributeEvent(playerId, intellect: 15d, strength: 25d));

            Assert.Equal(1, await CountAsync(c => c.PlayerAttributes.CountAsync(pa => pa.PlayerId == playerId && pa.AttributeId == (int)EAttribute.Intellect, CancellationToken)));
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var intellect = Assert.Single(await context.PlayerAttributes.AsNoTracking()
                .Where(pa => pa.PlayerId == playerId && pa.AttributeId == (int)EAttribute.Intellect)
                .ToListAsync(CancellationToken));
            Assert.Equal(15m, intellect.Amount);
        }

        [Fact]
        public async Task ModApplied_AppliedTwice_ConvergesToOneRowWithLatestMod()
        {
            var (playerId, itemId, slotId, firstModId) = await SeedAppliedModFixtureAsync();
            int secondModId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                secondModId = (await TestDataSeeder.CreateItemModAsync(context, name: "Second Mod")).Id;
            }

            // The second apply swaps the slot's mod: the delete-then-insert replaces the row in place rather
            // than duplicating, so the slot converges to the latest mod.
            await ApplyAsync(new ModAppliedEvent(playerId, itemId, slotId, firstModId));
            await ApplyAsync(new ModAppliedEvent(playerId, itemId, slotId, secondModId));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await verifyContext.AppliedMods.AsNoTracking()
                .Where(am => am.PlayerId == playerId && am.ItemId == itemId && am.ItemModSlotId == slotId)
                .ToListAsync(CancellationToken));
            Assert.Equal(secondModId, row.ItemModId);
        }

        [Fact]
        public async Task ModApplied_AppliedConcurrently_InsertsOneRowWithoutThrowing()
        {
            var (playerId, itemId, slotId, modId) = await SeedAppliedModFixtureAsync();

            await ApplyConcurrentlyAsync(new ModAppliedEvent(playerId, itemId, slotId, modId));

            Assert.Equal(1, await CountAsync(c => c.AppliedMods.CountAsync(am => am.PlayerId == playerId && am.ItemId == itemId && am.ItemModSlotId == slotId, CancellationToken)));
        }

        private static AttributeAllocationsChangedEvent MakeAttributeEvent(int playerId, double intellect, double strength) => new(
            playerId,
            [
                new AttributeAllocationEntry(EAttribute.Intellect, intellect),
                new AttributeAllocationEntry(EAttribute.Strength, strength),
            ]);

        // Seeds an item with a mod slot plus a mod the player has unlocked, the prerequisites a ModAppliedEvent
        // assumes already exist.
        private async Task<(int playerId, int itemId, int slotId, int modId)> SeedAppliedModFixtureAsync()
        {
            var playerId = await SeedPlayerAsync();
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var itemId = (await TestDataSeeder.CreateItemAsync(context)).Id;
            var slotId = (await TestDataSeeder.AddItemModSlotAsync(context, itemId)).Id;
            var modId = (await TestDataSeeder.CreateItemModAsync(context)).Id;
            await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, itemId);
            await TestDataSeeder.LinkModToPlayerAsync(context, playerId, modId);
            return (playerId, itemId, slotId, modId);
        }

        private static ProgressUpdatedEvent MakeProgressEvent(int playerId, int challengeId, decimal statValue, decimal challengeProgress, bool completed) => new()
        {
            PlayerId = playerId,
            Statistics = [new CachedPlayerStatistic { StatisticTypeId = (int)EStatisticType.EnemiesKilled, EntityId = null, Value = statValue }],
            Challenges = [new CachedPlayerChallenge { ChallengeId = challengeId, Progress = challengeProgress, Completed = completed, CompletedAt = completed ? DateTime.UtcNow : null }],
        };

        private async Task ApplyAsync<TEvent>(TEvent evt)
        {
            using var scope = CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IPlayerUpdateHandler<TEvent>>();
            await handler.HandleAsync(evt);
        }

        // Runs the same event through several independent scopes at once so multiple applies pass the
        // existence check before any insert commits — the cross-instance double-apply the catch absorbs.
        private async Task ApplyConcurrentlyAsync<TEvent>(TEvent evt)
        {
            var scopes = Enumerable.Range(0, Parallelism).Select(_ => CreateScope()).ToList();
            try
            {
                var handlers = scopes.Select(s => s.ServiceProvider.GetRequiredService<IPlayerUpdateHandler<TEvent>>()).ToList();
                await Task.WhenAll(handlers.Select(h => Task.Run(() => h.HandleAsync(evt))));
            }
            finally
            {
                foreach (var scope in scopes)
                {
                    scope.Dispose();
                }
            }
        }

        private async Task<int> CountAsync(Func<GameContext, Task<int>> count)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return await count(context);
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

        private async Task<int> SeedSkillAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return (await TestDataSeeder.CreateSkillAsync(context)).Id;
        }

        private async Task<int> SeedModAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return (await TestDataSeeder.CreateItemModAsync(context)).Id;
        }
    }
}
