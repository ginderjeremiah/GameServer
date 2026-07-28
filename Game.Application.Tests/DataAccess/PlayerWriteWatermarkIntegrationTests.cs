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

        private const int Helm = (int)EEquipmentSlot.HelmSlot;
        private const int Chest = (int)EEquipmentSlot.ChestSlot;

        // The prerequisites a ModAppliedEvent assumes already exist, plus a second mod so a re-apply can be
        // told apart from a no-op rather than only from a row count.
        private sealed record ModFixture(int PlayerId, int ItemId, int SlotId, int FirstModId, int SecondModId);

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
        public async Task ItemEquipped_NewerSequence_AppliesAndAdvancesBothTheItemAndTheSlotWatermarks()
        {
            var (playerId, itemId) = await SeedUnlockedItemAsync();

            var rejected = await ApplyAsync(new ItemEquippedEvent(playerId, itemId, Helm), sequence: 2);

            Assert.Equal(0, rejected);
            Assert.Equal(Helm, await ReadEquippedSlotAsync(playerId, itemId));
            // An equip writes two identities at once, so it advances both — neither key is decorative.
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.Equipment, PlayerWriteTargets.Equipment.Item(itemId)));
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.Equipment, PlayerWriteTargets.Equipment.Slot(Helm)));
        }

        [Fact]
        public async Task ItemEquipped_OlderEquipIntoASlotANewerEquipOwns_IsRejectedAndAdvancesNeitherWatermark()
        {
            var (playerId, incumbentId) = await SeedUnlockedItemAsync();
            var challengerId = await SeedItemForAsync(playerId);

            await ApplyAsync(new ItemEquippedEvent(playerId, incumbentId, Helm), sequence: 5);
            var rejected = await ApplyAsync(new ItemEquippedEvent(playerId, challengerId, Helm), sequence: 3);

            // The challenger's own item key has never been written, so it passes on its own; only the slot key
            // sees that this equip is stale. Applying it anyway would durably strip the incumbent.
            Assert.Equal(2, rejected);
            Assert.Equal(Helm, await ReadEquippedSlotAsync(playerId, incumbentId));
            Assert.Null(await ReadEquippedSlotAsync(playerId, challengerId));

            // All-or-nothing: the passing key must not keep its advance. A watermark seeded at 3 with no write
            // behind it would make a genuinely older event look already applied and drop it for good.
            Assert.Null(await ReadWatermarkAsync(playerId, PlayerWriteStream.Equipment, PlayerWriteTargets.Equipment.Item(challengerId)));
            Assert.Equal(5, await ReadWatermarkAsync(playerId, PlayerWriteStream.Equipment, PlayerWriteTargets.Equipment.Slot(Helm)));
        }

        [Fact]
        public async Task ItemEquipped_ReplayedOlderEquipAfterTheItemMovedOn_IsRejectedByTheItemKey()
        {
            var (playerId, itemId) = await SeedUnlockedItemAsync();

            await ApplyAsync(new ItemEquippedEvent(playerId, itemId, Helm), sequence: 2);
            await ApplyAsync(new ItemEquippedEvent(playerId, itemId, Chest), sequence: 4);

            // The move to Chest reassigned the item's own row, so it left the Helm watermark sitting at 2 —
            // which accepts this replay. Only the item key, at 4, can see that the replay is stale, which is
            // why the slot key alone would leave a hole: without it the replay drags the item back to Helm.
            var rejected = await ApplyAsync(new ItemEquippedEvent(playerId, itemId, Helm), sequence: 3);

            Assert.Equal(2, rejected);
            Assert.Equal(Chest, await ReadEquippedSlotAsync(playerId, itemId));
            Assert.Equal(2, await ReadWatermarkAsync(playerId, PlayerWriteStream.Equipment, PlayerWriteTargets.Equipment.Slot(Helm)));
            Assert.Equal(4, await ReadWatermarkAsync(playerId, PlayerWriteStream.Equipment, PlayerWriteTargets.Equipment.Item(itemId)));
        }

        [Fact]
        public async Task ItemEquipped_EqualSequence_Applies()
        {
            var (playerId, incumbentId) = await SeedUnlockedItemAsync();
            var challengerId = await SeedItemForAsync(playerId);

            await ApplyAsync(new ItemEquippedEvent(playerId, incumbentId, Helm), sequence: 4);
            // Both keys are at 4 and the predicate is <=, so the same save's sibling equip still lands.
            var rejected = await ApplyAsync(new ItemEquippedEvent(playerId, challengerId, Helm), sequence: 4);

            Assert.Equal(0, rejected);
            Assert.Equal(Helm, await ReadEquippedSlotAsync(playerId, challengerId));
            Assert.Null(await ReadEquippedSlotAsync(playerId, incumbentId));
        }

        [Fact]
        public async Task ItemEquipped_UnsequencedEvent_AppliesAgainstAdvancedWatermarksAndLeavesThemUnchanged()
        {
            var (playerId, incumbentId) = await SeedUnlockedItemAsync();
            var challengerId = await SeedItemForAsync(playerId);

            await ApplyAsync(new ItemEquippedEvent(playerId, incumbentId, Helm), sequence: 5);
            var rejected = await ApplyAsync(new ItemEquippedEvent(playerId, challengerId, Helm), sequence: DomainEventEnvelope.Unsequenced);

            Assert.Equal(0, rejected);
            Assert.Equal(Helm, await ReadEquippedSlotAsync(playerId, challengerId));
            Assert.Equal(5, await ReadWatermarkAsync(playerId, PlayerWriteStream.Equipment, PlayerWriteTargets.Equipment.Slot(Helm)));
            Assert.Null(await ReadWatermarkAsync(playerId, PlayerWriteStream.Equipment, PlayerWriteTargets.Equipment.Item(challengerId)));
        }

        [Fact]
        public async Task ItemUnequipped_OlderThanTheEquipThatFollowedIt_IsSkippedSoTheItemStaysWorn()
        {
            var (playerId, itemId) = await SeedUnlockedItemAsync();

            await ApplyAsync(new ItemEquippedEvent(playerId, itemId, Helm), sequence: 6);

            // An unequip carries no slot, so the item key is the only thing that can order it against the
            // newer equip. Unguarded, this replay durably strips a worn item — and because Redis holds the
            // correct state, nothing corrects the row until the player touches that slot again.
            var rejected = await ApplyAsync(new ItemUnequippedEvent(playerId, itemId), sequence: 4);

            Assert.Equal(1, rejected);
            Assert.Equal(Helm, await ReadEquippedSlotAsync(playerId, itemId));
            Assert.Equal(6, await ReadWatermarkAsync(playerId, PlayerWriteStream.Equipment, PlayerWriteTargets.Equipment.Item(itemId)));
        }

        [Fact]
        public async Task ItemUnequipped_EqualThenNewerSequence_AppliesAndAdvancesTheItemWatermark()
        {
            var (playerId, itemId) = await SeedUnlockedItemAsync();

            await ApplyAsync(new ItemEquippedEvent(playerId, itemId, Helm), sequence: 3);
            // Equal first (the same save's sibling writes), then strictly newer.
            Assert.Equal(0, await ApplyAsync(new ItemUnequippedEvent(playerId, itemId), sequence: 3));
            Assert.Null(await ReadEquippedSlotAsync(playerId, itemId));

            await ApplyAsync(new ItemEquippedEvent(playerId, itemId, Helm), sequence: 7);
            var rejected = await ApplyAsync(new ItemUnequippedEvent(playerId, itemId), sequence: 8);

            Assert.Equal(0, rejected);
            Assert.Null(await ReadEquippedSlotAsync(playerId, itemId));
            Assert.Equal(8, await ReadWatermarkAsync(playerId, PlayerWriteStream.Equipment, PlayerWriteTargets.Equipment.Item(itemId)));
        }

        [Fact]
        public async Task ItemUnequipped_UnsequencedEvent_AppliesAgainstAnAdvancedWatermarkAndLeavesItUnchanged()
        {
            var (playerId, itemId) = await SeedUnlockedItemAsync();

            await ApplyAsync(new ItemEquippedEvent(playerId, itemId, Helm), sequence: 6);
            var rejected = await ApplyAsync(new ItemUnequippedEvent(playerId, itemId), sequence: DomainEventEnvelope.Unsequenced);

            Assert.Equal(0, rejected);
            Assert.Null(await ReadEquippedSlotAsync(playerId, itemId));
            Assert.Equal(6, await ReadWatermarkAsync(playerId, PlayerWriteStream.Equipment, PlayerWriteTargets.Equipment.Item(itemId)));
        }

        [Fact]
        public async Task ModApplied_OlderSequenceAfterNewer_IsSkippedAndLeavesTheWatermark()
        {
            var mods = await SeedModFixtureAsync();

            await ApplyAsync(new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.SecondModId), sequence: 4);
            var rejected = await ApplyAsync(new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.FirstModId), sequence: 2);

            Assert.Equal(1, rejected);
            Assert.Equal(mods.SecondModId, await ReadAppliedModAsync(mods.PlayerId, mods.ItemId, mods.SlotId));
            Assert.Equal(4, await ReadWatermarkAsync(mods.PlayerId, PlayerWriteStream.Mods, PlayerWriteTargets.Mods.Slot(mods.ItemId, mods.SlotId)));
        }

        [Fact]
        public async Task ModApplied_EqualThenNewerSequence_BothApplyAndTheWatermarkEndsAtTheNewest()
        {
            var mods = await SeedModFixtureAsync();

            await ApplyAsync(new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.FirstModId), sequence: 3);
            Assert.Equal(0, await ApplyAsync(new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.SecondModId), sequence: 3));
            Assert.Equal(mods.SecondModId, await ReadAppliedModAsync(mods.PlayerId, mods.ItemId, mods.SlotId));

            var rejected = await ApplyAsync(new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.FirstModId), sequence: 5);

            Assert.Equal(0, rejected);
            Assert.Equal(mods.FirstModId, await ReadAppliedModAsync(mods.PlayerId, mods.ItemId, mods.SlotId));
            Assert.Equal(5, await ReadWatermarkAsync(mods.PlayerId, PlayerWriteStream.Mods, PlayerWriteTargets.Mods.Slot(mods.ItemId, mods.SlotId)));
        }

        [Fact]
        public async Task ModApplied_UnsequencedEvent_AppliesAgainstAnAdvancedWatermarkAndLeavesItUnchanged()
        {
            var mods = await SeedModFixtureAsync();

            await ApplyAsync(new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.SecondModId), sequence: 6);
            var rejected = await ApplyAsync(new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.FirstModId), sequence: DomainEventEnvelope.Unsequenced);

            Assert.Equal(0, rejected);
            Assert.Equal(mods.FirstModId, await ReadAppliedModAsync(mods.PlayerId, mods.ItemId, mods.SlotId));
            Assert.Equal(6, await ReadWatermarkAsync(mods.PlayerId, PlayerWriteStream.Mods, PlayerWriteTargets.Mods.Slot(mods.ItemId, mods.SlotId)));
        }

        [Fact]
        public async Task ModApplied_StaleAfterANewerModRemoved_DoesNotResurrectTheMod()
        {
            var mods = await SeedModFixtureAsync();

            await ApplyAsync(new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.FirstModId), sequence: 2);
            await ApplyAsync(new ModRemovedEvent(mods.PlayerId, mods.ItemId, mods.SlotId), sequence: 5);

            // This is why the watermark is a separate row rather than a version column on AppliedMod: the
            // remove deleted the row the version would have lived on, so a stale apply would find nothing to
            // compare against and put the mod back on an item the player has already stripped.
            var rejected = await ApplyAsync(new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.SecondModId), sequence: 3);

            Assert.Equal(1, rejected);
            Assert.Null(await ReadAppliedModAsync(mods.PlayerId, mods.ItemId, mods.SlotId));
            Assert.Equal(5, await ReadWatermarkAsync(mods.PlayerId, PlayerWriteStream.Mods, PlayerWriteTargets.Mods.Slot(mods.ItemId, mods.SlotId)));
        }

        [Fact]
        public async Task ModRemoved_OlderThanTheApplyThatFollowedIt_IsSkippedSoTheModSurvives()
        {
            var mods = await SeedModFixtureAsync();

            await ApplyAsync(new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.FirstModId), sequence: 7);
            var rejected = await ApplyAsync(new ModRemovedEvent(mods.PlayerId, mods.ItemId, mods.SlotId), sequence: 4);

            Assert.Equal(1, rejected);
            Assert.Equal(mods.FirstModId, await ReadAppliedModAsync(mods.PlayerId, mods.ItemId, mods.SlotId));
            Assert.Equal(7, await ReadWatermarkAsync(mods.PlayerId, PlayerWriteStream.Mods, PlayerWriteTargets.Mods.Slot(mods.ItemId, mods.SlotId)));
        }

        [Fact]
        public async Task ModRemoved_EqualSequenceToTheApplyItFollows_StillRemovesTheMod()
        {
            var mods = await SeedModFixtureAsync();

            await ApplyAsync(new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.FirstModId), sequence: 4);

            // A save that swaps a mod out stamps its remove and its apply at one sequence, and the two share
            // this stream's key by design — so the remove lands on a watermark its sibling apply just advanced
            // to the same value. Under a strict < predicate it would be silently skipped and the mod would
            // survive a removal the player actually made.
            var rejected = await ApplyAsync(new ModRemovedEvent(mods.PlayerId, mods.ItemId, mods.SlotId), sequence: 4);

            Assert.Equal(0, rejected);
            Assert.Null(await ReadAppliedModAsync(mods.PlayerId, mods.ItemId, mods.SlotId));
            Assert.Equal(4, await ReadWatermarkAsync(mods.PlayerId, PlayerWriteStream.Mods, PlayerWriteTargets.Mods.Slot(mods.ItemId, mods.SlotId)));
        }

        [Fact]
        public async Task ModRemoved_UnsequencedEvent_AppliesAgainstAnAdvancedWatermarkAndLeavesItUnchanged()
        {
            var mods = await SeedModFixtureAsync();

            await ApplyAsync(new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.FirstModId), sequence: 6);
            var rejected = await ApplyAsync(new ModRemovedEvent(mods.PlayerId, mods.ItemId, mods.SlotId), sequence: DomainEventEnvelope.Unsequenced);

            Assert.Equal(0, rejected);
            Assert.Null(await ReadAppliedModAsync(mods.PlayerId, mods.ItemId, mods.SlotId));
            Assert.Equal(6, await ReadWatermarkAsync(mods.PlayerId, PlayerWriteStream.Mods, PlayerWriteTargets.Mods.Slot(mods.ItemId, mods.SlotId)));
        }

        [Fact]
        public async Task ModApplied_ToADifferentSlotOnTheSameItem_IsGuardedIndependently()
        {
            var mods = await SeedModFixtureAsync();
            int otherSlotId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                otherSlotId = (await TestDataSeeder.AddItemModSlotAsync(context, mods.ItemId)).Id;
            }

            await ApplyAsync(new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.FirstModId), sequence: 6);

            // Keyed per mod slot: an older event targeting a slot the newer one never touched is still current
            // for that slot, so a coarser per-item (or per-player) key would silently drop it.
            var rejected = await ApplyAsync(new ModAppliedEvent(mods.PlayerId, mods.ItemId, otherSlotId, mods.SecondModId), sequence: 2);

            Assert.Equal(0, rejected);
            Assert.Equal(mods.FirstModId, await ReadAppliedModAsync(mods.PlayerId, mods.ItemId, mods.SlotId));
            Assert.Equal(mods.SecondModId, await ReadAppliedModAsync(mods.PlayerId, mods.ItemId, otherSlotId));
        }

        [Fact]
        public async Task ItemEquipped_DifferentItemsRacingIntoOneSlotAtEqualSequences_ConvergesToASingleOccupant()
        {
            var (playerId, firstItemId) = await SeedUnlockedItemAsync();
            var secondItemId = await SeedItemForAsync(playerId);

            // The idempotency suite's version of this race runs unsequenced, so it bypasses the guard and has
            // to swallow a DbUpdateException and rely on redelivery. Guarded, the two applies queue on the
            // slot's watermark row and the vacate-then-place is atomic, so neither throws and the slot is never
            // doubly occupied. Equal sequences so both pass the predicate and genuinely contend.
            await ApplyConcurrentlyAsync(sequence: 3,
                new ItemEquippedEvent(playerId, firstItemId, Helm),
                new ItemEquippedEvent(playerId, secondItemId, Helm));

            var slots = new[]
            {
                await ReadEquippedSlotAsync(playerId, firstItemId),
                await ReadEquippedSlotAsync(playerId, secondItemId),
            };
            Assert.Single(slots, slot => slot == Helm);
            Assert.Single(slots, slot => slot is null);
        }

        [Fact]
        public async Task ModApplied_RacingAppliesToOneSlotAtEqualSequences_ConvergeToOneRow()
        {
            var mods = await SeedModFixtureAsync();

            await ApplyConcurrentlyAsync(sequence: 3,
                new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.FirstModId),
                new ModAppliedEvent(mods.PlayerId, mods.ItemId, mods.SlotId, mods.SecondModId));

            // Last writer wins, whichever that was; what must hold is that exactly one mod is on the slot and
            // neither apply surfaced the primary-key violation to the queue.
            var applied = await ReadAppliedModAsync(mods.PlayerId, mods.ItemId, mods.SlotId);
            Assert.Contains(applied, new int?[] { mods.FirstModId, mods.SecondModId });
            Assert.Equal(3, await ReadWatermarkAsync(mods.PlayerId, PlayerWriteStream.Mods, PlayerWriteTargets.Mods.Slot(mods.ItemId, mods.SlotId)));
        }

        // Applies the given events at one sequence through independent scopes at once, the cross-instance
        // contention the watermark row is meant to serialize rather than merely detect.
        private async Task ApplyConcurrentlyAsync<TEvent>(long sequence, params TEvent[] events)
        {
            var scopes = events.Select(_ => CreateScope()).ToList();
            try
            {
                await Task.WhenAll(events.Zip(scopes, (evt, scope) => Task.Run(() =>
                {
                    DescribeSequence(scope, sequence);
                    return scope.ServiceProvider.GetRequiredService<IPlayerUpdateHandler<TEvent>>().HandleAsync(evt);
                })));
            }
            finally
            {
                foreach (var scope in scopes)
                {
                    scope.Dispose();
                }
            }
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

        private async Task<int?> ReadEquippedSlotAsync(int playerId, int itemId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = await context.UnlockedItems.AsNoTracking()
                .SingleAsync(ui => ui.PlayerId == playerId && ui.ItemId == itemId, CancellationToken);
            return row.EquipmentSlotId;
        }

        private async Task<int?> ReadAppliedModAsync(int playerId, int itemId, int modSlotId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = await context.AppliedMods.AsNoTracking()
                .SingleOrDefaultAsync(am => am.PlayerId == playerId && am.ItemId == itemId && am.ItemModSlotId == modSlotId, CancellationToken);
            return row?.ItemModId;
        }

        private async Task<(int PlayerId, int ItemId)> SeedUnlockedItemAsync()
        {
            var playerId = await SeedPlayerAsync();
            return (playerId, await SeedItemForAsync(playerId));
        }

        // Unlocked but unequipped, so a test's first equip is the write under test rather than seed state.
        private async Task<int> SeedItemForAsync(int playerId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var itemId = (await TestDataSeeder.CreateItemAsync(context)).Id;
            await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, itemId);
            return itemId;
        }

        private async Task<ModFixture> SeedModFixtureAsync()
        {
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemForAsync(playerId);
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var slotId = (await TestDataSeeder.AddItemModSlotAsync(context, itemId)).Id;
            var firstModId = (await TestDataSeeder.CreateItemModAsync(context)).Id;
            var secondModId = (await TestDataSeeder.CreateItemModAsync(context, name: "Second Mod")).Id;
            await TestDataSeeder.LinkModToPlayerAsync(context, playerId, firstModId);
            await TestDataSeeder.LinkModToPlayerAsync(context, playerId, secondModId);
            return new ModFixture(playerId, itemId, slotId, firstModId, secondModId);
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
