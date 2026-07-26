using Game.Core;
using Game.Core.Players.Events;
using Game.DataAccess;
using Game.DataAccess.PlayerUpdates;
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
    /// Verifies the <see cref="PlayerWriteWatermark"/> guard on the absolute-value write-behind handlers
    /// (#2474): a write older than what its target already holds is skipped instead of durably regressing the
    /// row, equal sequences still apply, and an unsequenced envelope bypasses the comparison entirely. Each
    /// apply runs through its own DI scope (its own <see cref="GameContext"/>), mirroring the synchronizer's
    /// per-event scope, with the envelope's sequence published onto the scope the way the dispatcher does.
    /// </summary>
    [Collection("Integration")]
    public class PlayerWriteWatermarkIntegrationTests : ApplicationIntegrationTestBase
    {
        // TestDataSeeder.CreatePlayerAsync's default, asserted where a rolled-back apply must leave the row as seeded.
        private const int SeededLevel = 5;

        public PlayerWriteWatermarkIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task PlayerCoreUpdated_FirstWriteWithNoWatermarkRow_AppliesAndSeedsTheWatermark()
        {
            var playerId = await SeedPlayerAsync();

            var rejected = await ApplyAsync(CoreEvent(playerId, level: 7, exp: 700), sequence: 3);

            Assert.Equal(0, rejected);
            await AssertPlayerAsync(playerId, level: 7, exp: 700);
            Assert.Equal(3, await ReadWatermarkAsync(playerId, PlayerWriteStream.PlayerCore, PlayerWriteWatermarkGuard.PlayerScopedTarget));
        }

        [Fact]
        public async Task PlayerCoreUpdated_OlderSequenceAfterNewer_IsSkippedAndLeavesTheWatermark()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(CoreEvent(playerId, level: 11, exp: 160), sequence: 2);
            var rejected = await ApplyAsync(CoreEvent(playerId, level: 9, exp: 100), sequence: 1);

            // This is the #2467 regression in miniature: the older event is the one a reclaim replays, and
            // applying it would durably regress the player past the newer save Redis already holds.
            Assert.Equal(1, rejected);
            await AssertPlayerAsync(playerId, level: 11, exp: 160);
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.PlayerCore, PlayerWriteWatermarkGuard.PlayerScopedTarget));
        }

        [Fact]
        public async Task PlayerCoreUpdated_EqualSequence_Applies()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(CoreEvent(playerId, level: 11, exp: 160), sequence: 4);
            // Equal sequences are the same save's siblings (and the at-least-once duplicate re-apply), so the
            // predicate is <=, not <. Under < this second write would be silently skipped.
            var rejected = await ApplyAsync(CoreEvent(playerId, level: 12, exp: 170), sequence: 4);

            Assert.Equal(0, rejected);
            await AssertPlayerAsync(playerId, level: 12, exp: 170);
        }

        [Fact]
        public async Task PlayerCoreUpdated_NewerSequence_AppliesAndAdvancesTheWatermark()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(CoreEvent(playerId, level: 9, exp: 100), sequence: 1);
            var rejected = await ApplyAsync(CoreEvent(playerId, level: 11, exp: 160), sequence: 2);

            Assert.Equal(0, rejected);
            await AssertPlayerAsync(playerId, level: 11, exp: 160);
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.PlayerCore, PlayerWriteWatermarkGuard.PlayerScopedTarget));
        }

        [Fact]
        public async Task PlayerCoreUpdated_UnsequencedEvent_AppliesAgainstAnAdvancedWatermarkAndLeavesItUnchanged()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(CoreEvent(playerId, level: 11, exp: 160), sequence: 5);

            // Sequence 0 is the "unsequenced" sentinel a pre-upgrade instance's envelope deserializes to, not a
            // low sequence. Treating it as one would make a player who reconnects onto a pre-upgrade instance
            // mid-rolling-deploy lose every guarded write for that whole session — silently, as no-ops.
            var rejected = await ApplyAsync(CoreEvent(playerId, level: 3, exp: 30), sequence: DomainEventEnvelope.Unsequenced);

            Assert.Equal(0, rejected);
            await AssertPlayerAsync(playerId, level: 3, exp: 30);
            Assert.Equal(5, await ReadWatermarkAsync(playerId, PlayerWriteStream.PlayerCore, PlayerWriteWatermarkGuard.PlayerScopedTarget));
        }

        [Fact]
        public async Task ProgressUpdated_OlderEventCarryingADifferentStatistic_AppliesThatRowAndSkipsOnlyTheSuperseded()
        {
            var playerId = await SeedPlayerAsync();
            var enemyId = await SeedEnemyAsync();

            // The newer save dirtied only the global kill count.
            await ApplyAsync(new ProgressUpdatedEvent
            {
                PlayerId = playerId,
                Statistics = [GlobalKills(50)],
            }, sequence: 2);

            // The older save dirtied that same statistic *and* a per-enemy one. A per-player watermark would
            // discard both; only the superseded global row may be skipped. Progress events carry only a save's
            // dirty rows, so collapsing this granularity would silently lose writes on the hottest path.
            var rejected = await ApplyAsync(new ProgressUpdatedEvent
            {
                PlayerId = playerId,
                Statistics = [GlobalKills(10), PerEnemyKills(enemyId, 4)],
            }, sequence: 1);

            Assert.Equal(1, rejected);
            Assert.Equal(50, await ReadStatisticAsync(playerId, EStatisticType.EnemiesKilled, null));
            Assert.Equal(4, await ReadStatisticAsync(playerId, EStatisticType.EnemiesKilled, enemyId));
        }

        [Fact]
        public async Task ProgressUpdated_OlderChallengeAndProficiencyRows_AreGuardedIndependentlyOfStatistics()
        {
            var playerId = await SeedPlayerAsync();
            var challengeId = await SeedChallengeAsync();
            var proficiencyId = await SeedProficiencyAsync();

            await ApplyAsync(new ProgressUpdatedEvent
            {
                PlayerId = playerId,
                Challenges = [Challenge(challengeId, progress: 8)],
            }, sequence: 2);

            var rejected = await ApplyAsync(new ProgressUpdatedEvent
            {
                PlayerId = playerId,
                Challenges = [Challenge(challengeId, progress: 3)],
                Proficiencies = [Proficiency(proficiencyId, level: 2, xp: 40)],
            }, sequence: 1);

            Assert.Equal(1, rejected);
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var challenge = await context.PlayerChallenges.AsNoTracking()
                .SingleAsync(pc => pc.PlayerId == playerId && pc.ChallengeId == challengeId, CancellationToken);
            Assert.Equal(8, challenge.Progress);
            var proficiency = await context.PlayerProficiencies.AsNoTracking()
                .SingleAsync(pp => pp.PlayerId == playerId && pp.ProficiencyId == proficiencyId, CancellationToken);
            Assert.Equal(2, proficiency.Level);
        }

        [Fact]
        public async Task GuardedStreams_AreIndependent_SoAProgressWriteDoesNotBlockAnOlderPlayerCoreWrite()
        {
            var playerId = await SeedPlayerAsync();

            // The two aggregates own separate counters and write disjoint tables, so their sequences are
            // separate spaces — a progress event must never be compared against the player row's watermark.
            await ApplyAsync(new ProgressUpdatedEvent
            {
                PlayerId = playerId,
                Statistics = [GlobalKills(50)],
            }, sequence: 9);

            var rejected = await ApplyAsync(CoreEvent(playerId, level: 6, exp: 60), sequence: 1);

            Assert.Equal(0, rejected);
            await AssertPlayerAsync(playerId, level: 6, exp: 60);
        }

        [Fact]
        public async Task GuardedApply_ThatFaultsAfterTheWatermarkAdvance_LeavesBothUnapplied()
        {
            var playerId = await SeedPlayerAsync();

            using (var scope = CreateScope())
            {
                DescribeSequence(scope, 4);
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var guard = scope.ServiceProvider.GetRequiredService<PlayerWriteWatermarkGuard>();

                // The watermark advance and the data write share one transaction precisely so this can't
                // half-land: an advanced watermark with no data would make the redelivered event look stale
                // and be skipped — a silently lost write, strictly worse than the bug the guard fixes.
                await Assert.ThrowsAsync<InvalidOperationException>(() => guard.ExecuteGuardedAsync(
                    playerId,
                    PlayerWriteStream.PlayerCore,
                    [PlayerWriteWatermarkGuard.PlayerScopedTarget],
                    async _ =>
                    {
                        await context.Players.Where(p => p.Id == playerId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Level, 99));
                        throw new InvalidOperationException("simulated failure between the watermark advance and the durable apply");
                    }));
            }

            Assert.Null(await ReadWatermarkAsync(playerId, PlayerWriteStream.PlayerCore, PlayerWriteWatermarkGuard.PlayerScopedTarget));
            await AssertPlayerAsync(playerId, level: SeededLevel, exp: 0);

            // Redelivery therefore still lands the write rather than being rejected as already applied.
            var rejected = await ApplyAsync(CoreEvent(playerId, level: 4, exp: 40), sequence: 4);
            Assert.Equal(0, rejected);
            await AssertPlayerAsync(playerId, level: 4, exp: 40);
        }

        private static PlayerCoreUpdatedEvent CoreEvent(int playerId, int level, int exp)
            => new(playerId, level, exp, 0, 100, 100, DateTime.UtcNow, false, null);

        private static CachedPlayerStatistic GlobalKills(decimal value) => new()
        {
            StatisticTypeId = (int)EStatisticType.EnemiesKilled,
            EntityId = null,
            Value = value,
        };

        private static CachedPlayerStatistic PerEnemyKills(int enemyId, decimal value) => new()
        {
            StatisticTypeId = (int)EStatisticType.EnemiesKilled,
            EntityId = enemyId,
            Value = value,
        };

        private static CachedPlayerChallenge Challenge(int challengeId, decimal progress) => new()
        {
            ChallengeId = challengeId,
            Progress = progress,
            Completed = false,
            CompletedAt = null,
        };

        private static CachedPlayerProficiency Proficiency(int proficiencyId, int level, decimal xp) => new()
        {
            ProficiencyId = proficiencyId,
            Level = level,
            Xp = xp,
        };

        /// <summary>
        /// Applies one event at <paramref name="sequence"/> through its own scope and returns how many of its
        /// targets the guard skipped — the count the synchronizer totals per drain pass.
        /// </summary>
        private async Task<int> ApplyAsync<TEvent>(TEvent evt, long sequence)
        {
            using var scope = CreateScope();
            DescribeSequence(scope, sequence);
            var handler = scope.ServiceProvider.GetRequiredService<IPlayerUpdateHandler<TEvent>>();
            await handler.HandleAsync(evt);
            return scope.ServiceProvider.GetRequiredService<PlayerUpdateContext>().RejectedTargetCount;
        }

        // Publishes the envelope's sequence onto the scope exactly as PlayerUpdateEventDispatcher does; the
        // payload itself is irrelevant here since the handler is invoked directly with a typed event.
        private static void DescribeSequence(IServiceScope scope, long sequence)
        {
            scope.ServiceProvider.GetRequiredService<PlayerUpdateContext>().Describe(new DomainEventEnvelope
            {
                Type = "test",
                Payload = "{}",
                Sequence = sequence,
            });
        }

        private async Task<long?> ReadWatermarkAsync(int playerId, PlayerWriteStream stream, string targetKey)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = await context.PlayerWriteWatermarks.AsNoTracking()
                .SingleOrDefaultAsync(w => w.PlayerId == playerId && w.Stream == stream && w.TargetKey == targetKey, CancellationToken);
            return row?.LastAppliedSequence;
        }

        private async Task<decimal?> ReadStatisticAsync(int playerId, EStatisticType type, int? entityId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = await context.PlayerStatistics.AsNoTracking()
                .SingleOrDefaultAsync(ps => ps.PlayerId == playerId && ps.StatisticTypeId == (int)type && ps.EntityId == entityId, CancellationToken);
            return row?.Value;
        }

        private async Task AssertPlayerAsync(int playerId, int level, int exp)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var player = await context.Players.AsNoTracking().SingleAsync(p => p.Id == playerId, CancellationToken);
            Assert.Equal(level, player.Level);
            Assert.Equal(exp, player.Exp);
        }

        private async Task<int> SeedPlayerAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            return player.Id;
        }

        private async Task<int> SeedEnemyAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return (await TestDataSeeder.CreateEnemyAsync(context)).Id;
        }

        private async Task<int> SeedChallengeAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return (await TestDataSeeder.CreateChallengeAsync(context)).Id;
        }

        private async Task<int> SeedProficiencyAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return (await TestDataSeeder.CreateProficiencyAsync(context)).Id;
        }
    }
}
