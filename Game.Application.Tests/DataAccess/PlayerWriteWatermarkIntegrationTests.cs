using System.Globalization;
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
    /// (#2474, #2496): a write older than what its target already holds is skipped instead of durably regressing the
    /// row, equal sequences still apply, and an unsequenced envelope bypasses the comparison entirely. Each
    /// apply runs through its own DI scope (its own <see cref="GameContext"/>), mirroring the synchronizer's
    /// per-event scope, with the envelope's sequence published onto the scope the way the dispatcher does.
    /// </summary>
    [Collection("Integration")]
    public class PlayerWriteWatermarkIntegrationTests : ApplicationIntegrationTestBase
    {
        // TestDataSeeder.CreatePlayerAsync's default, asserted where a rolled-back apply must leave the row as seeded.
        private const int SeededLevel = 5;

        // Fixed so a lesson's ReadAt is asserted as an offset from a known instant rather than from "now".
        private static readonly DateTime LessonUnlockedAt = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);

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
                var guard = scope.ServiceProvider.GetRequiredService<PlayerWriteWatermarkGuard>();

                // The watermark advance and the data write share one transaction precisely so this can't
                // half-land: an advanced watermark with no data would make the redelivered event look stale
                // and be skipped — a silently lost write, strictly worse than the bug the guard fixes.
                await Assert.ThrowsAsync<InvalidOperationException>(() => guard.ExecuteGuardedAsync(
                    playerId,
                    PlayerWriteStream.PlayerCore,
                    [PlayerWriteWatermarkGuard.PlayerScopedTarget],
                    async (context, _) =>
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

        [Fact]
        public async Task GuardedApply_UniqueViolationInsideTheTransaction_RestartsTheWholeAttemptAndLandsTheWrite()
        {
            var playerId = await SeedPlayerAsync();
            var target = $"stat:{(int)EStatisticType.EnemiesKilled}:";
            var attempts = 0;

            using (var scope = CreateScope())
            {
                DescribeSequence(scope, 4);
                var guard = scope.ServiceProvider.GetRequiredService<PlayerWriteWatermarkGuard>();

                // Mirrors ProgressUpdatedHandler's load-then-upsert shape, with the concurrent insert forced
                // deterministically rather than raced for. A DbUpdateException aborts the surrounding
                // transaction, so re-running only the apply would save into a transaction that can no longer
                // commit — the restart has to unwind and redo the watermark advance too.
                await guard.ExecuteGuardedAsync(playerId, PlayerWriteStream.Progress, [target], async (context, accepted) =>
                {
                    Assert.Contains(target, accepted);
                    attempts++;

                    var existing = await context.PlayerStatistics
                        .Where(ps => ps.PlayerId == playerId && ps.StatisticTypeId == (int)EStatisticType.EnemiesKilled && ps.EntityId == null)
                        .ToListAsync(CancellationToken);

                    if (attempts == 1)
                    {
                        // Another instance applying an *unsequenced* envelope bypasses the guard, so it isn't
                        // serialized by this watermark row and can land between the load and the save — the
                        // one interleaving that still produces a unique violation now that same-target
                        // sequenced applies queue behind each other on the watermark.
                        await InsertGlobalKillsFromAnotherScopeAsync(playerId, value: 7);
                    }

                    if (existing.Count > 0)
                    {
                        existing[0].Value = 50;
                    }
                    else
                    {
                        context.PlayerStatistics.Add(new PlayerStatistic
                        {
                            PlayerId = playerId,
                            StatisticTypeId = (int)EStatisticType.EnemiesKilled,
                            EntityId = null,
                            Value = 50,
                        });
                    }

                    await context.SaveChangesAsync(CancellationToken);
                });
            }

            // The second attempt loaded the now-existing row as an update and committed, and the watermark
            // ends at the event's sequence — neither left behind by the rollback nor double-advanced.
            Assert.Equal(2, attempts);
            Assert.Equal(50, await ReadStatisticAsync(playerId, EStatisticType.EnemiesKilled, null));
            Assert.Equal(4, await ReadWatermarkAsync(playerId, PlayerWriteStream.Progress, target));
        }

        [Fact]
        public async Task AttributeAllocationsChanged_OlderSequenceAfterNewer_IsSkippedAndLeavesTheWatermark()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(AllocationEvent(playerId, strength: 20d), sequence: 2);
            var rejected = await ApplyAsync(AllocationEvent(playerId, strength: 5d), sequence: 1);

            // The consequential one: a stale apply reverts spent stat points while the player row's
            // StatPointsUsed may already hold the newer value, leaving the two disagreeing.
            Assert.Equal(1, rejected);
            Assert.Equal(20m, await ReadAllocationAsync(playerId, EAttribute.Strength));
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.AttributeAllocations, PlayerWriteWatermarkGuard.PlayerScopedTarget));
        }

        [Fact]
        public async Task AttributeAllocationsChanged_EqualSequence_Applies()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(AllocationEvent(playerId, strength: 20d), sequence: 3);
            var rejected = await ApplyAsync(AllocationEvent(playerId, strength: 21d), sequence: 3);

            Assert.Equal(0, rejected);
            Assert.Equal(21m, await ReadAllocationAsync(playerId, EAttribute.Strength));
        }

        [Fact]
        public async Task AttributeAllocationsChanged_NewerSequence_AppliesAndAdvancesTheWatermark()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(AllocationEvent(playerId, strength: 5d), sequence: 1);
            var rejected = await ApplyAsync(AllocationEvent(playerId, strength: 20d), sequence: 2);

            Assert.Equal(0, rejected);
            Assert.Equal(20m, await ReadAllocationAsync(playerId, EAttribute.Strength));
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.AttributeAllocations, PlayerWriteWatermarkGuard.PlayerScopedTarget));
        }

        [Fact]
        public async Task AttributeAllocationsChanged_UnsequencedEvent_AppliesAgainstAnAdvancedWatermarkAndLeavesItUnchanged()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(AllocationEvent(playerId, strength: 20d), sequence: 5);
            var rejected = await ApplyAsync(AllocationEvent(playerId, strength: 3d), sequence: DomainEventEnvelope.Unsequenced);

            Assert.Equal(0, rejected);
            Assert.Equal(3m, await ReadAllocationAsync(playerId, EAttribute.Strength));
            Assert.Equal(5, await ReadWatermarkAsync(playerId, PlayerWriteStream.AttributeAllocations, PlayerWriteWatermarkGuard.PlayerScopedTarget));
        }

        [Fact]
        public async Task SelectedSkillsChanged_OlderSequenceAfterNewer_IsSkippedAndLeavesTheWatermark()
        {
            var playerId = await SeedPlayerAsync();
            var firstSkillId = await SeedSkillAsync();
            var secondSkillId = await SeedSkillAsync();

            await ApplyAsync(new SelectedSkillsChangedEvent(playerId, [secondSkillId]), sequence: 2);
            var rejected = await ApplyAsync(new SelectedSkillsChangedEvent(playerId, [firstSkillId]), sequence: 1);

            // The whole loadout is one target: applying the older rebuild would restore a loadout the player
            // has already replaced, deselecting the skill the newer event equipped.
            Assert.Equal(1, rejected);
            Assert.Equal([secondSkillId], await ReadSelectedSkillIdsAsync(playerId));
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.SelectedSkills, PlayerWriteWatermarkGuard.PlayerScopedTarget));
        }

        [Fact]
        public async Task SelectedSkillsChanged_EqualSequence_Applies()
        {
            var playerId = await SeedPlayerAsync();
            var firstSkillId = await SeedSkillAsync();
            var secondSkillId = await SeedSkillAsync();

            await ApplyAsync(new SelectedSkillsChangedEvent(playerId, [firstSkillId]), sequence: 3);
            var rejected = await ApplyAsync(new SelectedSkillsChangedEvent(playerId, [firstSkillId, secondSkillId]), sequence: 3);

            Assert.Equal(0, rejected);
            Assert.Equal([firstSkillId, secondSkillId], await ReadSelectedSkillIdsAsync(playerId));
        }

        [Fact]
        public async Task SelectedSkillsChanged_NewerSequence_AppliesAndAdvancesTheWatermark()
        {
            var playerId = await SeedPlayerAsync();
            var firstSkillId = await SeedSkillAsync();
            var secondSkillId = await SeedSkillAsync();

            await ApplyAsync(new SelectedSkillsChangedEvent(playerId, [firstSkillId]), sequence: 1);
            var rejected = await ApplyAsync(new SelectedSkillsChangedEvent(playerId, [secondSkillId]), sequence: 2);

            Assert.Equal(0, rejected);
            Assert.Equal([secondSkillId], await ReadSelectedSkillIdsAsync(playerId));
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.SelectedSkills, PlayerWriteWatermarkGuard.PlayerScopedTarget));
        }

        [Fact]
        public async Task SelectedSkillsChanged_UnsequencedEvent_AppliesAgainstAnAdvancedWatermarkAndLeavesItUnchanged()
        {
            var playerId = await SeedPlayerAsync();
            var firstSkillId = await SeedSkillAsync();
            var secondSkillId = await SeedSkillAsync();

            await ApplyAsync(new SelectedSkillsChangedEvent(playerId, [secondSkillId]), sequence: 5);
            var rejected = await ApplyAsync(new SelectedSkillsChangedEvent(playerId, [firstSkillId]), sequence: DomainEventEnvelope.Unsequenced);

            Assert.Equal(0, rejected);
            Assert.Equal([firstSkillId], await ReadSelectedSkillIdsAsync(playerId));
            Assert.Equal(5, await ReadWatermarkAsync(playerId, PlayerWriteStream.SelectedSkills, PlayerWriteWatermarkGuard.PlayerScopedTarget));
        }

        [Fact]
        public async Task LogPreferenceChanged_OlderSequenceAfterNewer_IsSkippedAndLeavesTheWatermark()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: false), sequence: 2);
            var rejected = await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: true), sequence: 1);

            Assert.Equal(1, rejected);
            Assert.False(await ReadLogPreferenceAsync(playerId, ELogType.Damage));
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.LogPreference, LogTarget(ELogType.Damage)));
        }

        [Fact]
        public async Task LogPreferenceChanged_EqualSequence_Applies()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: false), sequence: 3);
            var rejected = await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: true), sequence: 3);

            Assert.Equal(0, rejected);
            Assert.True(await ReadLogPreferenceAsync(playerId, ELogType.Damage));
        }

        [Fact]
        public async Task LogPreferenceChanged_NewerSequence_AppliesAndAdvancesTheWatermark()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: true), sequence: 1);
            var rejected = await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: false), sequence: 2);

            Assert.Equal(0, rejected);
            Assert.False(await ReadLogPreferenceAsync(playerId, ELogType.Damage));
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.LogPreference, LogTarget(ELogType.Damage)));
        }

        [Fact]
        public async Task LogPreferenceChanged_UnsequencedEvent_AppliesAgainstAnAdvancedWatermarkAndLeavesItUnchanged()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: false), sequence: 5);
            var rejected = await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: true), sequence: DomainEventEnvelope.Unsequenced);

            Assert.Equal(0, rejected);
            Assert.True(await ReadLogPreferenceAsync(playerId, ELogType.Damage));
            Assert.Equal(5, await ReadWatermarkAsync(playerId, PlayerWriteStream.LogPreference, LogTarget(ELogType.Damage)));
        }

        [Fact]
        public async Task LogPreferenceChanged_OlderEventForADifferentLogType_IsNotRejectedByTheNewerOne()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: false), sequence: 2);
            var rejected = await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Exp, Enabled: false), sequence: 1);

            // Each event carries one log type's flag, so the key has to be per type: a per-player watermark
            // would discard this still-current change to Exp purely because Damage was written more recently.
            Assert.Equal(0, rejected);
            Assert.False(await ReadLogPreferenceAsync(playerId, ELogType.Exp));
            Assert.False(await ReadLogPreferenceAsync(playerId, ELogType.Damage));
        }

        [Fact]
        public async Task ItemFavoriteChanged_OlderSequenceAfterNewer_IsSkippedAndLeavesTheWatermark()
        {
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            await ApplyAsync(new ItemFavoriteChangedEvent(playerId, itemId, Favorite: true), sequence: 2);
            var rejected = await ApplyAsync(new ItemFavoriteChangedEvent(playerId, itemId, Favorite: false), sequence: 1);

            Assert.Equal(1, rejected);
            Assert.True(await ReadFavoriteAsync(playerId, itemId));
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.ItemFavorite, ItemTarget(itemId)));
        }

        [Fact]
        public async Task ItemFavoriteChanged_EqualSequence_Applies()
        {
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            await ApplyAsync(new ItemFavoriteChangedEvent(playerId, itemId, Favorite: true), sequence: 3);
            var rejected = await ApplyAsync(new ItemFavoriteChangedEvent(playerId, itemId, Favorite: false), sequence: 3);

            Assert.Equal(0, rejected);
            Assert.False(await ReadFavoriteAsync(playerId, itemId));
        }

        [Fact]
        public async Task ItemFavoriteChanged_NewerSequence_AppliesAndAdvancesTheWatermark()
        {
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            await ApplyAsync(new ItemFavoriteChangedEvent(playerId, itemId, Favorite: false), sequence: 1);
            var rejected = await ApplyAsync(new ItemFavoriteChangedEvent(playerId, itemId, Favorite: true), sequence: 2);

            Assert.Equal(0, rejected);
            Assert.True(await ReadFavoriteAsync(playerId, itemId));
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.ItemFavorite, ItemTarget(itemId)));
        }

        [Fact]
        public async Task ItemFavoriteChanged_UnsequencedEvent_AppliesAgainstAnAdvancedWatermarkAndLeavesItUnchanged()
        {
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            await ApplyAsync(new ItemFavoriteChangedEvent(playerId, itemId, Favorite: true), sequence: 5);
            var rejected = await ApplyAsync(new ItemFavoriteChangedEvent(playerId, itemId, Favorite: false), sequence: DomainEventEnvelope.Unsequenced);

            Assert.Equal(0, rejected);
            Assert.False(await ReadFavoriteAsync(playerId, itemId));
            Assert.Equal(5, await ReadWatermarkAsync(playerId, PlayerWriteStream.ItemFavorite, ItemTarget(itemId)));
        }

        [Fact]
        public async Task ItemFavoriteChanged_OlderEventForADifferentItem_IsNotRejectedByTheNewerOne()
        {
            var playerId = await SeedPlayerAsync();
            var favoritedItemId = await SeedItemAsync();
            var otherItemId = await SeedItemAsync();

            await ApplyAsync(new ItemFavoriteChangedEvent(playerId, favoritedItemId, Favorite: true), sequence: 2);
            var rejected = await ApplyAsync(new ItemFavoriteChangedEvent(playerId, otherItemId, Favorite: true), sequence: 1);

            Assert.Equal(0, rejected);
            Assert.True(await ReadFavoriteAsync(playerId, otherItemId));
            Assert.True(await ReadFavoriteAsync(playerId, favoritedItemId));
        }

        [Fact]
        public async Task LessonRead_OlderSequenceAfterNewer_IsSkippedAndLeavesTheWatermark()
        {
            var playerId = await SeedPlayerAsync();
            var lessonId = await SeedLessonAsync();

            await ApplyAsync(ReadLesson(playerId, lessonId, readAtMinute: 20), sequence: 2);
            var rejected = await ApplyAsync(ReadLesson(playerId, lessonId, readAtMinute: 5), sequence: 1);

            Assert.Equal(1, rejected);
            Assert.Equal(LessonUnlockedAt.AddMinutes(20), await ReadLessonReadAtAsync(playerId, lessonId));
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.LessonRead, LessonTarget(lessonId)));
        }

        [Fact]
        public async Task LessonRead_EqualSequence_Applies()
        {
            var playerId = await SeedPlayerAsync();
            var lessonId = await SeedLessonAsync();

            await ApplyAsync(ReadLesson(playerId, lessonId, readAtMinute: 5), sequence: 3);
            var rejected = await ApplyAsync(ReadLesson(playerId, lessonId, readAtMinute: 20), sequence: 3);

            Assert.Equal(0, rejected);
            Assert.Equal(LessonUnlockedAt.AddMinutes(20), await ReadLessonReadAtAsync(playerId, lessonId));
        }

        [Fact]
        public async Task LessonRead_NewerSequence_AppliesAndAdvancesTheWatermark()
        {
            var playerId = await SeedPlayerAsync();
            var lessonId = await SeedLessonAsync();

            await ApplyAsync(ReadLesson(playerId, lessonId, readAtMinute: 5), sequence: 1);
            var rejected = await ApplyAsync(ReadLesson(playerId, lessonId, readAtMinute: 20), sequence: 2);

            Assert.Equal(0, rejected);
            Assert.Equal(LessonUnlockedAt.AddMinutes(20), await ReadLessonReadAtAsync(playerId, lessonId));
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.LessonRead, LessonTarget(lessonId)));
        }

        [Fact]
        public async Task LessonRead_UnsequencedEvent_AppliesAgainstAnAdvancedWatermarkAndLeavesItUnchanged()
        {
            var playerId = await SeedPlayerAsync();
            var lessonId = await SeedLessonAsync();

            await ApplyAsync(ReadLesson(playerId, lessonId, readAtMinute: 20), sequence: 5);
            var rejected = await ApplyAsync(ReadLesson(playerId, lessonId, readAtMinute: 5), sequence: DomainEventEnvelope.Unsequenced);

            Assert.Equal(0, rejected);
            Assert.Equal(LessonUnlockedAt.AddMinutes(5), await ReadLessonReadAtAsync(playerId, lessonId));
            Assert.Equal(5, await ReadWatermarkAsync(playerId, PlayerWriteStream.LessonRead, LessonTarget(lessonId)));
        }

        [Fact]
        public async Task LessonRead_OlderEventForADifferentLesson_IsNotRejectedByTheNewerOne()
        {
            var playerId = await SeedPlayerAsync();
            var readLessonId = await SeedLessonAsync("read-lesson");
            var otherLessonId = await SeedLessonAsync("other-lesson");

            await ApplyAsync(ReadLesson(playerId, readLessonId, readAtMinute: 20), sequence: 2);
            var rejected = await ApplyAsync(ReadLesson(playerId, otherLessonId, readAtMinute: 5), sequence: 1);

            Assert.Equal(0, rejected);
            Assert.Equal(LessonUnlockedAt.AddMinutes(5), await ReadLessonReadAtAsync(playerId, otherLessonId));
            Assert.Equal(LessonUnlockedAt.AddMinutes(20), await ReadLessonReadAtAsync(playerId, readLessonId));
        }

        [Fact]
        public async Task PlayerProducedStreams_AreIndependent_SoOneStreamsWriteDoesNotBlockAnOlderWriteOnAnother()
        {
            var playerId = await SeedPlayerAsync();

            // Every stream the Player aggregate produces shares one counter, so a later save's sequence is
            // genuinely higher — but the streams write disjoint targets, and an older event on one of them is
            // only stale relative to its own target. Sharing a watermark across them would drop it.
            await ApplyAsync(AllocationEvent(playerId, strength: 20d), sequence: 9);
            var rejected = await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: false), sequence: 1);

            Assert.Equal(0, rejected);
            Assert.False(await ReadLogPreferenceAsync(playerId, ELogType.Damage));
        }

        // A separate scope means a separate GameContext on its own connection, so this commits independently
        // of the guard's in-flight transaction rather than joining it.
        private async Task InsertGlobalKillsFromAnotherScopeAsync(int playerId, decimal value)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            context.PlayerStatistics.Add(new PlayerStatistic
            {
                PlayerId = playerId,
                StatisticTypeId = (int)EStatisticType.EnemiesKilled,
                EntityId = null,
                Value = value,
            });
            await context.SaveChangesAsync(CancellationToken);
        }

        private static PlayerCoreUpdatedEvent CoreEvent(int playerId, int level, int exp)
            => new(playerId, level, exp, 0, 100, 100, DateTime.UtcNow, false, null);

        // The event always carries the player's complete spread, which is what makes its player-scoped
        // watermark key defensible — so the fixture states every allocation rather than just the varying one.
        private static AttributeAllocationsChangedEvent AllocationEvent(int playerId, double strength) => new(
            playerId,
            [
                new AttributeAllocationEntry(EAttribute.Strength, strength),
                new AttributeAllocationEntry(EAttribute.Intellect, 0d),
            ]);

        private static LessonReadEvent ReadLesson(int playerId, int lessonId, int readAtMinute)
            => new(playerId, lessonId, LessonUnlockedAt, LessonUnlockedAt.AddMinutes(readAtMinute));

        // The target keys the guarded handlers derive, restated here so a test asserts against the format the
        // stream documents rather than against whatever the handler happens to produce.
        private static string LogTarget(ELogType logType) => ((int)logType).ToString(CultureInfo.InvariantCulture);
        private static string ItemTarget(int itemId) => itemId.ToString(CultureInfo.InvariantCulture);
        private static string LessonTarget(int lessonId) => lessonId.ToString(CultureInfo.InvariantCulture);

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

        private async Task<decimal?> ReadAllocationAsync(int playerId, EAttribute attribute)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = await context.PlayerAttributes.AsNoTracking()
                .SingleOrDefaultAsync(pa => pa.PlayerId == playerId && pa.AttributeId == (int)attribute, CancellationToken);
            return row?.Amount;
        }

        private async Task<List<int>> ReadSelectedSkillIdsAsync(int playerId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return await context.PlayerSkills.AsNoTracking()
                .Where(ps => ps.PlayerId == playerId && ps.Selected)
                .OrderBy(ps => ps.Order)
                .Select(ps => ps.SkillId)
                .ToListAsync(CancellationToken);
        }

        private async Task<bool?> ReadLogPreferenceAsync(int playerId, ELogType logType)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = await context.LogPreferences.AsNoTracking()
                .SingleOrDefaultAsync(lp => lp.PlayerId == playerId && lp.LogTypeId == (int)logType, CancellationToken);
            return row?.Enabled;
        }

        private async Task<bool?> ReadFavoriteAsync(int playerId, int itemId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = await context.UnlockedItems.AsNoTracking()
                .SingleOrDefaultAsync(ui => ui.PlayerId == playerId && ui.ItemId == itemId, CancellationToken);
            return row?.Favorite;
        }

        private async Task<DateTime?> ReadLessonReadAtAsync(int playerId, int lessonId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = await context.PlayerLessons.AsNoTracking()
                .SingleOrDefaultAsync(pl => pl.PlayerId == playerId && pl.LessonId == lessonId, CancellationToken);
            return row?.ReadAt;
        }

        private async Task<int> SeedSkillAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return (await TestDataSeeder.CreateSkillAsync(context)).Id;
        }

        private async Task<int> SeedItemAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return (await TestDataSeeder.CreateItemAsync(context)).Id;
        }

        // Lesson keys are globally unique, so a test seeding two lessons has to name them apart.
        private async Task<int> SeedLessonAsync(string key = "test-lesson")
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return (await TestDataSeeder.CreateLessonAsync(context, key)).Id;
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
